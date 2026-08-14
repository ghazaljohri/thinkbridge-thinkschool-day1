using Azure.Core;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Security.KeyVault.Secrets;
using QuotesApi.Options;
using QuotesApi.Services;
using QuotesApi.Services.Auth;
using QuotesApi.Extensions;
using QuotesApi.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Context;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;

var builder = WebApplication.CreateBuilder(args);

// KeyVault:Uri is intentionally absent from every checked-in appsettings file - it's
// set only via the KeyVault__Uri environment variable for local runs against real
// Azure Monitor. Left unset (as in tests and CI), no Key Vault call is made at all,
// so this can't add latency or an Azure dependency to the test suite. Fetched before
// UseSerilog/UseAzureMonitor below since both need it.
var keyVaultUri = builder.Configuration["KeyVault:Uri"];
string? appInsightsConnectionString = null;

if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    // DefaultAzureCredential probes Managed Identity first, which means reaching the
    // Azure Instance Metadata Service - on a real Azure host that resolves almost
    // instantly, but on a local dev machine with no IMDS it hangs for minutes before
    // timing out. In Development, skip straight to the Azure CLI credential from
    // `az login`; DefaultAzureCredential remains correct for actual Azure hosting.
    TokenCredential credential = builder.Environment.IsDevelopment()
        ? new AzureCliCredential()
        : new DefaultAzureCredential();

    var secretClient = new SecretClient(new Uri(keyVaultUri), credential);
    var connectionStringSecret = await secretClient.GetSecretAsync("AppInsights-ConnectionString");
    appInsightsConnectionString = connectionStringSecret.Value.Value;
}

// preserveStaticLogger avoids repeatedly overwriting the global Serilog.Log.Logger,
// since integration tests build many hosts from this same Program in one process.
// Once UseSerilog runs, Serilog becomes the *sole* Microsoft.Extensions.Logging
// pipeline and never forwards events to other registered ILoggerProviders - so the
// OpenTelemetry logging provider UseAzureMonitor() registers below never receives
// anything, confirmed by disabling Serilog entirely and watching logs immediately
// start reaching Application Insights. Rather than fight that, logs reach Azure
// Monitor via Serilog's own native ApplicationInsights sink instead - traces and
// metrics still flow through the OpenTelemetry/UseAzureMonitor pipeline below,
// which works correctly since it doesn't depend on Microsoft.Extensions.Logging.
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();

    if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
    {
        configuration.WriteTo.ApplicationInsights(
            appInsightsConnectionString,
            new TraceTelemetryConverter());
    }
},
preserveStaticLogger: true);

var openTelemetryBuilder = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(QuotesApiActivitySource.Name))
    .WithTracing(tracing => tracing
        .AddSource(QuotesApiActivitySource.Name)
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    openTelemetryBuilder.UseAzureMonitor(options =>
        options.ConnectionString = appInsightsConnectionString);
}

const string localJwtScheme = "LocalJwt";
const string entraJwtScheme = "EntraJwt";
const string bearerScheme = "Bearer";

// Jwt:SigningKey is a secret and never lives in appsettings.json - locally it comes
// from `dotnet user-secrets set Jwt:SigningKey ...`; in production, an env var
// (Jwt__SigningKey) holding a Key Vault reference that the hosting platform resolves
// before the app ever sees it. AccessTokenLifetime/RefreshTokenLifetime aren't
// secrets, so they're plain values in appsettings.json.
// ValidateOnStart fails fast at startup with a clear error if a required value is
// missing, instead of surfacing a confusing failure later the first time the option
// is actually resolved (e.g. on the first Entra-scheme request).
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<EntraOptions>()
    .Bind(builder.Configuration.GetSection("Entra"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = bearerScheme;
        options.DefaultChallengeScheme = bearerScheme;
    })
    .AddPolicyScheme(bearerScheme, "Selects local or Microsoft Entra JWT validation", _ => { })
    .AddJwtBearer(localJwtScheme)
    .AddJwtBearer(entraJwtScheme);

// PolicySchemeOptions/JwtBearerOptions are framework-managed via IOptionsMonitor
// internally and rebuilt whenever the underlying options change, so configuring them
// from IOptionsMonitor<TOptions> here means a config change (e.g. a rotated Entra
// tenant) is picked up next time these options are resolved - not frozen at the value
// captured when the process started, the way a plain local variable would be.
builder.Services.AddOptions<PolicySchemeOptions>(bearerScheme)
    .Configure<IOptionsMonitor<EntraOptions>>((options, entraMonitor) =>
    {
        options.ForwardDefaultSelector = context => AuthSchemeSelector.SelectScheme(
            context.Request.Headers.Authorization.ToString(),
            entraMonitor.CurrentValue.Authority,
            localJwtScheme,
            entraJwtScheme);
    });

builder.Services.AddOptions<JwtBearerOptions>(localJwtScheme)
    .Configure<IOptionsMonitor<JwtOptions>>((options, jwtMonitor) =>
    {
        // ValidateOnStart on JwtOptions above already guarantees SigningKey is
        // non-empty by the time anything can resolve CurrentValue here.
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(jwtMonitor.CurrentValue.SigningKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddOptions<JwtBearerOptions>(entraJwtScheme)
    .Configure<IOptionsMonitor<EntraOptions>>((options, entraMonitor) =>
    {
        var entra = entraMonitor.CurrentValue;

        options.Authority = entra.Authority;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidAudience = entra.EffectiveAudience,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                EntraClaimsTransformer.ApplyScopeClaims(context.Principal);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
    CanDeleteOwnQuoteHandler>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("can-edit-quotes", policy =>
    {
        policy.RequireClaim("scope", "quotes.write");
    });

    options.AddPolicy("can-delete-own-quote", policy =>
    {
        policy.Requirements.Add(new CanDeleteOwnQuoteRequirement());
    });
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<RefreshTokenService>();

var app = builder.Build();

app.Use((context, next) =>
{
    using (LogContext.PushProperty("TraceId", context.TraceIdentifier))
        return next();
});

// UseSerilogRequestLogging defaults to Serilog.Log.Logger (the static ambient logger),
// which preserveStaticLogger deliberately leaves unconfigured. Bind it explicitly to
// this host's own scoped logger so each of the many hosts our tests spin up logs to
// itself rather than fighting over the static logger.
app.UseSerilogRequestLogging(options =>
{
    options.Logger = app.Services.GetRequiredService<Serilog.ILogger>();
});

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

await app.ApplyMigrationsAsync();

app.MapGet("/", () => "Quotes API is running!");
app.MapAuthEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QuotesApi.Data.AppDbContext>();

    if (!db.Users.Any())
    {
        db.Users.Add(new QuotesApi.Models.Auth.User
        {
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!")
        });

        await db.SaveChangesAsync();
    }
}
app.MapQuoteEndpoints();
app.MapCollectionEndpoints();

app.Run();

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Unhandled exception for {Path}",
            httpContext.Request.Path);

        httpContext.Response.StatusCode = 500;

        await Results.Problem(
            statusCode: 500,
            title: "An unexpected error occurred.")
            .ExecuteAsync(httpContext);

        return true;
    }
}

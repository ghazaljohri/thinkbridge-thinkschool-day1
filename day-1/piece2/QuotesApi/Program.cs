using QuotesApi.Services.Auth;
using QuotesApi.Extensions;
using QuotesApi.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Serilog;
using Serilog.Context;

var builder = WebApplication.CreateBuilder(args);

// preserveStaticLogger avoids repeatedly overwriting the global Serilog.Log.Logger,
// since integration tests build many hosts from this same Program in one process.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext(),
    preserveStaticLogger: true);

const string localJwtScheme = "LocalJwt";
const string entraJwtScheme = "EntraJwt";
const string bearerScheme = "Bearer";

var entraTenantId = builder.Configuration["Entra:TenantId"]
    ?? throw new InvalidOperationException("Entra tenant ID is not configured.");
var entraClientId = builder.Configuration["Entra:ClientId"]
    ?? throw new InvalidOperationException("Entra client ID is not configured.");
var entraAudience = builder.Configuration["Entra:Audience"]
    ?? $"api://{entraClientId}";
var entraAuthority = $"https://login.microsoftonline.com/{entraTenantId}/v2.0";

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = bearerScheme;
        options.DefaultChallengeScheme = bearerScheme;
    })
    .AddPolicyScheme(bearerScheme, "Selects local or Microsoft Entra JWT validation", options =>
    {
        options.ForwardDefaultSelector = context => AuthSchemeSelector.SelectScheme(
            context.Request.Headers.Authorization.ToString(),
            entraAuthority,
            localJwtScheme,
            entraJwtScheme);
    })
    .AddJwtBearer(localJwtScheme, options =>
    {
        var key = builder.Configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT key is not configured.");

        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(key)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    })
    .AddJwtBearer(entraJwtScheme, options =>
    {
        options.Authority = entraAuthority;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidAudience = entraAudience,
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

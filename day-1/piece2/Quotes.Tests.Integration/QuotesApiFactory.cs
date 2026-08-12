using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using QuotesApi.Data;
using QuotesApi.Services;

namespace Quotes.Tests.Integration;

public sealed class QuotesApiFactory(SqlServerContainerFixture sqlServer) : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset FixedUtcNow =
        new(2026, 8, 12, 10, 30, 0, TimeSpan.Zero);

    private readonly string _connectionString = CreateDatabaseConnectionString(sqlServer.ConnectionString);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(
                _connectionString,
                sqlServerOptions => sqlServerOptions.MigrationsAssembly(
                    typeof(SqlServerMigrationsMarker).Assembly.FullName)));

            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(new FixedClock(FixedUtcNow));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

        return host;
    }

    private static string CreateDatabaseConnectionString(string containerConnectionString)
    {
        var builder = new SqlConnectionStringBuilder(containerConnectionString)
        {
            InitialCatalog = $"QuotesApiIntegration_{Guid.NewGuid():N}"
        };

        return builder.ConnectionString;
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}

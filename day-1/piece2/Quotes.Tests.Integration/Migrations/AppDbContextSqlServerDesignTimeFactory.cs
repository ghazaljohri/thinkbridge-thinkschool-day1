using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using QuotesApi.Data;

namespace Quotes.Tests.Integration.Migrations;

// Used only by `dotnet ef migrations add` to scaffold SQL Server migrations for
// AppDbContext into this project. The connection string is never used to connect;
// Testcontainers supplies the real one at test run time via QuotesApiFactory.
public sealed class AppDbContextSqlServerDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=QuotesApiDesignTime;Trusted_Connection=True;TrustServerCertificate=True;",
            sqlServerOptions => sqlServerOptions.MigrationsAssembly(
                typeof(SqlServerMigrationsMarker).Assembly.FullName));

        return new AppDbContext(optionsBuilder.Options);
    }
}

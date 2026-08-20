using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;
using Xunit.Abstractions;

namespace Quotes.Tests.Unit;

public sealed class QueryTranslationTests(ITestOutputHelper output) : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public async Task FullEntityQuery_LoggedSql_SelectsEveryColumn()
    {
        using var context = _fixture.CreateLoggingContext(out var log);
        await Seed(context);

        await context.Quotes.ToListAsync();

        var selectLine = log.Single(l => l.Contains("SELECT", StringComparison.Ordinal));
        output.WriteLine(selectLine);
        selectLine.Should().Contain("\"Id\"")
            .And.Contain("\"Author\"")
            .And.Contain("\"Text\"")
            .And.Contain("\"IsDeleted\"")
            .And.Contain("\"CreatedAtUtc\"");
    }

    [Fact]
    public async Task ProjectedQuery_LoggedSql_SelectsOnlyRequestedColumns()
    {
        using var context = _fixture.CreateLoggingContext(out var log);
        await Seed(context);

        // Rewritten from "pull the whole entity" to a projection - only the
        // two columns the caller actually needs travel over the wire.
        await context.Quotes
            .Select(q => new QuoteSummaryDto(q.Id, q.Author))
            .ToListAsync();

        var selectLine = log.Single(l => l.Contains("SELECT", StringComparison.Ordinal));
        output.WriteLine(selectLine);
        selectLine.Should().Contain("\"Id\"").And.Contain("\"Author\"");
        selectLine.Should().NotContain("\"Text\"");
        selectLine.Should().NotContain("\"IsDeleted\"");
        selectLine.Should().NotContain("\"CreatedAtUtc\"");
    }

    [Fact]
    public async Task FilterAfterAsEnumerable_RunsClientSide_NotInGeneratedSql()
    {
        // The mistake: AsEnumerable() mid-chain silently switches from
        // IQueryable (translated to SQL) to IEnumerable (LINQ-to-Objects).
        // The Where() after it still compiles and still "works" - it just
        // runs in memory after the ENTIRE table has already been pulled down.
        using var context = _fixture.CreateLoggingContext(out var log);
        await Seed(context);

        var buggyResults = context.Quotes
            .AsEnumerable()
            .Where(q => q.Author == "Author 1")
            .ToList();

        var loggedSql = log.Single(l => l.Contains("SELECT", StringComparison.Ordinal));
        output.WriteLine(loggedSql);

        // The catch: the logged SQL has no WHERE clause at all, even though
        // the code has a .Where(...) - proof the filter never reached the
        // database and ran client-side instead.
        loggedSql.Should().NotContain("WHERE");
        buggyResults.Should().ContainSingle(q => q.Author == "Author 1");
    }

    [Fact]
    public async Task FilterBeforeMaterializing_TranslatesToSql_HasWhereClause()
    {
        // The fix: keep the query as IQueryable until the filter is applied,
        // so it becomes a real WHERE clause instead of an in-memory scan.
        using var context = _fixture.CreateLoggingContext(out var log);
        await Seed(context);

        var correctResults = await context.Quotes
            .Where(q => q.Author == "Author 1")
            .ToListAsync();

        var loggedSql = log.Single(l => l.Contains("SELECT", StringComparison.Ordinal));
        output.WriteLine(loggedSql);

        loggedSql.Should().Contain("WHERE");
        correctResults.Should().ContainSingle(q => q.Author == "Author 1");
    }

    private static async Task Seed(AppDbContext context)
    {
        var now = DateTimeOffset.UtcNow;
        context.Quotes.AddRange(
            Quote.Create("Author 1", "First quote", now),
            Quote.Create("Author 2", "Second quote", now.AddMinutes(1)),
            Quote.Create("Author 3", "Third quote", now.AddMinutes(2)));
        await context.SaveChangesAsync();
    }

    public void Dispose() => _fixture.Dispose();

    private sealed record QuoteSummaryDto(int Id, string Author);

    private sealed class SqliteConnectionFixture : IDisposable
    {
        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;

        public SqliteConnectionFixture()
        {
            _connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
            _connection.Open();
            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        public AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;
            return new AppDbContext(options);
        }

        // Dev-only pattern: LogTo captures every generated SQL statement,
        // EnableSensitiveDataLogging() includes actual parameter values in
        // that log instead of just placeholders - never enabled outside
        // local development since it can leak real data into logs.
        public AppDbContext CreateLoggingContext(out List<string> log)
        {
            var captured = new List<string>();
            log = captured;
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .EnableSensitiveDataLogging()
                .LogTo(message => captured.Add(message), Microsoft.Extensions.Logging.LogLevel.Information)
                .Options;
            return new AppDbContext(options);
        }

        public void Dispose() => _connection.Dispose();
    }
}

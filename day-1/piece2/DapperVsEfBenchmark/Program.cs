using System.Diagnostics;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;

const int AuthorCount = 500;
const int QuotesPerAuthor = 20;
const int Iterations = 30;

// Same CTE the Day 11 fix uses for GET /api/authors/summary - MAX(CreatedAtUtc)
// grouped by Author, joined back to Quotes to pick the row that matched. Both
// versions below run this exact string, so the only thing being measured is
// the execution path (EF's DbContext/SqlQueryRaw pipeline vs a bare ADO
// connection through Dapper), not a difference in the SQL itself.
const string Sql = """
    WITH AuthorCounts AS (
        SELECT Author, COUNT(*) AS QuoteCount, MAX(CreatedAtUtc) AS MaxCreatedAtUtc
        FROM Quotes
        WHERE NOT IsDeleted
        GROUP BY Author
    )
    SELECT c.Author AS Author, c.QuoteCount AS QuoteCount, q.Text AS MostRecentQuoteText
    FROM AuthorCounts c
    JOIN Quotes q ON q.Author = c.Author AND q.CreatedAtUtc = c.MaxCreatedAtUtc AND NOT q.IsDeleted
    """;

var dbPath = Path.Combine(Path.GetTempPath(), $"dapper-vs-ef-{Guid.NewGuid():N}.db");
var connectionString = $"Data Source={dbPath}";

Console.WriteLine($"Seeding {AuthorCount:N0} authors x {QuotesPerAuthor} quotes into {dbPath}...");
await SeedAsync(connectionString, AuthorCount, QuotesPerAuthor);

Console.WriteLine();
Console.WriteLine("Warming up (JIT, query plan cache, file cache)...");
await ReadWithEfAsync(connectionString);
await ReadWithDapperAsync(connectionString);

var ef = new List<TimeSpan>();
var dapper = new List<TimeSpan>();

for (var i = 0; i < Iterations; i++)
{
    ef.Add(await MeasureAsync(() => ReadWithEfAsync(connectionString)));
    dapper.Add(await MeasureAsync(() => ReadWithDapperAsync(connectionString)));
}

Report("EF Core (SqlQueryRaw<T>, same SQL)", ef);
Report("Dapper (QueryAsync<T>, same SQL)", dapper);

Console.WriteLine();
Console.WriteLine("Rule: don't reach for Dapper by default. Reach for it only once a read path");
Console.WriteLine("has already been pushed to raw SQL (SqlQueryRaw/FromSqlRaw) because EF's LINQ");
Console.WriteLine("translator can't express it, AND that path is hot enough that the remaining");
Console.WriteLine("per-call DbContext/materialization overhead shows up in a profile. Below that");
Console.WriteLine("bar, EF's tracking, migrations, and change-detection are worth more than the");
Console.WriteLine("saved milliseconds - see WHY.md for the numbers behind this.");

File.Delete(dbPath);

static async Task<TimeSpan> MeasureAsync(Func<Task<int>> action)
{
    var sw = Stopwatch.StartNew();
    var count = await action();
    sw.Stop();

    if (count != AuthorCount)
        throw new InvalidOperationException($"Expected {AuthorCount} authors, got {count}");

    return sw.Elapsed;
}

static void Report(string label, List<TimeSpan> results)
{
    var avgMs = results.Average(r => r.TotalMilliseconds);
    var minMs = results.Min(r => r.TotalMilliseconds);
    Console.WriteLine();
    Console.WriteLine($"{label}: {results.Count} runs");
    Console.WriteLine($"  average: {avgMs,8:F3} ms   best: {minMs,8:F3} ms");
}

static async Task SeedAsync(string connectionString, int authorCount, int quotesPerAuthor)
{
    var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
    await using var context = new AppDbContext(options);
    await context.Database.EnsureCreatedAsync();

    context.ChangeTracker.AutoDetectChangesEnabled = false;
    var now = DateTimeOffset.UtcNow;
    for (var a = 0; a < authorCount; a++)
    {
        var author = $"Author {a}";
        for (var q = 0; q < quotesPerAuthor; q++)
            context.Quotes.Add(Quote.Create(author, $"Quote {q} from {author}", now.AddSeconds(a * quotesPerAuthor + q)));

        if (a % 50 == 49)
            await context.SaveChangesAsync();
    }

    await context.SaveChangesAsync();
}

static async Task<int> ReadWithEfAsync(string connectionString)
{
    var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
    await using var context = new AppDbContext(options);

    var summaries = await context.Database.SqlQueryRaw<AuthorSummaryRow>(Sql).ToListAsync();
    return summaries.Count;
}

static async Task<int> ReadWithDapperAsync(string connectionString)
{
    await using var connection = new SqliteConnection(connectionString);
    await connection.OpenAsync();

    var summaries = await connection.QueryAsync<AuthorSummaryRow>(Sql);
    return summaries.AsList().Count;
}

// QuoteCount is long, not int: SQLite has no separate 32-bit integer storage
// class, so Microsoft.Data.Sqlite always returns COUNT(*) as Int64 - EF's
// SqlQueryRaw narrows that silently, but Dapper's constructor-matching
// materializer requires the property type to match the column type exactly.
public sealed record AuthorSummaryRow(string Author, long QuoteCount, string? MostRecentQuoteText);

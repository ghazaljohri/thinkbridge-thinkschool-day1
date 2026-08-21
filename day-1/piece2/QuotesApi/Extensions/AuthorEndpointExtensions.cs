using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;

namespace QuotesApi.Extensions;

public static class AuthorEndpointExtensions
{
    public static void MapAuthorEndpoints(this WebApplication app)
    {
        // Fixed version, for the Day 11 profiling exercise: ONE query via
        // raw SQL instead of EF's LINQ translator (which can't express
        // "first per group" cleanly on SQLite, and separately refuses to
        // order by anything derived from a DateTimeOffset column). Uses
        // MAX(CreatedAtUtc) + a join back to Quotes rather than a window
        // function - the IX_Quotes_Author_CreatedAtUtc composite index
        // satisfies both the GROUP BY and the join as index scans/seeks with
        // no temp B-tree sort at all (checked via EXPLAIN QUERY PLAN), where
        // a ROW_NUMBER() OVER (PARTITION BY ... ORDER BY ...) formulation
        // still forced one.
        app.MapGet("/api/authors/summary", async (
            AppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var summaries = await db.Database.SqlQueryRaw<AuthorSummary>("""
                WITH AuthorCounts AS (
                    SELECT Author, COUNT(*) AS QuoteCount, MAX(CreatedAtUtc) AS MaxCreatedAtUtc
                    FROM Quotes
                    WHERE NOT IsDeleted
                    GROUP BY Author
                )
                SELECT c.Author AS Author, c.QuoteCount AS QuoteCount, q.Text AS MostRecentQuoteText
                FROM AuthorCounts c
                JOIN Quotes q ON q.Author = c.Author AND q.CreatedAtUtc = c.MaxCreatedAtUtc AND NOT q.IsDeleted
                """).ToListAsync(cancellationToken);

            return Results.Ok(summaries);
        });

        // Deliberately slow, kept for the Day 11 before/after comparison: an
        // N+1 over authors -> quotes. Fetches the distinct author list, then
        // issues two MORE round trips per author (a count and a
        // most-recent-quote lookup) instead of the single query above.
        app.MapGet("/api/authors/summary-slow", async (
            AppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var authors = await db.Quotes
                .Where(q => !q.IsDeleted)
                .Select(q => q.Author)
                .Distinct()
                .ToListAsync(cancellationToken);

            var summaries = new List<AuthorSummary>(authors.Count);
            foreach (var author in authors)
            {
                var count = await db.Quotes
                    .CountAsync(q => q.Author == author && !q.IsDeleted, cancellationToken);

                // SQLite's EF Core provider refuses to translate ORDER BY on
                // anything derived from a DateTimeOffset column - not just
                // the raw property, but .UtcTicks and .UtcDateTime too.
                // Materializing this author's (small) quote list first and
                // ordering in memory sidesteps it without changing the round
                // trip count that's actually being profiled here.
                var authorQuotes = await db.Quotes
                    .Where(q => q.Author == author && !q.IsDeleted)
                    .Select(q => new { q.Text, q.CreatedAtUtc })
                    .ToListAsync(cancellationToken);
                var mostRecentText = authorQuotes
                    .OrderByDescending(q => q.CreatedAtUtc)
                    .Select(q => q.Text)
                    .FirstOrDefault();

                summaries.Add(new AuthorSummary(author, count, mostRecentText));
            }

            return Results.Ok(summaries);
        });
    }

    public sealed record AuthorSummary(string Author, int QuoteCount, string? MostRecentQuoteText);
}

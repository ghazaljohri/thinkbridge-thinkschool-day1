using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;

namespace QuotesApi.Extensions;

public static class AuthorEndpointExtensions
{
    // Deliberately slow, for the Day 11 profiling exercise: an N+1 over
    // authors -> quotes. Fetches the distinct author list, then issues two
    // MORE round trips per author (a count and a most-recent-quote lookup)
    // instead of the single grouped query from sql/author-stats-cte.sql.
    public static void MapAuthorEndpoints(this WebApplication app)
    {
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

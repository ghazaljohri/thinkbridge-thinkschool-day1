using QuotesApi.Models;
using QuotesApi.Repositories;

namespace QuotesApi.Extensions;

public static class QuoteEndpointExtensions
{
    public static void MapQuoteEndpoints(this WebApplication app)
    {
        app.MapGet("/api/quotes", async (
            int? page,
            int? size,
            IQuoteRepository repository,
            CancellationToken cancellationToken) =>
        {
            var currentPage = page ?? 1;
            var pageSize = size ?? 10;

            if (currentPage < 1 || pageSize < 1 || pageSize > 100)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["page"] = currentPage < 1
                        ? ["Page must be at least 1."]
                        : [],
                    ["size"] = pageSize < 1 || pageSize > 100
                        ? ["Size must be between 1 and 100."]
                        : []
                });
            }

            var (items, total) = await repository.GetPagedAsync(
                currentPage, pageSize, cancellationToken);

            return Results.Ok(new
            {
                page = currentPage,
                size = pageSize,
                total,
                items
            });
        });

        app.MapPost("/api/quotes", async (
            Quote quote,
            IQuoteRepository repository,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            var errors = new Dictionary<string, string[]>();

            if (string.IsNullOrWhiteSpace(quote.Author))
                errors["author"] = ["Author is required."];

            if (string.IsNullOrWhiteSpace(quote.Text))
                errors["text"] = ["Text is required."];

            if (quote.Author?.Length > 200)
                errors["author"] = ["Author must be 200 characters or fewer."];

            if (quote.Text?.Length > 1000)
                errors["text"] = ["Text must be 1000 characters or fewer."];

            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var created = await repository.AddAsync(
                quote, cancellationToken);

            logger.LogInformation(
                "Created quote {QuoteId} by {Author}",
                created.Id,
                created.Author);

            return Results.Created(
                $"/api/quotes/{created.Id}", created);
        });

        app.MapGet("/api/quotes/{id:int}", async (
            int id,
            IQuoteRepository repository,
            CancellationToken cancellationToken) =>
        {
            var quote = await repository.GetByIdAsync(
                id, cancellationToken);

            return quote is null
                ? Results.NotFound()
                : Results.Ok(quote);
        });

        app.MapDelete("/api/quotes/{id:int}", async (
            int id,
            IQuoteRepository repository,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            var deleted = await repository.DeleteAsync(
                id, cancellationToken);

            if (!deleted)
                return Results.NotFound();

            logger.LogInformation("Deleted quote {QuoteId}", id);

            return Results.NoContent();
        });
    }
}

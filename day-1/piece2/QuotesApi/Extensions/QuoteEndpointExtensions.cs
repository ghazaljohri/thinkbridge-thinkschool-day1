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
            CreateQuoteRequest request,
            IQuoteRepository repository,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var quote = Quote.Create(
                    request.Author,
                    request.Text);

                var created = await repository.AddAsync(
                    quote,
                    cancellationToken);

                logger.LogInformation(
                    "Created quote {QuoteId} by {Author}",
                    created.Id,
                    created.Author);

                return Results.Created(
                    $"/api/quotes/{created.Id}",
                    created);
            }
            catch (ArgumentException ex)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        [ex.ParamName ?? "request"] = [ex.Message]
                    });
            }
        }).RequireAuthorization();

        app.MapGet("/api/quotes/{id:int}", async (
            int id,
            IQuoteRepository repository,
            CancellationToken cancellationToken) =>
        {
            var quote = await repository.GetByIdAsync(
                id,
                cancellationToken);

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
                id,
                cancellationToken);

            if (!deleted)
                return Results.NotFound();

            logger.LogInformation(
                "Deleted quote {QuoteId}",
                id);

            return Results.NoContent();
        }).RequireAuthorization();
    }

    public sealed record CreateQuoteRequest(
        string Author,
        string Text);
}

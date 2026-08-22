using QuotesApi.Models.Collections;
using QuotesApi.Queries;
using QuotesApi.Repositories;
using QuotesApi.Services;

namespace QuotesApi.Extensions;

public static class CollectionEndpointExtensions
{
    public static void MapCollectionEndpoints(this WebApplication app)
    {
        // Read side: goes straight to ICollectionQueries, never touches
        // ICollectionRepository or the Collection aggregate.
        app.MapGet("/api/collections", async (
            int ownerId,
            ICollectionQueries queries,
            CancellationToken cancellationToken) =>
        {
            var summaries = await queries.GetSummariesByOwnerAsync(ownerId, cancellationToken);
            return Results.Ok(summaries);
        });

        // Write side: goes through the Collection aggregate so its
        // invariants (name length, non-empty name) are enforced.
        app.MapPost("/api/collections", async (
            CollectionRequest request,
            ICollectionRepository repository,
            CancellationToken cancellationToken) =>
        {
            var collection = new Collection(
                request.Name,
                request.OwnerId);

            var created = await repository.AddAsync(
                collection,
                cancellationToken);

            return Results.Created(
                $"/api/collections/{created.Id}",
                created);
        });

        app.MapGet("/api/collections/{id:int}", async (
            int id,
            ICollectionQueries queries,
            CancellationToken cancellationToken) =>
        {
            var detail = await queries.GetDetailAsync(id, cancellationToken);

            if (detail is null)
                return Results.NotFound();

            var items = detail.Items
                .Select(item => new CollectionItemResponse(item.QuoteId, item.Author, item.Text, item.AddedAt))
                .ToList();

            return Results.Ok(new CollectionResponse(detail.Id, detail.Name, detail.OwnerId, items));
        });

        app.MapDelete("/api/collections/{id:int}", async (
            int id,
            ICollectionRepository repository,
            CancellationToken cancellationToken) =>
        {
            var deleted = await repository.DeleteAsync(
                id,
                cancellationToken);

            return deleted
                ? Results.NoContent()
                : Results.NotFound();
        });

        app.MapPost("/api/collections/{id:int}/items", async (
            int id,
            AddCollectionItemRequest request,
            ICollectionRepository repository,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            var collection = await repository.GetByIdAsync(
                id,
                cancellationToken);

            if (collection is null)
                return Results.NotFound();

            collection.AddItem(request.QuoteId, clock.UtcNow);

            await repository.SaveAsync(
                collection,
                cancellationToken);

            return Results.NoContent();
        });

        app.MapDelete("/api/collections/{id:int}/items/{quoteId:int}", async (
            int id,
            int quoteId,
            ICollectionRepository repository,
            CancellationToken cancellationToken) =>
        {
            var collection = await repository.GetByIdAsync(
                id,
                cancellationToken);

            if (collection is null)
                return Results.NotFound();

            collection.RemoveItem(quoteId);

            await repository.SaveAsync(
                collection,
                cancellationToken);

            return Results.NoContent();
        });
    }

    public sealed record CollectionRequest(
        string Name,
        int OwnerId);

    public sealed record AddCollectionItemRequest(
        int QuoteId);

    public sealed record CollectionResponse(
        int Id,
        string Name,
        int OwnerId,
        IReadOnlyList<CollectionItemResponse> Items);

    public sealed record CollectionItemResponse(
        int QuoteId,
        string Author,
        string Text,
        DateTime AddedAt);
}

using QuotesApi.Models.Collections;
using QuotesApi.Repositories;
using QuotesApi.Services;

namespace QuotesApi.Extensions;

public static class CollectionEndpointExtensions
{
    public static void MapCollectionEndpoints(this WebApplication app)
    {
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
            ICollectionRepository repository,
            IQuoteRepository quoteRepository,
            CancellationToken cancellationToken) =>
        {
            var collection = await repository.GetByIdAsync(
                id,
                cancellationToken);

            if (collection is null)
                return Results.NotFound();

            var quoteIds = collection.Items.Select(i => i.QuoteId).ToList();
            var quotesById = (await quoteRepository.GetByIdsAsync(quoteIds, cancellationToken))
                .ToDictionary(q => q.Id);

            var items = collection.Items
                .Select(item =>
                {
                    quotesById.TryGetValue(item.QuoteId, out var quote);
                    return new CollectionItemResponse(item.QuoteId, quote?.Author, quote?.Text, item.AddedAt);
                })
                .ToList();

            return Results.Ok(new CollectionResponse(collection.Id, collection.Name, collection.OwnerId, items));
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
        string? Author,
        string? Text,
        DateTime AddedAt);
}

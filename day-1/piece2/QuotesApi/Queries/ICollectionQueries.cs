namespace QuotesApi.Queries;

public interface ICollectionQueries
{
    Task<CollectionDetail?> GetDetailAsync(
        int collectionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CollectionSummary>> GetSummariesByOwnerAsync(
        int ownerId,
        CancellationToken cancellationToken);
}

// Denormalized, screen-shaped for a "my collections" list: item count and
// last-added timestamp instead of the full item list the write-side
// Collection aggregate carries.
public sealed record CollectionSummary(
    int Id,
    string Name,
    int OwnerId,
    int ItemCount,
    DateTime? LastAddedAt);

// Denormalized, screen-shaped for a collection detail view: each item
// already carries the quote's Author/Text, so the caller never has to
// resolve quotes separately to render the list.
public sealed record CollectionDetail(
    int Id,
    string Name,
    int OwnerId,
    IReadOnlyList<CollectionDetailItem> Items);

public sealed record CollectionDetailItem(
    int QuoteId,
    string Author,
    string Text,
    DateTime AddedAt);

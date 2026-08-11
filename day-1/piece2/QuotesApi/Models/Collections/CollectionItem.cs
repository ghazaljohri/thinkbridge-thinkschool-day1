namespace QuotesApi.Models.Collections;

public sealed class CollectionItem
{
    public int CollectionId { get; private set; }
    public int QuoteId { get; private set; }
    public DateTime AddedAt { get; private set; }

    private CollectionItem() { }

    public CollectionItem(int quoteId, DateTimeOffset addedAt)
    {
        if (quoteId <= 0)
            throw new ArgumentException(
                "QuoteId must be greater than zero.",
                nameof(quoteId));

        QuoteId = quoteId;
        AddedAt = addedAt.UtcDateTime;
    }
}

namespace QuotesApi.Models.Collections;

public class Collection
{
    private readonly List<CollectionItem> _items = [];
    public int Id { get; private set; }
    public string Name { get; private set; } = "";
    public int OwnerId { get; private set; }

    public IReadOnlyCollection<CollectionItem> Items => _items.AsReadOnly();
    private Collection() { }

    public Collection(string name, int ownerId)
    {
        ValidateName(name);

        if (ownerId <= 0)
            throw new ArgumentException("OwnerId must be greater than zero.", nameof(ownerId));

        Name = name.Trim();
        OwnerId = ownerId;
    }

    public void AddItem(int quoteId, DateTimeOffset addedAt)
    {
        if (quoteId <= 0)
            throw new ArgumentException("QuoteId must be greater than zero.", nameof(quoteId));

        if (_items.Count >= 50)
            throw new InvalidOperationException("A collection cannot contain more than 50 items.");

        if (_items.Any(x => x.QuoteId == quoteId))
            throw new InvalidOperationException("The quote is already in the collection.");

        _items.Add(new CollectionItem(quoteId, addedAt));
    }

    public void RemoveItem(int quoteId)
    {
        var item = _items.FirstOrDefault(x => x.QuoteId == quoteId);

        if (item is null)
            throw new InvalidOperationException("The quote is not in the collection.");

        _items.Remove(item);
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Collection name is required.", nameof(name));

        var length = name.Trim().Length;

        if (length < 3 || length > 80)
            throw new ArgumentException(
                "Collection name must be between 3 and 80 characters.",
                nameof(name));
    }
}

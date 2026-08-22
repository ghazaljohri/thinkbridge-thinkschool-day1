using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;

namespace QuotesApi.Queries;

// Read side of the Collections CQRS-lite split. Queries here go straight
// from the database to the exact denormalized shape a screen needs, instead
// of loading the normalized Collection aggregate (Repositories/) and
// reassembling a response from it. Writes still go through that aggregate
// so its invariants - the 50-item cap, the duplicate-item check, name
// length - stay enforced in one place; this type never mutates anything.
public class CollectionQueries : ICollectionQueries
{
    private readonly AppDbContext _db;

    public CollectionQueries(AppDbContext db)
    {
        _db = db;
    }

    public async Task<CollectionDetail?> GetDetailAsync(
        int collectionId,
        CancellationToken cancellationToken)
    {
        var collection = await _db.Collections
            .AsNoTracking()
            .Where(c => c.Id == collectionId)
            .Select(c => new { c.Id, c.Name, c.OwnerId })
            .FirstOrDefaultAsync(cancellationToken);

        if (collection is null)
            return null;

        var items = await _db.CollectionItems
            .AsNoTracking()
            .Where(i => i.CollectionId == collectionId)
            .Join(
                _db.Quotes.AsNoTracking(),
                i => i.QuoteId,
                q => q.Id,
                (i, q) => new CollectionDetailItem(q.Id, q.Author, q.Text, i.AddedAt))
            .ToListAsync(cancellationToken);

        return new CollectionDetail(collection.Id, collection.Name, collection.OwnerId, items);
    }

    public async Task<IReadOnlyList<CollectionSummary>> GetSummariesByOwnerAsync(
        int ownerId,
        CancellationToken cancellationToken)
    {
        return await _db.Collections
            .AsNoTracking()
            .Where(c => c.OwnerId == ownerId)
            .Select(c => new CollectionSummary(
                c.Id,
                c.Name,
                c.OwnerId,
                c.Items.Count,
                c.Items.Select(i => (DateTime?)i.AddedAt).Max()))
            .ToListAsync(cancellationToken);
    }
}

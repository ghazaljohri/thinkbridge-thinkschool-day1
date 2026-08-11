using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models.Collections;

namespace QuotesApi.Repositories;

public class CollectionRepository : ICollectionRepository
{
    private readonly AppDbContext _db;

    public CollectionRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Collection> AddAsync(
        Collection collection,
        CancellationToken cancellationToken)
    {
        _db.Collections.Add(collection);
        await _db.SaveChangesAsync(cancellationToken);
        return collection;
    }

    public async Task<Collection?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await _db.Collections
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var collection = await _db.Collections
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (collection is null)
            return false;

        _db.Collections.Remove(collection);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> ExistsAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _db.Collections
            .AnyAsync(c => c.Id == id, cancellationToken);
    }

    public async Task SaveAsync(
        Collection collection,
        CancellationToken cancellationToken)
    {
        await _db.SaveChangesAsync(cancellationToken);
    }
}

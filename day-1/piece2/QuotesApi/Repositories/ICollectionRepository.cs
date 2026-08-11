using QuotesApi.Models.Collections;

namespace QuotesApi.Repositories;

public interface ICollectionRepository
{
    Task<Collection> AddAsync(
        Collection collection,
        CancellationToken cancellationToken);

    Task<Collection?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(
        int id,
        CancellationToken cancellationToken);

    Task SaveAsync(
        Collection collection,
        CancellationToken cancellationToken);
}

using OrderApi.Models;

namespace OrderApi.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<Order> AddAsync(
        Order order,
        CancellationToken cancellationToken);
}

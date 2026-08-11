using OrderApi.Models;

namespace OrderApi.Services;

public interface IOrderService
{
    Task<Order?> GetOrderAsync(
        int id,
        CancellationToken cancellationToken);

    Task<Order> CreateOrderAsync(
        string customerName,
        decimal total,
        CancellationToken cancellationToken);
}

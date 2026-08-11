using OrderApi.Models;
using OrderApi.Repositories;

namespace OrderApi.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository repository,
        ILogger<OrderService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public Task<Order?> GetOrderAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _repository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<Order> CreateOrderAsync(
        string customerName,
        decimal total,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerName))
            throw new ArgumentException(
                "Customer name is required.",
                nameof(customerName));

        if (total <= 0)
            throw new ArgumentException(
                "Order total must be greater than zero.",
                nameof(total));

        var order = new Order
        {
            CustomerName = customerName.Trim(),
            Total = total,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        var created = await _repository.AddAsync(
            order,
            cancellationToken);

        _logger.LogInformation(
            "Created order {OrderId} for {CustomerName}",
            created.Id,
            created.CustomerName);

        return created;
    }
}

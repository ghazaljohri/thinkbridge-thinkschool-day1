using Microsoft.Extensions.Logging.Abstractions;
using OrderApi.Models;
using OrderApi.Repositories;
using OrderApi.Services;

namespace OrderApi.Tests.Services;

public class OrderServiceTests
{
    [Fact]
    public async Task CreateOrderAsync_CreatesValidOrder()
    {
        var repository = new FakeOrderRepository();
        var service = new OrderService(
            repository,
            NullLogger<OrderService>.Instance);

        var result = await service.CreateOrderAsync(
            "Ghazal",
            100m,
            CancellationToken.None);

        Assert.Equal("Ghazal", result.CustomerName);
        Assert.Equal(100m, result.Total);
        Assert.Equal("Pending", result.Status);
    }

    [Fact]
    public async Task CreateOrderAsync_RejectsEmptyCustomerName()
    {
        var repository = new FakeOrderRepository();
        var service = new OrderService(
            repository,
            NullLogger<OrderService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateOrderAsync(
                "",
                100m,
                CancellationToken.None));
    }

    [Fact]
    public async Task CreateOrderAsync_RejectsInvalidTotal()
    {
        var repository = new FakeOrderRepository();
        var service = new OrderService(
            repository,
            NullLogger<OrderService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateOrderAsync(
                "Ghazal",
                0m,
                CancellationToken.None));
    }

    private sealed class FakeOrderRepository : IOrderRepository
    {
        private int _nextId = 1;

        public Task<Order?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<Order?>(null);
        }

        public Task<Order> AddAsync(
            Order order,
            CancellationToken cancellationToken)
        {
            order.Id = _nextId++;
            return Task.FromResult(order);
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using OrderApi.Data;

namespace OrderApi.Tests.Integration;

public class OrderApiIntegrationTests
{
    [Fact]
    public async Task CreateOrder_ReturnsCreatedOrder()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseSqlite("Data Source=test-orders.db"));
                });
            });

        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        var response = await client.PostAsJsonAsync(
            "/api/orders",
            new
            {
                CustomerName = "Ghazal",
                Total = 250m
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();

        Assert.NotNull(order);
        Assert.Equal("Ghazal", order.CustomerName);
        Assert.Equal(250m, order.Total);
        Assert.Equal("Pending", order.Status);
    }

    private record OrderResponse(
        int Id,
        string CustomerName,
        decimal Total,
        string Status,
        DateTime CreatedAt);
}

using Microsoft.AspNetCore.Mvc;
using OrderApi.Models;
using OrderApi.Services;

namespace OrderApi.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _service;

    public OrdersController(IOrderService service)
    {
        _service = service;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Order>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var order = await _service.GetOrderAsync(
            id,
            cancellationToken);

        if (order is null)
            return NotFound();

        return Ok(order);
    }

    [HttpPost]
    public async Task<ActionResult<Order>> Create(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName))
            return BadRequest("Customer name is required.");

        if (request.Total <= 0)
            return BadRequest("Order total must be greater than zero.");

        var order = await _service.CreateOrderAsync(
            request.CustomerName,
            request.Total,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = order.Id },
            order);
    }
}

public record CreateOrderRequest(
    string CustomerName,
    decimal Total);

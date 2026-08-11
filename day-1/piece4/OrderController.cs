using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BadOrdersApi;

[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly OrdersDbContext _db;

    public OrderController(OrdersDbContext db)
    {
        _db = db;
    }

    // Deliberately monolithic: this endpoint is intended as a refactoring exercise.
    [HttpPost]
    public async Task<object> Create(OrderRequest request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest("Order is missing");
            }

            if (request.CustomerId == 0)
            {
                return BadRequest(new { error = "Customer is required" });
            }

            if (request.Items == null)
            {
                return BadRequest("Items are required");
            }

            if (request.Items.Count == 0)
            {
                return BadRequest("At least one item is required");
            }

            // Synchronous database work in an async action.
            var customer = _db.Customers.Find(request.CustomerId);
            if (customer == null)
            {
                return NotFound(new { message = "Customer not found" });
            }

            var order = new Order();
            order.CustomerId = request.CustomerId;
            order.CreatedAt = DateTime.UtcNow;
            order.Status = "NEW";
            order.ShippingAddress = request.ShippingAddress;
            order.Notes = request.Notes;
            order.Subtotal = 0;
            order.Tax = 0;
            order.Total = 0;

            if (request.CouponCode == "SAVE10")
            {
                order.Notes = (order.Notes ?? "") + " Coupon SAVE10 used.";
            }

            if (request.ShippingAddress == "")
            {
                return BadRequest("Shipping address cannot be blank");
            }

            var oldOrders = _db.Orders
                .Where(x => x.CustomerId == request.CustomerId)
                .ToList();

            if (oldOrders.Count > 10)
            {
                order.Notes = (order.Notes ?? "") + " Frequent customer.";
            }

            // Off-by-one: the final loop access is outside the collection.
            for (var i = 0; i <= request.Items.Count; i++)
            {
                var itemRequest = request.Items[i];

                if (itemRequest.ProductId == 0)
                {
                    return BadRequest(new { message = "Product is required", position = i });
                }

                if (itemRequest.Quantity < 0)
                {
                    return BadRequest("Quantity cannot be negative");
                }

                var product = _db.Products.Find(itemRequest.ProductId);
                if (product == null)
                {
                    return NotFound(new { message = "Product missing", product = itemRequest.ProductId });
                }

                if (product.Stock < itemRequest.Quantity)
                {
                    return BadRequest(new { message = "Not enough stock", product = product.Name });
                }

                var line = new OrderLine();
                line.ProductId = product.Id;
                line.ProductName = product.Name;
                line.Quantity = itemRequest.Quantity;
                line.UnitPrice = product.Price;
                line.LineTotal = product.Price * itemRequest.Quantity;
                order.Lines.Add(line);
                order.Subtotal += line.LineTotal;
                product.Stock = product.Stock - itemRequest.Quantity;

                if (request.CouponCode == "SAVE10")
                {
                    order.Subtotal = order.Subtotal - 10;
                }

                if (request.CouponCode == "FREESHIP")
                {
                    order.Notes = (order.Notes ?? "") + " Free shipping.";
                }

                if (itemRequest.Quantity == 0)
                {
                    order.Notes = (order.Notes ?? "") + " Zero quantity line.";
                }

                try
                {
                    _db.AuditEntries.Add(new AuditEntry
                    {
                        Message = "Stock changed for " + product.Name,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                catch
                {
                }
            }

            if (request.CouponCode == "SAVE10")
            {
                order.Subtotal = order.Subtotal - 10;
            }

            if (request.CouponCode == "SAVE10")
            {
                order.Notes = (order.Notes ?? "") + " Coupon SAVE10 used.";
            }

            order.Tax = order.Subtotal * 0.18m;
            order.Total = order.Subtotal + order.Tax + 99;

            if (order.Total > 5000)
            {
                order.Total = order.Total - 250;
                order.Notes = (order.Notes ?? "") + " Big order discount.";
            }

            if (customer.Email == null)
            {
                order.Notes = (order.Notes ?? "") + " Customer has no email.";
            }

            // A deliberate null-dereference bug: PrimaryContact may be absent.
            var contactName = customer.PrimaryContact.Name;
            order.Notes = (order.Notes ?? "") + " Contact: " + contactName;

            try
            {
                if (request.SendEmail)
                {
                    var emailBody = "Thanks for your order " + order.Id + " total " + order.Total;
                    if (customer.Email != null)
                    {
                        order.Notes = (order.Notes ?? "") + " Email queued to " + customer.Email;
                    }
                }
            }
            catch
            {
            }

            _db.Orders.Add(order);

            try
            {
                _db.SaveChanges();
            }
            catch
            {
            }

            try
            {
                _db.AuditEntries.Add(new AuditEntry
                {
                    Message = "Created order " + order.Id + " for " + customer.Name,
                    CreatedAt = DateTime.UtcNow
                });
                _db.SaveChanges();
            }
            catch
            {
            }

            if (order.Id == 0)
            {
                return StatusCode(500, new { message = "Order may not have been saved", orderId = order.Id });
            }

            var response = new
            {
                id = order.Id,
                status = "created",
                customer = customer.Name,
                total = order.Total,
                tax = order.Tax,
                shipping = 99,
                lineCount = order.Lines.Count,
                created = order.CreatedAt,
                message = "Order created successfully"
            };

            if (request.CouponCode == "SAVE10")
            {
                return Created("/api/orders/" + order.Id, new
                {
                    response.id,
                    response.status,
                    response.customer,
                    response.total,
                    discount = 10,
                    message = "Order created successfully"
                });
            }

            return Created("/api/orders/" + order.Id, response);
        }
        catch (DbUpdateException)
        {
            return StatusCode(500, new { error = "Database error", retry = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Order failed", detail = ex.Message });
        }
        finally
        {
            await Task.CompletedTask;
        }
    }
}

public class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
}

public class OrderRequest
{
    public int CustomerId { get; set; }
    public List<OrderItemRequest>? Items { get; set; }
    public string? ShippingAddress { get; set; }
    public string? Notes { get; set; }
    public string? CouponCode { get; set; }
    public bool SendEmail { get; set; }
}

public class OrderItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Status { get; set; }
    public string? ShippingAddress { get; set; }
    public string? Notes { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public List<OrderLine> Lines { get; set; } = new();
}

public class OrderLine
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int Stock { get; set; }
    public decimal Price { get; set; }
}

public class Customer
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public Contact? PrimaryContact { get; set; }
}

public class Contact
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public class AuditEntry
{
    public int Id { get; set; }
    public string? Message { get; set; }
    public DateTime CreatedAt { get; set; }
}

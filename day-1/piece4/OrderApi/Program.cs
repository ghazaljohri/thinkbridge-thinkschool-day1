using Microsoft.EntityFrameworkCore;
using OrderApi.Data;
using OrderApi.Extensions;
using OrderApi.Repositories;
using OrderApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=orders.db"));

builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();

var app = builder.Build();

app.UseGlobalExceptionHandling();

app.MapControllers();

app.Run();

public partial class Program { }

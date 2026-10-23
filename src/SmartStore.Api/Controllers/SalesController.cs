using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartStore.Application;
using SmartStore.Domain;
using SmartStore.Infrastructure;
namespace SmartStore.Api.Controllers;

[ApiController, Route("api/sales")]
public sealed class SalesController(StoreDbContext db) : ControllerBase
{
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => await db.SaleOrders.AsNoTracking().Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct) is { } order ? Ok(order) : NotFound();
    [HttpPost]
    public async Task<IActionResult> Create(SaleRequest r, CancellationToken ct)
    {
        if (r.Items is null || r.Items.Count == 0 || r.Items.Any(x => x is null || x.Quantity <= 0) || r.Items.Select(x => x.ProductId).Distinct().Count() != r.Items.Count) return BadRequest("Provide unique products with positive quantities.");
        var ids = r.Items.Select(x => x.ProductId).ToArray();
        var products = await db.Products.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        if (products.Count != ids.Length) return BadRequest("Unknown product.");
        var order = new SaleOrder();
        foreach (var line in r.Items)
        {
            var product = products[line.ProductId];
            product.AdjustStock(-line.Quantity, $"Sale {order.Id}");
            order.Items.Add(new SaleOrderItem { ProductId = product.Id, Quantity = line.Quantity, UnitPrice = product.Price });
        }
        db.SaleOrders.Add(order);
        // One SaveChanges = one transaction: order, stock, and movement succeed together.
        await db.SaveChangesAsync(ct);
        return Created($"/api/sales/{order.Id}", order);
    }
}

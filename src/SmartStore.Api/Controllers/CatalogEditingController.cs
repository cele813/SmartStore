using Microsoft.AspNetCore.Mvc;
using SmartStore.Application;
using SmartStore.Infrastructure;
namespace SmartStore.Api.Controllers;

[ApiController, Route("api")]
public sealed class CatalogEditingController(StoreDbContext db) : ControllerBase
{
    [HttpPut("categories/{id:guid}")]
    public async Task<IActionResult> Category(Guid id, CategoryRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return BadRequest();
        var item = await db.Categories.FindAsync([id], ct);
        if (item is null) return NotFound();
        item.Name = r.Name.Trim(); await db.SaveChangesAsync(ct); return Ok(item);
    }
    [HttpDelete("categories/{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct)
    {
        var item = await db.Categories.FindAsync([id], ct);
        if (item is null) return NotFound();
        db.Categories.Remove(item); await db.SaveChangesAsync(ct); return NoContent();
    }
    [HttpPut("suppliers/{id:guid}")]
    public async Task<IActionResult> Supplier(Guid id, SupplierRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return BadRequest();
        var item = await db.Suppliers.FindAsync([id], ct);
        if (item is null) return NotFound();
        item.Name = r.Name.Trim(); item.Email = r.Email; item.Phone = r.Phone;
        await db.SaveChangesAsync(ct); return Ok(item);
    }
    [HttpDelete("suppliers/{id:guid}")]
    public async Task<IActionResult> DeleteSupplier(Guid id, CancellationToken ct)
    {
        var item = await db.Suppliers.FindAsync([id], ct);
        if (item is null) return NotFound();
        db.Suppliers.Remove(item); await db.SaveChangesAsync(ct); return NoContent();
    }
    [HttpPut("products/{id:guid}")]
    public async Task<IActionResult> Product(Guid id, ProductRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Sku) || string.IsNullOrWhiteSpace(r.Name)) return BadRequest();
        var item = await db.Products.FindAsync([id], ct);
        if (item is null) return NotFound();
        item.Sku = r.Sku.Trim().ToUpperInvariant(); item.Name = r.Name.Trim(); item.Price = r.Price;
        item.CategoryId = r.CategoryId; item.SupplierId = r.SupplierId;
        await db.SaveChangesAsync(ct); return Ok(item);
    }
    [HttpDelete("products/{id:guid}")]
    public async Task<IActionResult> DeleteProduct(Guid id, CancellationToken ct)
    {
        var item = await db.Products.FindAsync([id], ct);
        if (item is null) return NotFound();
        db.Products.Remove(item); await db.SaveChangesAsync(ct); return NoContent();
    }
}

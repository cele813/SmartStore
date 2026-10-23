using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartStore.Application;
using SmartStore.Domain;
using SmartStore.Infrastructure;
namespace SmartStore.Api.Controllers;

[ApiController, Route("api/categories")]
public sealed class CategoriesController(StoreDbContext db) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(await db.Categories.AsNoTracking().OrderBy(x => x.Name).Take(200).ToListAsync(ct));
    [HttpPost]
    public async Task<IActionResult> Create(CategoryRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return BadRequest();
        var entity = new Category { Name = r.Name.Trim() };
        db.Categories.Add(entity); await db.SaveChangesAsync(ct);
        return Created($"/api/categories/{entity.Id}", entity);
    }
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => await db.Categories.FindAsync([id], ct) is { } item ? Ok(item) : NotFound();
}
[ApiController, Route("api/suppliers")]
public sealed class SuppliersController(StoreDbContext db) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(await db.Suppliers.AsNoTracking().OrderBy(x => x.Name).Take(200).ToListAsync(ct));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => await db.Suppliers.FindAsync([id], ct) is { } item ? Ok(item) : NotFound();
    [HttpPost]
    public async Task<IActionResult> Create(SupplierRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return BadRequest();
        var entity = new Supplier { Name = r.Name.Trim(), Email = r.Email, Phone = r.Phone };
        db.Suppliers.Add(entity); await db.SaveChangesAsync(ct);
        return Created($"/api/suppliers/{entity.Id}", entity);
    }
}
[ApiController, Route("api/products")]
public sealed class ProductsController(StoreDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, int page = 1, int pageSize = 20)
    {
        if (page < 1 || page > 1000000 || pageSize < 1 || pageSize > 100) return BadRequest();
        var query = db.Products.AsNoTracking();
        return Ok(new { page, pageSize, total = await query.CountAsync(ct), items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct) });
    }
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) is { } item ? Ok(item) : NotFound();
    [HttpPost]
    public async Task<IActionResult> Create(ProductRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Sku) || string.IsNullOrWhiteSpace(r.Name)) return BadRequest();
        if (!await db.Categories.AnyAsync(x => x.Id == r.CategoryId, ct) || (r.SupplierId.HasValue && !await db.Suppliers.AnyAsync(x => x.Id == r.SupplierId, ct))) return BadRequest("Invalid category or supplier.");
        var entity = new Product { Sku = r.Sku.Trim().ToUpperInvariant(), Name = r.Name.Trim(), Price = r.Price, CategoryId = r.CategoryId, SupplierId = r.SupplierId };
        db.Products.Add(entity); await db.SaveChangesAsync(ct);
        return Created($"/api/products/{entity.Id}", entity);
    }
    [HttpPost("{id:guid}/stock-adjustments")]
    public async Task<IActionResult> Adjust(Guid id, StockRequest r, CancellationToken ct)
    {
        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (product is null) return NotFound();
        product.AdjustStock(r.Quantity, r.Reason); await db.SaveChangesAsync(ct);
        return Ok(new { product.Id, product.Stock });
    }
    [HttpGet("{id:guid}/stock-movements")] public async Task<IActionResult> Movements(Guid id, CancellationToken ct) => Ok(await db.StockMovements.AsNoTracking().Where(x => x.ProductId == id).OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct));
}

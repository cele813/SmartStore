using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using SmartStore.Application;
using SmartStore.Domain;
using SmartStore.Infrastructure;
using Xunit;
namespace SmartStore.Tests;

public class PostgresTests
{
    [Fact, Trait("Category", "Integration")]
    public async Task Sale_is_atomic_and_concurrent_stock_updates_are_rejected()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Store")
            ?? throw new InvalidOperationException("Set ConnectionStrings__Store to a dedicated test PostgreSQL database.");
        var options = new DbContextOptionsBuilder<StoreDbContext>().UseNpgsql(connection).Options;
        await using var db = new StoreDbContext(options);
        await db.Database.MigrateAsync();
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var categoryResponse = await client.PostAsJsonAsync("/api/categories", new CategoryRequest($"Test-{Guid.NewGuid():N}"));
        Assert.Equal(HttpStatusCode.Created, categoryResponse.StatusCode);
        var category = (await categoryResponse.Content.ReadFromJsonAsync<Category>())!;
        var request = new ProductRequest($"TEST-{Guid.NewGuid():N}", "Coffee", 25000, category.Id, null);
        var productResponse = await client.PostAsJsonAsync("/api/products", request);
        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        var product = (await productResponse.Content.ReadFromJsonAsync<Product>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/products", request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/products/{product.Id}/stock-adjustments", new StockRequest(10, "Receipt"))).StatusCode);
        var saleResponse = await client.PostAsJsonAsync("/api/sales", new SaleRequest([new(product.Id, 3)]));
        Assert.Equal(HttpStatusCode.Created, saleResponse.StatusCode);
        var order = (await saleResponse.Content.ReadFromJsonAsync<SaleOrder>())!;
        Assert.Equal(75000, order.Total);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/sales", new SaleRequest([new(product.Id, 8)]))).StatusCode);
        Assert.Equal(7, (await db.Products.AsNoTracking().SingleAsync(x => x.Id == product.Id)).Stock);
        Assert.Equal(2, await db.StockMovements.CountAsync(x => x.ProductId == product.Id));
        Assert.Single(await db.SaleOrderItems.Where(x => x.ProductId == product.Id).ToListAsync());
        await using var other = new StoreDbContext(options);
        var first = await db.Products.SingleAsync(x => x.Id == product.Id);
        var second = await other.Products.SingleAsync(x => x.Id == product.Id);
        first.AdjustStock(-1, "Concurrent A"); second.AdjustStock(-1, "Concurrent B");
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => other.SaveChangesAsync());
        await using var verification = new StoreDbContext(options);
        Assert.Equal(6, (await verification.Products.SingleAsync(x => x.Id == product.Id)).Stock);
        Assert.Equal(3, await verification.StockMovements.CountAsync(x => x.ProductId == product.Id));
    }
}

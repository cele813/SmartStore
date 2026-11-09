using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SmartStore.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using SmartStore.Application;
using SmartStore.Domain;
using SmartStore.Infrastructure;
using Xunit;
namespace SmartStore.Tests;

[Collection("PostgreSQL")]
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
        var admin = await TestAuth.Register(client);
        using var staff = factory.CreateClient();
        await TestAuth.Register(staff);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/categories", new CategoryRequest("Forbidden"))).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync("Admin"))
                Assert.True((await roles.CreateAsync(new IdentityRole("Admin"))).Succeeded);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<StoreUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync(admin.Email))!, "Admin")).Succeeded);
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(admin.Email, admin.Password))).StatusCode);
        await TestAuth.RefreshCsrf(client);
        Assert.Equal("Admin", (await client.GetFromJsonAsync<UserResponse>("/api/auth/me"))!.Role);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/accounts")).StatusCode);
        var accounts = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/accounts");
        Assert.Contains(accounts.GetProperty("items").EnumerateArray(), account => account.GetProperty("email").GetString() == admin.Email);
        var newAccount = new RegisterRequest("New Staff", $"new-{Guid.NewGuid():N}@example.com", "TestPass123!");
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/accounts", newAccount)).StatusCode);
        var created = await client.PostAsJsonAsync("/api/accounts", newAccount);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var account = (await created.Content.ReadFromJsonAsync<UserResponse>())!;
        Assert.Equal("Staff", account.Role);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/accounts", newAccount)).StatusCode);
        var accountPath = $"/api/accounts/{account.Id}";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(accountPath, new
        {
            displayName = "Renamed Staff", email = "changed@example.com", role = "Admin"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(accountPath, new { displayName = "Renamed Staff" })).StatusCode);
        var renamed = (await client.GetFromJsonAsync<UserResponse>(accountPath))!;
        Assert.Equal(("Renamed Staff", newAccount.Email, "Staff"), (renamed.DisplayName, renamed.Email, renamed.Role));

        using var newStaff = factory.CreateClient();
        await TestAuth.RefreshCsrf(newStaff);
        Assert.Equal(HttpStatusCode.OK, (await newStaff.PostAsJsonAsync("/api/auth/login", new LoginRequest(newAccount.Email, newAccount.Password))).StatusCode);
        var adminAccount = (await client.GetFromJsonAsync<UserResponse>("/api/auth/me"))!;
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/accounts/{adminAccount.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(accountPath)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(accountPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await newStaff.GetAsync("/api/products")).StatusCode);

        var categoryResponse = await client.PostAsJsonAsync("/api/categories", new CategoryRequest($"Test-{Guid.NewGuid():N}"));
        Assert.Equal(HttpStatusCode.Created, categoryResponse.StatusCode);
        var category = (await categoryResponse.Content.ReadFromJsonAsync<Category>())!;
        var request = new ProductRequest($"TEST-{Guid.NewGuid():N}", "Coffee", 25000, category.Id, null);
        var productResponse = await client.PostAsJsonAsync("/api/products", request);
        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        var product = (await productResponse.Content.ReadFromJsonAsync<Product>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/products", request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/products/{product.Id}/stock-adjustments", new StockRequest(10, "Receipt"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync($"/api/products/{product.Id}/stock-adjustments", new StockRequest(1, "Forbidden"))).StatusCode);
        var saleResponse = await staff.PostAsJsonAsync("/api/sales", new SaleRequest([new(product.Id, 3)]));
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
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/products/{product.Id}", request with { Name = "Updated coffee", Price = 30000 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/products/{product.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/categories/{category.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/sales?page=1&pageSize=10")).StatusCode);
    }
}

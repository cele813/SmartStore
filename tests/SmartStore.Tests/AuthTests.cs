using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using SmartStore.Application;
using SmartStore.Infrastructure;
using Xunit;
namespace SmartStore.Tests;

[Collection("PostgreSQL")]
public class AuthTests
{
    [Fact, Trait("Category", "Integration")]
    public async Task Registration_login_logout_and_csrf_protect_the_workspace()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Store")
            ?? throw new InvalidOperationException("Configure a dedicated PostgreSQL test database.");
        await using var db = new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>().UseNpgsql(connection).Options);
        await db.Database.MigrateAsync();
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/products")).StatusCode);
        var request = await TestAuth.Register(client);
        var me = await client.GetFromJsonAsync<UserResponse>("/api/auth/me");
        Assert.Equal(request.Email, me!.Email);
        var stored = await db.Users.SingleAsync(x => x.Email == request.Email);
        Assert.NotEqual(request.Password, stored.PasswordHash);
        Assert.False(string.IsNullOrWhiteSpace(stored.PasswordHash));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/products")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", request with { Email = request.Email.ToUpperInvariant() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", request with { Email = $"weak-{Guid.NewGuid():N}@example.com", Password = "abcdefgh" })).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/categories", new CategoryRequest("CSRF must reject"))).StatusCode);
        await TestAuth.RefreshCsrf(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        await TestAuth.RefreshCsrf(client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(request.Email, "WrongPass123!"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(request.Email, request.Password))).StatusCode);
        await TestAuth.RefreshCsrf(client);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(request.Email, "WrongPass123!"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(request.Email, request.Password))).StatusCode);
    }
}

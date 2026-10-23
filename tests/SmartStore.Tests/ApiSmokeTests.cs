using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
namespace SmartStore.Tests;

public class ApiSmokeTests
{
    private static WebApplicationFactory<Program> CreateFactory() => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        b.UseSetting("ConnectionStrings:Store", "Host=localhost;Database=unused;Username=unused;Password=unused"));

    [Fact]
    public async Task Liveness_and_frontend_load_without_database_connection()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        var html = await client.GetStringAsync("/");
        Assert.Contains("Create account", html);
        Assert.Contains("/app.js", html);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/app.js")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/styles.css")).StatusCode);
    }
    [Theory]
    [InlineData("/api/products")]
    [InlineData("/api/categories")]
    [InlineData("/api/suppliers")]
    [InlineData("/api/sales")]
    [InlineData("/api/auth/me")]
    public async Task Store_data_requires_authentication(string path)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }
    [Fact]
    public async Task Registration_requires_csrf_and_valid_input()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new { displayName = "Staff", email = "staff@example.com", password = "TestPass123!" })).StatusCode);
        await TestAuth.RefreshCsrf(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new { displayName = "", email = "invalid", password = "x" })).StatusCode);
    }
}

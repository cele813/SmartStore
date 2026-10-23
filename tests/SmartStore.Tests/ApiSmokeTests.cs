using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
namespace SmartStore.Tests;

public class ApiSmokeTests
{
    [Fact]
    public async Task Liveness_runs_without_database_connection()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.UseSetting("ConnectionStrings:Store", "Host=localhost;Database=unused;Username=unused;Password=unused"));
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }
    [Fact]
    public async Task Invalid_product_is_rejected_before_database_access()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.UseSetting("ConnectionStrings:Store", "Host=localhost;Database=unused;Username=unused;Password=unused"));
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/products", new { sku = "", name = "", price = -1 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

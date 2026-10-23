using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SmartStore.Application;
using Xunit;
namespace SmartStore.Tests;

internal static class TestAuth
{
    internal static async Task RefreshCsrf(HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", body.GetProperty("token").GetString());
    }
    internal static async Task<RegisterRequest> Register(HttpClient client)
    {
        await RefreshCsrf(client);
        var request = new RegisterRequest("Test Staff", $"staff-{Guid.NewGuid():N}@example.com", "TestPass123!");
        using var response = await client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await RefreshCsrf(client);
        return request;
    }
}

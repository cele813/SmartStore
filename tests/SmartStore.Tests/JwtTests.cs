using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartStore.Application;
using SmartStore.Infrastructure;
using SmartStore.Infrastructure.Auth;
using Xunit;

namespace SmartStore.Tests;

[Collection("PostgreSQL")]
public class JwtTests
{
    [Theory, Trait("Category", "Integration")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Bearer_tokens_preserve_roles_and_reject_deleted_accounts(bool configureKey)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Store")
            ?? throw new InvalidOperationException("Configure a dedicated PostgreSQL test database.");
        await using var db = new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>().UseNpgsql(connection).Options);
        await db.Database.MigrateAsync();

        var key = configureKey ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) : "";
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseEnvironment("Development").UseSetting("Jwt:SigningKey", key));
        using var adminCookie = factory.CreateClient(new() { AllowAutoRedirect = false });
        var admin = await TestAuth.Register(adminCookie);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync("Admin"))
                Assert.True((await roles.CreateAsync(new IdentityRole("Admin"))).Succeeded);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<StoreUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync(admin.Email))!, "Admin")).Succeeded);
        }
        Assert.Equal(HttpStatusCode.OK, (await adminCookie.PostAsJsonAsync("/api/auth/login", new LoginRequest(admin.Email, admin.Password))).StatusCode);

        using var registration = factory.CreateClient();
        var staff = await TestAuth.Register(registration);
        using var adminBearer = factory.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = false });
        using var staffBearer = factory.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = false });
        var adminToken = await IssueToken(adminBearer, admin);
        await IssueToken(staffBearer, staff);

        var staffAccount = (await staffBearer.GetFromJsonAsync<UserResponse>("/api/auth/me"))!;
        Assert.Equal("Staff", staffAccount.Role);
        Assert.Equal("Admin", (await adminBearer.GetFromJsonAsync<UserResponse>("/api/auth/me"))!.Role);
        Assert.Equal(HttpStatusCode.Forbidden, (await staffBearer.GetAsync("/api/accounts")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adminBearer.GetAsync("/api/accounts")).StatusCode);

        var category = new CategoryRequest($"JWT-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.Forbidden, (await staffBearer.PostAsJsonAsync("/api/categories", category)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await adminBearer.PostAsJsonAsync("/api/categories", category)).StatusCode);
        adminCookie.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await adminCookie.PostAsJsonAsync("/api/categories", category)).StatusCode);
        adminCookie.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid");
        Assert.Equal(HttpStatusCode.Unauthorized, (await adminCookie.GetAsync("/api/accounts")).StatusCode);

        var signatureStart = adminToken.LastIndexOf('.') + 1;
        var tampered = adminToken[..signatureStart] + (adminToken[signatureStart] == 'A' ? "B" : "A") + adminToken[(signatureStart + 1)..];
        adminBearer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);
        Assert.Equal(HttpStatusCode.Unauthorized, (await adminBearer.GetAsync("/api/accounts")).StatusCode);

        adminBearer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        Assert.Equal(HttpStatusCode.NoContent, (await adminBearer.DeleteAsync($"/api/accounts/{staffAccount.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staffBearer.GetAsync("/api/products")).StatusCode);
    }

    private static async Task<string> IssueToken(HttpClient client, RegisterRequest account)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/token", new LoginRequest(account.Email, account.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.True(response.Headers.CacheControl?.NoStore);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bearer", payload.GetProperty("tokenType").GetString());
        Assert.Equal(900, payload.GetProperty("expiresIn").GetInt32());
        var token = payload.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return token;
    }
}

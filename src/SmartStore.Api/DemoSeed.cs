using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartStore.Domain;
using SmartStore.Infrastructure;
using SmartStore.Infrastructure.Auth;

internal static class DemoSeed
{
    private const string Password = "SmartStoreDemo123!";
    private const string AdminEmail = "admin@smartstore.example";
    private const string StaffEmail = "staff@smartstore.example";

    public static async Task<int> RunAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<StoreDbContext>();
        if ((await db.Database.GetPendingMigrationsAsync()).Any())
        {
            Console.Error.WriteLine("Apply the database migrations before seeding demo data.");
            return 1;
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        var users = services.GetRequiredService<UserManager<StoreUser>>();
        var adminCreated = await CreateAccountAsync(users, AdminEmail, "Demo Admin");
        if (adminCreated && await AdminGrant.RunAsync(services, AdminEmail) != 0)
            return 1;

        var staffCreated = await CreateAccountAsync(users, StaffEmail, "Demo Staff");

        const string categoryName = "Demo Beverages";
        var category = await db.Categories.SingleOrDefaultAsync(item => item.Name == categoryName);
        if (category is null)
        {
            category = new Category { Name = categoryName };
            db.Categories.Add(category);
        }

        (string Sku, string Name, decimal Price, int Stock)[] samples =
        [
            ("DEMO-COFFEE", "Demo Coffee", 45000m, 24),
            ("DEMO-TEA", "Demo Green Tea", 32000m, 16),
            ("DEMO-WATER", "Demo Mineral Water", 10000m, 30)
        ];

        foreach (var sample in samples)
        {
            if (await db.Products.AnyAsync(product => product.Sku == sample.Sku))
                continue;

            var product = new Product
            {
                Sku = sample.Sku,
                Name = sample.Name,
                Price = sample.Price,
                CategoryId = category.Id
            };
            product.AdjustStock(sample.Stock, "Demo seed receipt");
            db.Products.Add(product);
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        Console.WriteLine("Demo data is ready.");
        ReportAccount(AdminEmail, adminCreated);
        ReportAccount(StaffEmail, staffCreated);
        return 0;
    }

    private static async Task<bool> CreateAccountAsync(UserManager<StoreUser> users, string email, string name)
    {
        if (await users.FindByEmailAsync(email) is not null)
            return false;

        var user = new StoreUser { UserName = email, Email = email, DisplayName = name };
        var result = await users.CreateAsync(user, Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(error => error.Description)));
        return true;
    }

    private static void ReportAccount(string email, bool created)
    {
        Console.WriteLine(created ? $"{email} / {Password}" : $"{email} already exists; its password and role were preserved.");
    }
}

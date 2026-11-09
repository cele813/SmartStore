using Microsoft.AspNetCore.Identity;
using SmartStore.Infrastructure.Auth;

internal static class AdminGrant
{
    public static async Task<int> RunAsync(IServiceProvider services, string email)
    {
        var users = services.GetRequiredService<UserManager<StoreUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            Console.Error.WriteLine("Account not found. Register it before granting Admin access.");
            return 1;
        }

        if (!await roles.RoleExistsAsync(StoreRoles.Admin))
        {
            var creation = await roles.CreateAsync(new IdentityRole(StoreRoles.Admin));
            if (!creation.Succeeded && !await roles.RoleExistsAsync(StoreRoles.Admin))
                return ReportFailure(creation);
        }

        if (!await users.IsInRoleAsync(user, StoreRoles.Admin))
        {
            var grant = await users.AddToRoleAsync(user, StoreRoles.Admin);
            if (!grant.Succeeded && !await users.IsInRoleAsync(user, StoreRoles.Admin))
                return ReportFailure(grant);
        }

        Console.WriteLine("Admin access granted. Sign out and sign in again to refresh the session.");
        return 0;
    }

    private static int ReportFailure(IdentityResult result)
    {
        foreach (var error in result.Errors) Console.Error.WriteLine(error.Description);
        return 1;
    }
}

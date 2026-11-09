using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SmartStore.Infrastructure.Auth;

internal static class StoreRoles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
}

internal sealed class StorePrincipalFactory(
    UserManager<StoreUser> users,
    RoleManager<IdentityRole> roles,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<StoreUser, IdentityRole>(users, roles, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(StoreUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (!identity.HasClaim(identity.RoleClaimType, StoreRoles.Admin))
            identity.AddClaim(new Claim(identity.RoleClaimType, StoreRoles.Staff));
        return identity;
    }
}

using Microsoft.AspNetCore.Identity;
namespace SmartStore.Infrastructure.Auth;

public sealed class StoreUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
}

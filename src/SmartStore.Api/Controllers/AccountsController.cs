using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartStore.Application;
using SmartStore.Infrastructure;
using SmartStore.Infrastructure.Auth;

namespace SmartStore.Api.Controllers;

[ApiController, Authorize(Roles = StoreRoles.Admin), Route("api/accounts")]
public sealed class AccountsController(StoreDbContext db, UserManager<StoreUser> users) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, int page = 1)
    {
        if (page is < 1 or > 1000000) return BadRequest();

        const int pageSize = 20;
        var adminUsers =
            from membership in db.UserRoles
            join role in db.Roles on membership.RoleId equals role.Id
            where role.Name == StoreRoles.Admin
            select membership.UserId;

        var items = await db.Users.AsNoTracking()
            .OrderBy(user => user.Email).ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1)
            .Select(user => new UserResponse(user.Id, user.DisplayName, user.Email!,
                adminUsers.Contains(user.Id) ? StoreRoles.Admin : StoreRoles.Staff))
            .ToListAsync(ct);
        return Ok(new { items = items.Take(pageSize), hasMore = items.Count > pageSize });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var account = await users.FindByIdAsync(id);
        if (account is null) return NotFound();

        var role = await users.IsInRoleAsync(account, StoreRoles.Admin) ? StoreRoles.Admin : StoreRoles.Staff;
        return Ok(new UserResponse(account.Id, account.DisplayName, account.Email!, role));
    }

    [HttpPost]
    public async Task<IActionResult> Create(RegisterRequest request)
    {
        var name = request.DisplayName.Trim();
        if (name.Length < 2) return InvalidName();

        var email = request.Email.Trim();
        var account = new StoreUser { UserName = email, Email = email, DisplayName = name };
        var result = await users.CreateAsync(account, request.Password);
        if (!result.Succeeded) return AccountError(result);

        return CreatedAtAction(nameof(Get), new { id = account.Id },
            new UserResponse(account.Id, account.DisplayName, email, StoreRoles.Staff));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Rename(string id, RenameAccountRequest request)
    {
        var name = request.DisplayName.Trim();
        if (name.Length < 2) return InvalidName();

        var account = await users.FindByIdAsync(id);
        if (account is null) return NotFound();

        account.DisplayName = name;
        var result = await users.UpdateAsync(account);
        return result.Succeeded ? NoContent() : AccountError(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var account = await users.FindByIdAsync(id);
        if (account is null) return NotFound();
        if (await users.IsInRoleAsync(account, StoreRoles.Admin))
            return Conflict(new ProblemDetails { Title = "Admin accounts cannot be deleted." });

        var result = await users.DeleteAsync(account);
        return result.Succeeded ? NoContent() : AccountError(result);
    }

    private IActionResult InvalidName() =>
        BadRequest(new ProblemDetails { Title = "Name must contain at least two characters." });

    private IActionResult AccountError(IdentityResult result)
    {
        var conflict = result.Errors.FirstOrDefault(error =>
            error.Code is "ConcurrencyFailure" or "DuplicateEmail" or "DuplicateUserName");
        if (conflict is not null)
            return Conflict(new ProblemDetails { Title = conflict.Description });

        foreach (var error in result.Errors) ModelState.AddModelError("account", error.Description);
        return ValidationProblem(ModelState);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record RenameAccountRequest([Required, StringLength(100, MinimumLength = 2)] string DisplayName);
}

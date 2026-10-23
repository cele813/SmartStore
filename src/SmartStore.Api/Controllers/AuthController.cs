using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartStore.Application;
using SmartStore.Infrastructure.Auth;
namespace SmartStore.Api.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(UserManager<StoreUser> users, SignInManager<StoreUser> signIn, IAntiforgery antiforgery) : ControllerBase
{
    [AllowAnonymous, HttpGet("csrf")]
    public IActionResult Csrf()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
    }

    [AllowAnonymous, HttpPost("register"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var name = request.DisplayName.Trim();
        if (name.Length < 2) return BadRequest(new ProblemDetails { Title = "Name must contain at least two characters." });
        var email = request.Email.Trim();
        var user = new StoreUser { UserName = email, Email = email, DisplayName = name };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError("registration", error.Description);
            return ValidationProblem(ModelState);
        }
        await signIn.SignInAsync(user, isPersistent: false);
        return StatusCode(StatusCodes.Status201Created, ToResponse(user));
    }

    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null) return Unauthorized(new ProblemDetails { Title = "Invalid email or password." });
        var result = await signIn.PasswordSignInAsync(user, request.Password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded) return Unauthorized(new ProblemDetails { Title = "Invalid email or password, or account temporarily locked." });
        return Ok(ToResponse(user));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        Response.Headers.CacheControl = "no-store";
        var user = await users.GetUserAsync(User);
        return user is null ? Unauthorized() : Ok(ToResponse(user));
    }

    private static UserResponse ToResponse(StoreUser user) => new(user.Id, user.DisplayName, user.Email!);
}

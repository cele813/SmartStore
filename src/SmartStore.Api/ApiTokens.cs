using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using SmartStore.Infrastructure.Auth;

public sealed class ApiTokens
{
    private const string Issuer = "SmartStore";
    private const string Audience = "SmartStore.Api";
    private const string StampClaim = "sst";
    private const int LifetimeSeconds = 900;
    private readonly SymmetricSecurityKey? key;

    public ApiTokens(IConfiguration configuration, IHostEnvironment environment)
    {
        var value = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(value))
        {
            if (environment.IsDevelopment())
                key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
            return;
        }

        Span<byte> decoded = new byte[value.Length];
        if (!Convert.TryFromBase64String(value, decoded, out var length) || length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be a Base64 value with at least 32 decoded bytes.");

        key = new SymmetricSecurityKey(decoded[..length].ToArray());
    }

    public bool Enabled => key is not null;

    public object Issue(StoreUser user, bool isAdmin, string securityStamp)
    {
        var now = DateTimeOffset.UtcNow;
        var token = new JwtSecurityToken(
            Issuer,
            Audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
                new Claim("role", isAdmin ? StoreRoles.Admin : StoreRoles.Staff),
                new Claim(StampClaim, StampFingerprint(securityStamp))
            ],
            notBefore: now.UtcDateTime,
            expires: now.AddSeconds(LifetimeSeconds).UtcDateTime,
            signingCredentials: new SigningCredentials(key!, SecurityAlgorithms.HmacSha256));

        return new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), tokenType = "Bearer", expiresIn = LifetimeSeconds };
    }

    public void Configure(JwtBearerOptions options)
    {
        options.IncludeErrorDetails = false;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = key,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = "role"
        };
        options.Events.OnTokenValidated = async context =>
        {
            var principal = context.Principal!;
            var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<StoreUser>>();
            var id = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var account = id is null ? null : await users.FindByIdAsync(id);
            if (account is null
                || principal.FindFirstValue(StampClaim) != StampFingerprint(await users.GetSecurityStampAsync(account))
                || await users.IsLockedOutAsync(account)
                || principal.IsInRole(StoreRoles.Admin) != await users.IsInRoleAsync(account, StoreRoles.Admin))
            {
                context.Fail("The account or its permissions have changed.");
                return;
            }

            ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(ClaimTypes.NameIdentifier, account.Id));
        };
    }

    private static string StampFingerprint(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

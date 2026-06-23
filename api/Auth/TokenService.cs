using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TaskManager.Api.Domain;

namespace TaskManager.Api.Auth;

/// <summary>
/// Issues the signed JWT carried in the auth cookie. The signing key and issuer come from
/// configuration (Jwt:Key / Jwt:Issuer); the matching validation lives in Program.cs.
/// </summary>
public sealed class TokenService
{
    private readonly SymmetricSecurityKey _key;

    public string Issuer { get; }
    public TimeSpan Lifetime { get; } = TimeSpan.FromDays(7);
    public SymmetricSecurityKey SigningKey => _key;

    public TokenService(IConfiguration config)
    {
        var key = config["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "Jwt:Key is not configured. Set it via the Jwt__Key environment variable (production) " +
                "or appsettings.Development.json (local).");
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        Issuer = config["Jwt:Issuer"] ?? "taskmanager";
    }

    public string CreateToken(User user)
    {
        // "sub" is the user id (read back by CurrentUser; JwtBearer keeps it verbatim because
        // MapInboundClaims is disabled in Program.cs). "jti" makes each issued token unique.
        var claims = new[]
        {
            new Claim("sub", user.Id.ToString()),
            new Claim("email", user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: null,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(Lifetime),
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

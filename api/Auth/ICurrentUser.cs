using System.Security.Claims;

namespace TaskManager.Api.Auth;

/// <summary>Per-request identity, resolved from the JWT claims on the current HttpContext.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
}

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public CurrentUser(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal? Principal => _http.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId =>
        Guid.TryParse(
            Principal?.FindFirst("sub")?.Value
            ?? Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            out var id)
            ? id
            : null;

    public string? Email =>
        Principal?.FindFirst("email")?.Value
        ?? Principal?.FindFirst(ClaimTypes.Email)?.Value;
}

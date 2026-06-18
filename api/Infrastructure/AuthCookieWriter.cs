using TaskManager.Api.Auth;

namespace TaskManager.Api.Infrastructure;

public static class AuthCookieWriter
{
    public static void SetAuthCookie(HttpContext ctx, string token, IHostEnvironment env, TimeSpan lifetime) =>
        ctx.Response.Cookies.Append(AuthCookie.Name, token, BuildOptions(env, DateTimeOffset.UtcNow.Add(lifetime)));

    public static void ClearAuthCookie(HttpContext ctx, IHostEnvironment env) =>
        ctx.Response.Cookies.Delete(AuthCookie.Name, BuildOptions(env, expires: null));

    private static CookieOptions BuildOptions(IHostEnvironment env, DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        // Off in Development so the app runs over plain http://localhost with no HTTPS dev cert.
        Secure = !env.IsDevelopment(),
        IsEssential = true,
        Path = "/",
        Expires = expires
    };
}

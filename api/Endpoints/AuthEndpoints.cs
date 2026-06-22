using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Auth;
using TaskManager.Api.Contracts;
using TaskManager.Api.Data;
using TaskManager.Api.Domain;
using TaskManager.Api.Infrastructure;
using TaskManager.Api.Validation;

namespace TaskManager.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var audit = app.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Audit");
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (
            RegisterRequest req, AppDbContext db, TokenService tokens, IHostEnvironment env, HttpContext http) =>
        {
            var email = Normalize(req.Email);
            if (await db.Users.AnyAsync(u => u.Email == email))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["email"] = new[] { "An account with this email already exists." }
                });
            }

            var user = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
            };
            db.Users.Add(user);

            // Every user gets a private "Personal" team to hold their own tasks.
            var personal = new Team { Name = "Personal", IsPersonal = true, CreatedByUserId = user.Id };
            db.Teams.Add(personal);
            db.TeamMemberships.Add(new TeamMembership { TeamId = personal.Id, UserId = user.Id });
            await db.SaveChangesAsync();

            AuthCookieWriter.SetAuthCookie(http, tokens.CreateToken(user), env, tokens.Lifetime);
            audit.LogInformation("Account registered {UserId}", user.Id);
            return Results.Created("/api/auth/me", new UserResponse(user.Id, user.Email));
        })
        .AddEndpointFilter<ValidationFilter<RegisterRequest>>();

        group.MapPost("/login", async (
            LoginRequest req, AppDbContext db, TokenService tokens, IHostEnvironment env, HttpContext http) =>
        {
            var email = Normalize(req.Email);
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            {
                audit.LogWarning("Login failed for {Email}", email);
                // Generic message — never reveal whether the email exists.
                return Results.Problem(title: "Invalid email or password.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            AuthCookieWriter.SetAuthCookie(http, tokens.CreateToken(user), env, tokens.Lifetime);
            audit.LogInformation("Login succeeded {UserId}", user.Id);
            return Results.Ok(new UserResponse(user.Id, user.Email));
        })
        .AddEndpointFilter<ValidationFilter<LoginRequest>>();

        group.MapPost("/logout", (IHostEnvironment env, HttpContext http, ICurrentUser current) =>
        {
            AuthCookieWriter.ClearAuthCookie(http, env);
            audit.LogInformation("Logout {UserId}", current.UserId);
            return Results.NoContent();
        });

        group.MapGet("/me", async (ICurrentUser current, AppDbContext db) =>
        {
            if (current.UserId is not { } id) return Results.Unauthorized();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
            return user is null ? Results.Unauthorized() : Results.Ok(new UserResponse(user.Id, user.Email));
        })
        .RequireAuthorization();
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}

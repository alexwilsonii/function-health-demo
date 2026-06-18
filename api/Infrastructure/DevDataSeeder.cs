using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Api.Domain;

namespace TaskManager.Api.Infrastructure;

/// <summary>
/// Seeds a demo account + a few sample tasks on startup — Development only (wired up in Program.cs).
/// Idempotent: keyed on the demo user's existence, so it runs once and never duplicates.
/// </summary>
public static class DevDataSeeder
{
    public const string DemoEmail = "demo@example.com";
    public const string DemoPassword = "Password123!";

    public static async Task SeedAsync(AppDbContext db)
    {
        // Users are not covered by the per-user query filter, so this check is reliable at startup
        // (where there is no current user).
        if (await db.Users.AnyAsync(u => u.Email == DemoEmail)) return;

        var user = new User
        {
            Email = DemoEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword),
        };
        db.Users.Add(user);

        var now = DateTimeOffset.UtcNow;
        db.Tasks.AddRange(
            new TaskItem
            {
                UserId = user.Id,
                Title = "Review pull request #42",
                Notes = "Auth + ownership changes — needs a careful pass.",
                Status = TaskState.InProgress,
                Priority = TaskPriority.High,
                IsPinned = true,
                DueAt = now.AddDays(1),
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now,
            },
            new TaskItem
            {
                UserId = user.Id,
                Title = "Renew SSL certificate",
                Notes = "Production cert is past due.",
                Status = TaskState.Todo,
                Priority = TaskPriority.High,
                DueAt = now.AddDays(-2), // overdue
                CreatedAt = now.AddDays(-3),
                UpdatedAt = now.AddDays(-3),
            },
            new TaskItem
            {
                UserId = user.Id,
                Title = "Write release notes",
                Status = TaskState.Todo,
                Priority = TaskPriority.Medium,
                DueAt = now.AddDays(3),
                CreatedAt = now.AddHours(-6),
                UpdatedAt = now.AddHours(-6),
            },
            new TaskItem
            {
                UserId = user.Id,
                Title = "Update dependencies",
                Notes = "Bump minor versions across the solution.",
                Status = TaskState.Todo,
                Priority = TaskPriority.Low,
                CreatedAt = now.AddHours(-2),
                UpdatedAt = now.AddHours(-2),
            },
            new TaskItem
            {
                UserId = user.Id,
                Title = "Archive Q1 reports",
                Status = TaskState.Done,
                Priority = TaskPriority.Low,
                CompletedAt = now.AddDays(-1),
                CreatedAt = now.AddDays(-5),
                UpdatedAt = now.AddDays(-1),
            });

        await db.SaveChangesAsync();
    }
}

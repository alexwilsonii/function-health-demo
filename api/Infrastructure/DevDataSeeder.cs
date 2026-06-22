using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Api.Domain;

namespace TaskManager.Api.Infrastructure;

/// <summary>
/// Seeds a demo account, a teammate, and a shared team (to show team visibility + assignment) on
/// startup — Development, or anywhere SeedDemoData=true. Idempotent: keyed on the demo user.
/// Both accounts use the same password so you can log in as either to see both sides.
/// </summary>
public static class DevDataSeeder
{
    public const string DemoEmail = "demo@example.com";
    public const string TeammateEmail = "teammate@example.com";
    public const string DemoPassword = "Password123!";

    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync(u => u.Email == DemoEmail)) return;

        var now = DateTimeOffset.UtcNow;
        var hash = BCrypt.Net.BCrypt.HashPassword(DemoPassword);

        var demo = NewUser(DemoEmail, hash);
        var mate = NewUser(TeammateEmail, hash);
        db.Users.AddRange(demo, mate);

        var demoPersonal = NewTeam("Personal", isPersonal: true, demo.Id);
        var matePersonal = NewTeam("Personal", isPersonal: true, mate.Id);
        var shared = NewTeam("Acme Web Team", isPersonal: false, demo.Id);
        db.Teams.AddRange(demoPersonal, matePersonal, shared);

        db.TeamMemberships.AddRange(
            new TeamMembership { TeamId = demoPersonal.Id, UserId = demo.Id },
            new TeamMembership { TeamId = matePersonal.Id, UserId = mate.Id },
            new TeamMembership { TeamId = shared.Id, UserId = demo.Id },
            new TeamMembership { TeamId = shared.Id, UserId = mate.Id });

        db.Tasks.AddRange(
            // demo's private (Personal team) tasks
            Task_(demoPersonal.Id, demo.Id, demo.Id, "Plan my week", null,
                TaskState.Todo, TaskPriority.Medium, now.AddDays(1), now.AddHours(-3)),
            Task_(demoPersonal.Id, demo.Id, null, "Renew SSL certificate", "Production cert is past due.",
                TaskState.Todo, TaskPriority.High, now.AddDays(-2), now.AddDays(-3)),

            // shared team — tasks created by and assigned across both members
            Pin(Task_(shared.Id, demo.Id, mate.Id, "Review pull request #42", "Auth + ownership changes — needs a careful pass.",
                TaskState.InProgress, TaskPriority.High, now.AddDays(1), now.AddDays(-1))),
            Task_(shared.Id, mate.Id, demo.Id, "Write release notes", "For the 2.0 launch.",
                TaskState.Todo, TaskPriority.Medium, now.AddDays(3), now.AddHours(-6)),
            Task_(shared.Id, mate.Id, null, "Triage incoming bugs", null,
                TaskState.Todo, TaskPriority.Low, null, now.AddHours(-2)),
            Done(Task_(shared.Id, demo.Id, mate.Id, "Ship hotfix 1.9.3", null,
                TaskState.Done, TaskPriority.High, null, now.AddDays(-2)), now.AddDays(-1)));

        await db.SaveChangesAsync();
    }

    private static User NewUser(string email, string hash) => new() { Email = email, PasswordHash = hash };

    private static Team NewTeam(string name, bool isPersonal, Guid createdBy) =>
        new() { Name = name, IsPersonal = isPersonal, CreatedByUserId = createdBy };

    private static TaskItem Task_(Guid teamId, Guid creator, Guid? assignee, string title, string? notes,
        TaskState status, TaskPriority priority, DateTimeOffset? dueAt, DateTimeOffset createdAt) => new()
    {
        TeamId = teamId,
        CreatedByUserId = creator,
        AssigneeUserId = assignee,
        Title = title,
        Notes = notes,
        Status = status,
        Priority = priority,
        DueAt = dueAt,
        CreatedAt = createdAt,
        UpdatedAt = createdAt,
    };

    private static TaskItem Pin(TaskItem t)
    {
        t.IsPinned = true;
        return t;
    }

    private static TaskItem Done(TaskItem t, DateTimeOffset completedAt)
    {
        t.CompletedAt = completedAt;
        return t;
    }
}

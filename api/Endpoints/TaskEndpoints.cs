using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Auth;
using TaskManager.Api.Contracts;
using TaskManager.Api.Data;
using TaskManager.Api.Domain;
using TaskManager.Api.Validation;

namespace TaskManager.Api.Endpoints;

public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var audit = app.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Audit");

        // Every task endpoint requires auth; visibility is scoped to the teams the current user is in.
        var group = app.MapGroup("/api/tasks").WithTags("Tasks").RequireAuthorization();

        group.MapGet("", async (
            AppDbContext db, IMembership membership, ICurrentUser current,
            string? q, TaskState? status, TaskPriority? priority, string? sort, Guid? teamId, string? assignee) =>
        {
            var teamIds = await membership.TeamIdsAsync();
            IQueryable<TaskItem> query = db.Tasks.Where(t => teamIds.Contains(t.TeamId));

            if (teamId is { } tid) query = query.Where(t => t.TeamId == tid);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                query = query.Where(t =>
                    t.Title.ToLower().Contains(term) ||
                    (t.Notes != null && t.Notes.ToLower().Contains(term)));
            }
            if (status.HasValue) query = query.Where(t => t.Status == status.Value);
            if (priority.HasValue) query = query.Where(t => t.Priority == priority.Value);

            // Assignee filter: "me" | "unassigned" | a member's user-id.
            if (assignee == "me")
            {
                var uid = current.UserId;
                query = query.Where(t => t.AssigneeUserId == uid);
            }
            else if (assignee == "unassigned")
            {
                query = query.Where(t => t.AssigneeUserId == null);
            }
            else if (Guid.TryParse(assignee, out var aid))
            {
                query = query.Where(t => t.AssigneeUserId == aid);
            }

            var ordered = query.OrderByDescending(t => t.IsPinned);
            ordered = sort switch
            {
                "priority" => ordered.ThenByDescending(t => t.Priority).ThenByDescending(t => t.CreatedAt),
                "created"  => ordered.ThenByDescending(t => t.CreatedAt),
                _          => ordered.ThenBy(t => t.DueAt == null).ThenBy(t => t.DueAt).ThenByDescending(t => t.CreatedAt)
            };

            var tasks = await ordered.Select(ToResponse).ToListAsync();
            return Results.Ok(tasks);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, IMembership membership) =>
        {
            var teamIds = await membership.TeamIdsAsync();
            var task = await db.Tasks.Where(t => t.Id == id && teamIds.Contains(t.TeamId)).Select(ToResponse).FirstOrDefaultAsync();
            return task is null ? Results.NotFound() : Results.Ok(task);
        });

        group.MapPost("", async (CreateTaskRequest req, AppDbContext db, IMembership membership, ICurrentUser current) =>
        {
            if (!await membership.IsMemberAsync(req.TeamId))
                return Field("teamId", "You're not a member of that team.");
            if (req.AssigneeUserId is { } aid && !await db.TeamMemberships.AnyAsync(m => m.TeamId == req.TeamId && m.UserId == aid))
                return Field("assigneeUserId", "Assignee must be a member of the team.");

            var now = DateTimeOffset.UtcNow;
            var status = req.Status ?? TaskState.Todo;
            var task = new TaskItem
            {
                TeamId = req.TeamId,
                CreatedByUserId = current.UserId!.Value,
                AssigneeUserId = req.AssigneeUserId,
                Title = req.Title.Trim(),
                Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
                Status = status,
                Priority = req.Priority ?? TaskPriority.Medium,
                DueAt = req.DueAt?.ToUniversalTime(),
                CreatedAt = now,
                UpdatedAt = now,
                CompletedAt = status == TaskState.Done ? now : null
            };
            db.Tasks.Add(task);
            await db.SaveChangesAsync();
            audit.LogInformation("Task created {TaskId} in team {TeamId} by {UserId}", task.Id, task.TeamId, current.UserId);

            var resp = await db.Tasks.Where(t => t.Id == task.Id).Select(ToResponse).FirstAsync();
            return Results.Created($"/api/tasks/{task.Id}", resp);
        })
        .AddEndpointFilter<ValidationFilter<CreateTaskRequest>>();

        group.MapPatch("/{id:guid}", async (Guid id, UpdateTaskRequest req, AppDbContext db, IMembership membership, ICurrentUser current) =>
        {
            var teamIds = await membership.TeamIdsAsync();
            var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && teamIds.Contains(t.TeamId));
            if (task is null)
            {
                audit.LogWarning("Task update denied (not visible) {TaskId} by {UserId}", id, current.UserId);
                return Results.NotFound();
            }
            if (req.AssigneeUserId is { } aid && !await db.TeamMemberships.AnyAsync(m => m.TeamId == task.TeamId && m.UserId == aid))
                return Field("assigneeUserId", "Assignee must be a member of the team.");

            var wasDone = task.Status == TaskState.Done;
            task.Title = req.Title.Trim();
            task.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
            task.Status = req.Status;
            task.Priority = req.Priority;
            task.DueAt = req.DueAt?.ToUniversalTime();
            task.IsPinned = req.IsPinned;
            task.AssigneeUserId = req.AssigneeUserId;
            task.UpdatedAt = DateTimeOffset.UtcNow;
            if (req.Status == TaskState.Done && !wasDone) task.CompletedAt = DateTimeOffset.UtcNow;
            else if (req.Status != TaskState.Done) task.CompletedAt = null;

            await db.SaveChangesAsync();
            audit.LogInformation("Task updated {TaskId} by {UserId}", id, current.UserId);

            var resp = await db.Tasks.Where(t => t.Id == id).Select(ToResponse).FirstAsync();
            return Results.Ok(resp);
        })
        .AddEndpointFilter<ValidationFilter<UpdateTaskRequest>>();

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, IMembership membership, ICurrentUser current) =>
        {
            var teamIds = await membership.TeamIdsAsync();
            var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && teamIds.Contains(t.TeamId));
            if (task is null)
            {
                audit.LogWarning("Task delete denied (not visible) {TaskId} by {UserId}", id, current.UserId);
                return Results.NotFound();
            }
            // Visible to all team members, but only the creator may delete.
            if (task.CreatedByUserId != current.UserId)
            {
                audit.LogWarning("Task delete forbidden (not creator) {TaskId} by {UserId}", id, current.UserId);
                return Results.Problem(title: "Only the task's creator can delete it.", statusCode: StatusCodes.Status403Forbidden);
            }

            db.Tasks.Remove(task);
            await db.SaveChangesAsync();
            audit.LogInformation("Task deleted {TaskId} by {UserId}", id, current.UserId);
            return Results.NoContent();
        });
    }

    private static IResult Field(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = new[] { message } });

    // Projected in-query so EF joins Team / CreatedBy / Assignee in one round-trip.
    private static readonly Expression<Func<TaskItem, TaskResponse>> ToResponse = t => new TaskResponse(
        t.Id,
        t.TeamId,
        t.Team!.Name,
        t.Title,
        t.Notes,
        t.Status,
        t.Priority,
        t.DueAt,
        t.IsPinned,
        t.CreatedByUserId,
        t.CreatedBy!.Email,
        t.AssigneeUserId,
        t.Assignee != null ? t.Assignee.Email : null,
        t.CreatedAt,
        t.UpdatedAt,
        t.CompletedAt);
}

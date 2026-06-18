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
        // Every task endpoint requires auth; every query is scoped to the current user by the
        // global query filter (and, for single-item ops, an explicit predicate as well).
        var audit = app.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Audit");
        var group = app.MapGroup("/api/tasks").WithTags("Tasks").RequireAuthorization();

        group.MapGet("", async (
            AppDbContext db, string? q, TaskState? status, TaskPriority? priority, string? sort) =>
        {
            IQueryable<TaskItem> query = db.Tasks;

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                query = query.Where(t =>
                    t.Title.ToLower().Contains(term) ||
                    (t.Notes != null && t.Notes.ToLower().Contains(term)));
            }
            if (status.HasValue) query = query.Where(t => t.Status == status.Value);
            if (priority.HasValue) query = query.Where(t => t.Priority == priority.Value);

            // Pinned always float to the top; then the chosen sort (default: soonest due first).
            var ordered = query.OrderByDescending(t => t.IsPinned);
            ordered = sort switch
            {
                "priority" => ordered.ThenByDescending(t => t.Priority).ThenByDescending(t => t.CreatedAt),
                "created"  => ordered.ThenByDescending(t => t.CreatedAt),
                _          => ordered.ThenBy(t => t.DueAt == null).ThenBy(t => t.DueAt).ThenByDescending(t => t.CreatedAt)
            };

            var tasks = await ordered.ToListAsync();
            return Results.Ok(tasks.Select(ToResponse));
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
            return task is null ? Results.NotFound() : Results.Ok(ToResponse(task));
        });

        group.MapPost("", async (CreateTaskRequest req, AppDbContext db, ICurrentUser current) =>
        {
            var now = DateTimeOffset.UtcNow;
            var status = req.Status ?? TaskState.Todo;

            var task = new TaskItem
            {
                UserId = current.UserId!.Value,
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
            audit.LogInformation("Task created {TaskId} by {UserId}", task.Id, current.UserId);
            return Results.Created($"/api/tasks/{task.Id}", ToResponse(task));
        })
        .AddEndpointFilter<ValidationFilter<CreateTaskRequest>>();

        group.MapPatch("/{id:guid}", async (Guid id, UpdateTaskRequest req, AppDbContext db, ICurrentUser current) =>
        {
            var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == current.UserId);
            if (task is null)
            {
                audit.LogWarning("Task update denied (not found or not owner) {TaskId} by {UserId}", id, current.UserId);
                return Results.NotFound();
            }

            var wasDone = task.Status == TaskState.Done;

            task.Title = req.Title.Trim();
            task.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
            task.Status = req.Status;
            task.Priority = req.Priority;
            task.DueAt = req.DueAt?.ToUniversalTime();
            task.IsPinned = req.IsPinned;
            task.UpdatedAt = DateTimeOffset.UtcNow;

            // CompletedAt invariant: set when entering Done, cleared when leaving it.
            if (req.Status == TaskState.Done && !wasDone) task.CompletedAt = DateTimeOffset.UtcNow;
            else if (req.Status != TaskState.Done) task.CompletedAt = null;

            await db.SaveChangesAsync();
            audit.LogInformation("Task updated {TaskId} by {UserId}", task.Id, current.UserId);
            return Results.Ok(ToResponse(task));
        })
        .AddEndpointFilter<ValidationFilter<UpdateTaskRequest>>();

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, ICurrentUser current) =>
        {
            var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == current.UserId);
            if (task is null)
            {
                audit.LogWarning("Task delete denied (not found or not owner) {TaskId} by {UserId}", id, current.UserId);
                return Results.NotFound();
            }

            db.Tasks.Remove(task);
            await db.SaveChangesAsync();
            audit.LogInformation("Task deleted {TaskId} by {UserId}", task.Id, current.UserId);
            return Results.NoContent();
        });
    }

    private static TaskResponse ToResponse(TaskItem t) => new(
        t.Id, t.Title, t.Notes, t.Status, t.Priority, t.DueAt, t.IsPinned, t.CreatedAt, t.UpdatedAt, t.CompletedAt);
}

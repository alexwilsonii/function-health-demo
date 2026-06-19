using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Auth;
using TaskManager.Api.Contracts;
using TaskManager.Api.Data;
using TaskManager.Api.Domain;
using TaskManager.Api.Validation;

namespace TaskManager.Api.Endpoints;

public static class CommentEndpoints
{
    public static void MapCommentEndpoints(this IEndpointRouteBuilder app)
    {
        var audit = app.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Audit");

        // Nested under the task: every handler verifies task ownership first (the task query is
        // owner-scoped), so a foreign/missing task returns 404 before any comment is touched.
        var group = app.MapGroup("/api/tasks/{taskId:guid}/comments")
            .WithTags("Comments")
            .RequireAuthorization();

        group.MapGet("", async (Guid taskId, AppDbContext db) =>
        {
            if (!await OwnsTaskAsync(db, taskId)) return Results.NotFound();

            var comments = await db.Comments
                .Where(c => c.TaskId == taskId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return Results.Ok(comments.Select(ToResponse));
        });

        group.MapPost("", async (Guid taskId, CreateCommentRequest req, AppDbContext db, ICurrentUser current) =>
        {
            if (!await OwnsTaskAsync(db, taskId)) return Results.NotFound();

            var comment = new Comment
            {
                TaskId = taskId,
                UserId = current.UserId!.Value,
                Body = req.Body.Trim(),
            };
            db.Comments.Add(comment);
            await db.SaveChangesAsync();

            audit.LogInformation("Comment added {CommentId} on {TaskId} by {UserId}", comment.Id, taskId, current.UserId);
            return Results.Created($"/api/tasks/{taskId}/comments/{comment.Id}", ToResponse(comment));
        })
        .AddEndpointFilter<ValidationFilter<CreateCommentRequest>>();

        group.MapDelete("/{commentId:guid}", async (Guid taskId, Guid commentId, AppDbContext db, ICurrentUser current) =>
        {
            if (!await OwnsTaskAsync(db, taskId)) return Results.NotFound();

            var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId && c.TaskId == taskId);
            if (comment is null) return Results.NotFound();

            db.Comments.Remove(comment);
            await db.SaveChangesAsync();

            audit.LogInformation("Comment deleted {CommentId} on {TaskId} by {UserId}", commentId, taskId, current.UserId);
            return Results.NoContent();
        });
    }

    // Owner-scoped: the global query filter restricts Tasks to the current user.
    private static async Task<bool> OwnsTaskAsync(AppDbContext db, Guid taskId) =>
        await db.Tasks.AnyAsync(t => t.Id == taskId);

    private static CommentResponse ToResponse(Comment c) => new(c.Id, c.TaskId, c.Body, c.CreatedAt);
}

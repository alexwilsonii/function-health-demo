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

        // Nested under the task: every handler checks the task is visible to the current user (i.e. in
        // one of their teams) before touching comments — a foreign/missing task returns 404.
        var group = app.MapGroup("/api/tasks/{taskId:guid}/comments")
            .WithTags("Comments")
            .RequireAuthorization();

        group.MapGet("", async (Guid taskId, AppDbContext db, IMembership membership) =>
        {
            if (!await CanSeeTaskAsync(db, membership, taskId)) return Results.NotFound();

            var comments = await db.Comments
                .Where(c => c.TaskId == taskId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new CommentResponse(c.Id, c.TaskId, c.Body, c.Author!.Email, c.CreatedAt))
                .ToListAsync();

            return Results.Ok(comments);
        });

        group.MapPost("", async (Guid taskId, CreateCommentRequest req, AppDbContext db, IMembership membership, ICurrentUser current) =>
        {
            if (!await CanSeeTaskAsync(db, membership, taskId)) return Results.NotFound();

            var comment = new Comment
            {
                TaskId = taskId,
                UserId = current.UserId!.Value,
                Body = req.Body.Trim()
            };
            db.Comments.Add(comment);
            await db.SaveChangesAsync();

            audit.LogInformation("Comment added {CommentId} on {TaskId} by {UserId}", comment.Id, taskId, current.UserId);
            return Results.Created($"/api/tasks/{taskId}/comments/{comment.Id}",
                new CommentResponse(comment.Id, taskId, comment.Body, current.Email!, comment.CreatedAt));
        })
        .AddEndpointFilter<ValidationFilter<CreateCommentRequest>>();

        group.MapDelete("/{commentId:guid}", async (Guid taskId, Guid commentId, AppDbContext db, IMembership membership, ICurrentUser current) =>
        {
            if (!await CanSeeTaskAsync(db, membership, taskId)) return Results.NotFound();

            var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId && c.TaskId == taskId);
            if (comment is null) return Results.NotFound();
            if (comment.UserId != current.UserId)
                return Results.Problem(title: "You can only delete your own comments.", statusCode: StatusCodes.Status403Forbidden);

            db.Comments.Remove(comment);
            await db.SaveChangesAsync();
            audit.LogInformation("Comment deleted {CommentId} on {TaskId} by {UserId}", commentId, taskId, current.UserId);
            return Results.NoContent();
        });
    }

    // Visible if the task is in one of the current user's teams.
    private static async Task<bool> CanSeeTaskAsync(AppDbContext db, IMembership membership, Guid taskId)
    {
        var teamIds = await membership.TeamIdsAsync();
        return await db.Tasks.AnyAsync(t => t.Id == taskId && teamIds.Contains(t.TeamId));
    }
}

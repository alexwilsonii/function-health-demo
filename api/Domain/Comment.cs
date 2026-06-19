namespace TaskManager.Api.Domain;

/// <summary>
/// A free-text comment on a task. Reached only through the owning task
/// (/api/tasks/{taskId}/comments), so ownership is enforced via the parent task.
/// </summary>
public class Comment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TaskId { get; set; }

    /// <summary>Author. In this single-user app it's always the task owner; kept for integrity/audit.</summary>
    public Guid UserId { get; set; }

    public string Body { get; set; } = default!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public TaskItem? Task { get; set; }
}

namespace TaskManager.Api.Domain;

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Owner. Every task belongs to exactly one user; enforced by a global query filter.</summary>
    public Guid UserId { get; set; }

    public string Title { get; set; } = default!;

    public string? Notes { get; set; }

    public TaskState Status { get; set; } = TaskState.Todo;

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    /// <summary>A due *instant*, stored in UTC. Null = no due date.</summary>
    public DateTimeOffset? DueAt { get; set; }

    public bool IsPinned { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Set when Status becomes Done, cleared when it leaves Done.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    public User? User { get; set; }
}

namespace TaskManager.Api.Domain;

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The team this task belongs to. Visibility = membership of this team.</summary>
    public Guid TeamId { get; set; }

    /// <summary>Who created the task.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>Optional assignee — who should do it. Must be a member of the task's team.</summary>
    public Guid? AssigneeUserId { get; set; }

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

    public Team? Team { get; set; }
    public User? CreatedBy { get; set; }
    public User? Assignee { get; set; }
}

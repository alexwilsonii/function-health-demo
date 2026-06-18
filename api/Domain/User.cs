namespace TaskManager.Api.Domain;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Stored normalized (trimmed + lower-cased) so uniqueness is case-insensitive.</summary>
    public string Email { get; set; } = default!;

    public string PasswordHash { get; set; } = default!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}

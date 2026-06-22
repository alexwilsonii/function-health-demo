namespace TaskManager.Api.Domain;

/// <summary>Join row: a user's membership in a team. Composite key (TeamId, UserId).</summary>
public class TeamMembership
{
    public Guid TeamId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    public Team? Team { get; set; }
    public User? User { get; set; }
}

namespace TaskManager.Api.Domain;

public class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = default!;

    /// <summary>A user's auto-created private team (one member; can't add members, leave, or delete).</summary>
    public bool IsPersonal { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<TeamMembership> Memberships { get; set; } = new List<TeamMembership>();
}

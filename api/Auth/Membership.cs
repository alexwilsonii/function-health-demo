using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;

namespace TaskManager.Api.Auth;

/// <summary>
/// The current user's team membership — the basis of task visibility now that tasks belong to teams.
/// Replaces the old per-user global query filter: endpoints scope explicitly to these team ids.
/// Cached per request (scoped service).
/// </summary>
public interface IMembership
{
    Task<IReadOnlyList<Guid>> TeamIdsAsync();
    Task<bool> IsMemberAsync(Guid teamId);
}

public sealed class Membership : IMembership
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private IReadOnlyList<Guid>? _cache;

    public Membership(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<IReadOnlyList<Guid>> TeamIdsAsync()
    {
        if (_cache is not null) return _cache;
        if (_current.UserId is not { } uid) return _cache = Array.Empty<Guid>();

        _cache = await _db.TeamMemberships
            .Where(m => m.UserId == uid)
            .Select(m => m.TeamId)
            .ToListAsync();
        return _cache;
    }

    public async Task<bool> IsMemberAsync(Guid teamId) => (await TeamIdsAsync()).Contains(teamId);
}

using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Auth;
using TaskManager.Api.Contracts;
using TaskManager.Api.Data;
using TaskManager.Api.Domain;
using TaskManager.Api.Validation;

namespace TaskManager.Api.Endpoints;

public static class TeamEndpoints
{
    public static void MapTeamEndpoints(this IEndpointRouteBuilder app)
    {
        var audit = app.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Audit");
        var group = app.MapGroup("/api/teams").WithTags("Teams").RequireAuthorization();

        // My teams
        group.MapGet("", async (AppDbContext db, ICurrentUser current) =>
        {
            var uid = current.UserId!.Value;
            var teams = await db.TeamMemberships
                .Where(m => m.UserId == uid)
                .OrderByDescending(m => m.Team!.IsPersonal)
                .ThenBy(m => m.Team!.Name)
                .Select(m => new TeamResponse(m.Team!.Id, m.Team.Name, m.Team.IsPersonal, m.Team.Memberships.Count))
                .ToListAsync();
            return Results.Ok(teams);
        });

        // Create a team (creator becomes the first member)
        group.MapPost("", async (CreateTeamRequest req, AppDbContext db, ICurrentUser current) =>
        {
            var uid = current.UserId!.Value;
            var team = new Team { Name = req.Name.Trim(), IsPersonal = false, CreatedByUserId = uid };
            db.Teams.Add(team);
            db.TeamMemberships.Add(new TeamMembership { TeamId = team.Id, UserId = uid });
            await db.SaveChangesAsync();
            audit.LogInformation("Team created {TeamId} by {UserId}", team.Id, uid);
            return Results.Created($"/api/teams/{team.Id}", new TeamResponse(team.Id, team.Name, team.IsPersonal, 1));
        })
        .AddEndpointFilter<ValidationFilter<CreateTeamRequest>>();

        // Team detail + members (members only)
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, IMembership membership) =>
        {
            if (!await membership.IsMemberAsync(id)) return Results.NotFound();
            var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == id);
            if (team is null) return Results.NotFound();

            var members = await db.TeamMemberships
                .Where(m => m.TeamId == id)
                .OrderBy(m => m.JoinedAt)
                .Select(m => new MemberResponse(m.UserId, m.User!.Email, m.JoinedAt))
                .ToListAsync();

            return Results.Ok(new TeamDetailResponse(team.Id, team.Name, team.IsPersonal, members));
        });

        // Add a member by email (any member; not the Personal team)
        group.MapPost("/{id:guid}/members", async (Guid id, AddMemberRequest req, AppDbContext db, IMembership membership, ICurrentUser current) =>
        {
            if (!await membership.IsMemberAsync(id)) return Results.NotFound();
            var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == id);
            if (team is null) return Results.NotFound();
            if (team.IsPersonal) return Bad("You can't add members to your Personal team.");

            var email = req.Email.Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user is null) return Field("email", "No user is registered with that email.");
            if (await db.TeamMemberships.AnyAsync(m => m.TeamId == id && m.UserId == user.Id))
                return Field("email", "That user is already a member.");

            db.TeamMemberships.Add(new TeamMembership { TeamId = id, UserId = user.Id });
            await db.SaveChangesAsync();
            audit.LogInformation("Member {AddedUserId} added to team {TeamId} by {UserId}", user.Id, id, current.UserId);
            return Results.Created($"/api/teams/{id}", new MemberResponse(user.Id, user.Email, DateTimeOffset.UtcNow));
        })
        .AddEndpointFilter<ValidationFilter<AddMemberRequest>>();

        // Leave a team (only if at least one other member remains; not Personal)
        group.MapDelete("/{id:guid}/members/me", async (Guid id, AppDbContext db, IMembership membership, ICurrentUser current) =>
        {
            var uid = current.UserId!.Value;
            if (!await membership.IsMemberAsync(id)) return Results.NotFound();
            var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == id);
            if (team is null) return Results.NotFound();
            if (team.IsPersonal) return Bad("You can't leave your Personal team.");
            if (await db.TeamMemberships.CountAsync(m => m.TeamId == id) <= 1)
                return Bad("You're the last member — delete the team instead.");

            // Drop this user's assignments on the team's tasks so nothing stays assigned to a non-member.
            var assigned = await db.Tasks.Where(t => t.TeamId == id && t.AssigneeUserId == uid).ToListAsync();
            foreach (var t in assigned) t.AssigneeUserId = null;

            var m = await db.TeamMemberships.FirstAsync(x => x.TeamId == id && x.UserId == uid);
            db.TeamMemberships.Remove(m);
            await db.SaveChangesAsync();
            audit.LogInformation("User {UserId} left team {TeamId}", uid, id);
            return Results.NoContent();
        });

        // Delete a team (sole member only; not Personal). Cascades tasks + comments + memberships.
        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, IMembership membership, ICurrentUser current) =>
        {
            if (!await membership.IsMemberAsync(id)) return Results.NotFound();
            var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == id);
            if (team is null) return Results.NotFound();
            if (team.IsPersonal) return Bad("You can't delete your Personal team.");
            if (await db.TeamMemberships.CountAsync(m => m.TeamId == id) > 1)
                return Bad("Only a team's sole member can delete it — others must leave first.");

            db.Teams.Remove(team);
            await db.SaveChangesAsync();
            audit.LogInformation("Team deleted {TeamId} by {UserId}", id, current.UserId);
            return Results.NoContent();
        });
    }

    private static IResult Bad(string message) =>
        Results.Problem(title: message, statusCode: StatusCodes.Status400BadRequest);

    private static IResult Field(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = new[] { message } });
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TaskManager.Tests;

/// <summary>Teams, membership management, and assignment rules.</summary>
public class TeamTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public TeamTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task New_user_gets_a_personal_team()
    {
        var client = await _factory.RegisterClientAsync();

        var teams = await (await client.GetAsync("/api/teams")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Contains(teams.EnumerateArray(), t => t.GetProperty("isPersonal").GetBoolean());
    }

    [Fact]
    public async Task Add_member_by_email_grants_visibility()
    {
        var owner = await _factory.RegisterClientAsync();
        var mate = await _factory.RegisterClientAsync();
        var team = await owner.CreateTeamAsync("Team A");
        var id = await owner.CreateTaskAsync(team, "Team task");

        Assert.Equal(HttpStatusCode.NotFound, (await mate.GetAsync($"/api/tasks/{id}")).StatusCode);

        (await owner.AddMemberAsync(team, await mate.EmailAsync())).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, (await mate.GetAsync($"/api/tasks/{id}")).StatusCode);
    }

    [Fact]
    public async Task Add_member_with_unknown_email_is_rejected()
    {
        var owner = await _factory.RegisterClientAsync();
        var team = await owner.CreateTeamAsync();

        var res = await owner.AddMemberAsync(team, "nobody@example.com");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Non_member_cannot_add_to_a_team()
    {
        var owner = await _factory.RegisterClientAsync();
        var outsider = await _factory.RegisterClientAsync();
        var team = await owner.CreateTeamAsync();

        var res = await outsider.AddMemberAsync(team, await outsider.EmailAsync());

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Cannot_add_members_to_the_personal_team()
    {
        var owner = await _factory.RegisterClientAsync();
        var mate = await _factory.RegisterClientAsync();
        var personal = await owner.PersonalTeamIdAsync();

        var res = await owner.AddMemberAsync(personal, await mate.EmailAsync());

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Last_member_cannot_leave_but_can_delete()
    {
        var owner = await _factory.RegisterClientAsync();
        var team = await owner.CreateTeamAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await owner.DeleteAsync($"/api/teams/{team}/members/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/teams/{team}")).StatusCode);
    }

    [Fact]
    public async Task Member_can_leave_when_others_remain()
    {
        var owner = await _factory.RegisterClientAsync();
        var mate = await _factory.RegisterClientAsync();
        var team = await owner.CreateTeamAsync();
        (await owner.AddMemberAsync(team, await mate.EmailAsync())).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NoContent, (await mate.DeleteAsync($"/api/teams/{team}/members/me")).StatusCode);

        var teams = await (await mate.GetAsync("/api/teams")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(teams.EnumerateArray(), t => t.GetProperty("id").GetGuid() == team);
    }

    [Fact]
    public async Task Cannot_create_a_task_in_a_team_you_are_not_in()
    {
        var owner = await _factory.RegisterClientAsync();
        var outsider = await _factory.RegisterClientAsync();
        var team = await owner.CreateTeamAsync();

        var res = await outsider.PostAsJsonAsync("/api/tasks", new { teamId = team, title = "sneaky" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Assignee_must_be_a_team_member()
    {
        var owner = await _factory.RegisterClientAsync();
        var outsider = await _factory.RegisterClientAsync();
        var team = await owner.CreateTeamAsync();

        var res = await owner.PostAsJsonAsync("/api/tasks",
            new { teamId = team, title = "x", assigneeUserId = await outsider.UserIdAsync() });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Assigned_task_shows_for_assignee_under_assignee_me_filter()
    {
        var owner = await _factory.RegisterClientAsync();
        var mate = await _factory.RegisterClientAsync();
        var team = await owner.CreateTeamAsync();
        (await owner.AddMemberAsync(team, await mate.EmailAsync())).EnsureSuccessStatusCode();

        await owner.CreateTaskAsync(new { teamId = team, title = "For mate", assigneeUserId = await mate.UserIdAsync() });

        var forMate = await (await mate.GetAsync("/api/tasks?assignee=me")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(forMate.EnumerateArray(), t => t.GetProperty("title").GetString() == "For mate");
    }
}

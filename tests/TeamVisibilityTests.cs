using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TaskManager.Tests;

/// <summary>
/// Visibility is team-based: members of a task's team can see and edit it; non-members cannot (404,
/// no existence leak); and only the creator may delete it (403 for a teammate who can see it).
/// </summary>
public class TeamVisibilityTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public TeamVisibilityTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Non_member_cannot_see_get_patch_or_delete_a_task()
    {
        var a = await _factory.RegisterClientAsync();
        var b = await _factory.RegisterClientAsync();
        var team = await a.PersonalTeamIdAsync();
        var id = await a.CreateTaskAsync(team, "A's private task");

        var bList = await (await b.GetAsync("/api/tasks")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(id, bList.EnumerateArray().Select(t => t.GetProperty("id").GetGuid()));
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/tasks/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PatchJsonAsync($"/api/tasks/{id}", TestHelpers.Update())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/tasks/{id}")).StatusCode);
    }

    [Fact]
    public async Task Team_member_can_see_and_edit_but_only_creator_can_delete()
    {
        var creator = await _factory.RegisterClientAsync();
        var mate = await _factory.RegisterClientAsync();

        var team = await creator.CreateTeamAsync("Shared");
        (await creator.AddMemberAsync(team, await mate.EmailAsync())).EnsureSuccessStatusCode();
        var id = await creator.CreateTaskAsync(team, "Shared task");

        // Member can see + edit it.
        Assert.Equal(HttpStatusCode.OK, (await mate.GetAsync($"/api/tasks/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await mate.PatchJsonAsync($"/api/tasks/{id}", TestHelpers.Update(title: "Edited by mate"))).StatusCode);

        // Member is not the creator → cannot delete (403, not 404, since they can see it).
        Assert.Equal(HttpStatusCode.Forbidden, (await mate.DeleteAsync($"/api/tasks/{id}")).StatusCode);

        // Creator can delete.
        Assert.Equal(HttpStatusCode.NoContent, (await creator.DeleteAsync($"/api/tasks/{id}")).StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_request_is_rejected_401()
    {
        var anon = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/tasks")).StatusCode);
    }
}

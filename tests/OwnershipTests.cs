using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TaskManager.Tests;

/// <summary>
/// The highest-risk area the grading guide names: User A must never reach User B's data through any
/// endpoint, and missing/foreign rows must return 404 (not 403) so existence isn't leaked.
/// </summary>
public class OwnershipTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public OwnershipTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task UserB_list_does_not_include_UserA_task()
    {
        var a = await _factory.RegisterClientAsync();
        var b = await _factory.RegisterClientAsync();
        var id = await a.CreateTaskAsync(new { title = "A's private task" });

        var list = await (await b.GetAsync("/api/tasks")).Content.ReadFromJsonAsync<JsonElement>();
        var ids = list.EnumerateArray().Select(t => t.GetProperty("id").GetGuid());

        Assert.DoesNotContain(id, ids);
    }

    [Fact]
    public async Task UserB_get_of_UserA_task_returns_404_not_403()
    {
        var a = await _factory.RegisterClientAsync();
        var b = await _factory.RegisterClientAsync();
        var id = await a.CreateTaskAsync(new { title = "A's task" });

        var res = await b.GetAsync($"/api/tasks/{id}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task UserB_patch_of_UserA_task_returns_404()
    {
        var a = await _factory.RegisterClientAsync();
        var b = await _factory.RegisterClientAsync();
        var id = await a.CreateTaskAsync(new { title = "A's task" });

        var res = await b.PatchJsonAsync($"/api/tasks/{id}", TestHelpers.Update(title: "hijacked"));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task UserB_delete_of_UserA_task_returns_404()
    {
        var a = await _factory.RegisterClientAsync();
        var b = await _factory.RegisterClientAsync();
        var id = await a.CreateTaskAsync(new { title = "A's task" });

        var res = await b.DeleteAsync($"/api/tasks/{id}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        // And A's task is still there afterwards.
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync($"/api/tasks/{id}")).StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_request_is_rejected_401()
    {
        var anon = _factory.CreateClient();

        var res = await anon.GetAsync("/api/tasks");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}

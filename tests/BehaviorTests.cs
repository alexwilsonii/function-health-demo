using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TaskManager.Tests;

/// <summary>Correctness invariants that are easy to break: completion timestamps, sort ordering, auth.</summary>
public class BehaviorTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public BehaviorTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Completing_a_task_sets_CompletedAt_and_reopening_clears_it()
    {
        var client = await _factory.RegisterClientAsync();
        var id = await client.CreateTaskAsync(new { title = "Toggle me", status = "Todo" });

        var done = await (await client.PatchJsonAsync($"/api/tasks/{id}", TestHelpers.Update(title: "Toggle me", status: "Done")))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(JsonValueKind.Null, done.GetProperty("completedAt").ValueKind);

        var reopened = await (await client.PatchJsonAsync($"/api/tasks/{id}", TestHelpers.Update(title: "Toggle me", status: "Todo")))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, reopened.GetProperty("completedAt").ValueKind);
    }

    [Fact]
    public async Task Editing_a_task_bumps_UpdatedAt()
    {
        var client = await _factory.RegisterClientAsync();
        var created = await (await client.PostAsJsonAsync("/api/tasks", new { title = "Original" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        var before = created.GetProperty("updatedAt").GetDateTimeOffset();

        await Task.Delay(10);
        var edited = await (await client.PatchJsonAsync($"/api/tasks/{id}", TestHelpers.Update(title: "Edited")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var after = edited.GetProperty("updatedAt").GetDateTimeOffset();

        Assert.True(after > before);
    }

    [Fact]
    public async Task Sort_by_due_returns_chronological_order()
    {
        var client = await _factory.RegisterClientAsync();
        await client.CreateTaskAsync(new { title = "late", dueAt = "2026-12-01T00:00:00Z" });
        await client.CreateTaskAsync(new { title = "early", dueAt = "2026-01-01T00:00:00Z" });
        await client.CreateTaskAsync(new { title = "mid", dueAt = "2026-06-01T00:00:00Z" });

        var list = await (await client.GetAsync("/api/tasks?sort=due")).Content.ReadFromJsonAsync<JsonElement>();
        var titles = list.EnumerateArray().Select(t => t.GetProperty("title").GetString()).ToList();

        Assert.Equal(new[] { "early", "mid", "late" }, titles);
    }

    [Fact]
    public async Task Search_matches_title_case_insensitively()
    {
        var client = await _factory.RegisterClientAsync();
        await client.CreateTaskAsync(new { title = "Buy MILK" });
        await client.CreateTaskAsync(new { title = "Walk dog" });

        var list = await (await client.GetAsync("/api/tasks?q=milk")).Content.ReadFromJsonAsync<JsonElement>();
        var titles = list.EnumerateArray().Select(t => t.GetProperty("title").GetString()).ToList();

        Assert.Single(titles);
        Assert.Equal("Buy MILK", titles[0]);
    }

    [Fact]
    public async Task Duplicate_registration_is_rejected()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var client = _factory.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/register", new { email, password = "password123" })).EnsureSuccessStatusCode();

        var dup = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "password123" });

        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var client = _factory.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/register", new { email, password = "password123" })).EnsureSuccessStatusCode();

        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}

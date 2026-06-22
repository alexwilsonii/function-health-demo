using System.Net.Http.Json;
using System.Text.Json;

namespace TaskManager.Tests;

public static class TestHelpers
{
    /// <summary>Registers a fresh user (unique email unless given) and returns an authenticated client.</summary>
    public static async Task<HttpClient> RegisterClientAsync(this ApiFactory factory, string? email = null)
    {
        var client = factory.CreateClient();
        email ??= $"{Guid.NewGuid():N}@example.com";
        var res = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "password123" });
        res.EnsureSuccessStatusCode();
        return client;
    }

    public static async Task<Guid> UserIdAsync(this HttpClient client) => (await Me(client)).GetProperty("id").GetGuid();

    public static async Task<string> EmailAsync(this HttpClient client) => (await Me(client)).GetProperty("email").GetString()!;

    private static async Task<JsonElement> Me(HttpClient client) =>
        await (await client.GetAsync("/api/auth/me")).Content.ReadFromJsonAsync<JsonElement>();

    public static async Task<Guid> PersonalTeamIdAsync(this HttpClient client)
    {
        var teams = await (await client.GetAsync("/api/teams")).Content.ReadFromJsonAsync<JsonElement>();
        return teams.EnumerateArray().First(t => t.GetProperty("isPersonal").GetBoolean()).GetProperty("id").GetGuid();
    }

    public static async Task<Guid> CreateTeamAsync(this HttpClient client, string name = "Team")
    {
        var res = await client.PostAsJsonAsync("/api/teams", new { name });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    public static Task<HttpResponseMessage> AddMemberAsync(this HttpClient client, Guid teamId, string email) =>
        client.PostAsJsonAsync($"/api/teams/{teamId}/members", new { email });

    public static async Task<Guid> CreateTaskAsync(this HttpClient client, object payload)
    {
        var res = await client.PostAsJsonAsync("/api/tasks", payload);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    public static Task<Guid> CreateTaskAsync(this HttpClient client, Guid teamId, string title = "Task") =>
        client.CreateTaskAsync(new { teamId, title });

    public static Task<HttpResponseMessage> PatchJsonAsync(this HttpClient client, string url, object payload) =>
        client.PatchAsync(url, JsonContent.Create(payload));

    /// <summary>A complete, valid UpdateTaskRequest body (PATCH carries the full editable state).</summary>
    public static object Update(
        string title = "Task",
        string? notes = null,
        string status = "Todo",
        string priority = "Medium",
        string? dueAt = null,
        bool isPinned = false,
        Guid? assigneeUserId = null) =>
        new { title, notes, status, priority, dueAt, isPinned, assigneeUserId };
}

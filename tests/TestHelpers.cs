using System.Net.Http.Json;
using System.Text.Json;

namespace TaskManager.Tests;

public static class TestHelpers
{
    /// <summary>Registers a fresh user (unique email) and returns an authenticated client (cookie set).</summary>
    public static async Task<HttpClient> RegisterClientAsync(this ApiFactory factory, string? email = null)
    {
        var client = factory.CreateClient();
        email ??= $"{Guid.NewGuid():N}@example.com";
        var res = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "password123" });
        res.EnsureSuccessStatusCode();
        return client;
    }

    public static async Task<Guid> CreateTaskAsync(this HttpClient client, object payload)
    {
        var res = await client.PostAsJsonAsync("/api/tasks", payload);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetGuid();
    }

    public static Task<HttpResponseMessage> PatchJsonAsync(this HttpClient client, string url, object payload) =>
        client.PatchAsync(url, JsonContent.Create(payload));

    /// <summary>A complete, valid UpdateTaskRequest body (PATCH carries the full editable state).</summary>
    public static object Update(
        string title = "Task",
        string? notes = null,
        string status = "Todo",
        string priority = "Medium",
        string? dueAt = null,
        bool isPinned = false) =>
        new { title, notes, status, priority, dueAt, isPinned };
}

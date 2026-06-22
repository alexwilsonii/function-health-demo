using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TaskManager.Tests;

/// <summary>Comments are reached only through the owning task, so team visibility applies to them too.</summary>
public class CommentTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CommentTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Add_then_list_returns_the_trimmed_comment_with_author()
    {
        var client = await _factory.RegisterClientAsync();
        var taskId = await client.CreateTaskAsync(await client.PersonalTeamIdAsync(), "T");

        var add = await client.PostAsJsonAsync($"/api/tasks/{taskId}/comments", new { body = "  hello  " });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);

        var list = await (await client.GetAsync($"/api/tasks/{taskId}/comments")).Content.ReadFromJsonAsync<JsonElement>();
        var first = list.EnumerateArray().Single();

        Assert.Equal("hello", first.GetProperty("body").GetString());
        Assert.Equal(await client.EmailAsync(), first.GetProperty("authorEmail").GetString());
    }

    [Fact]
    public async Task Empty_comment_is_rejected_with_field_error()
    {
        var client = await _factory.RegisterClientAsync();
        var taskId = await client.CreateTaskAsync(await client.PersonalTeamIdAsync(), "T");

        var res = await client.PostAsJsonAsync($"/api/tasks/{taskId}/comments", new { body = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("body", out _));
    }

    [Fact]
    public async Task Delete_removes_the_comment()
    {
        var client = await _factory.RegisterClientAsync();
        var taskId = await client.CreateTaskAsync(await client.PersonalTeamIdAsync(), "T");
        var created = await (await client.PostAsJsonAsync($"/api/tasks/{taskId}/comments", new { body = "x" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var commentId = created.GetProperty("id").GetGuid();

        var del = await client.DeleteAsync($"/api/tasks/{taskId}/comments/{commentId}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var list = await (await client.GetAsync($"/api/tasks/{taskId}/comments")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(list.EnumerateArray());
    }

    [Fact]
    public async Task Non_member_cannot_list_or_add_comments_on_a_task()
    {
        var a = await _factory.RegisterClientAsync();
        var b = await _factory.RegisterClientAsync();
        var taskId = await a.CreateTaskAsync(await a.PersonalTeamIdAsync(), "A's task");
        await a.PostAsJsonAsync($"/api/tasks/{taskId}/comments", new { body = "secret" });

        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/tasks/{taskId}/comments")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await b.PostAsJsonAsync($"/api/tasks/{taskId}/comments", new { body = "sneaky" })).StatusCode);
    }
}

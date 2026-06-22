using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Api.Data;

namespace TaskManager.Tests;

/// <summary>
/// The second highest-risk area: bad input is rejected with the single 400 ValidationProblemDetails
/// envelope — including binding failures (invalid enum / malformed JSON), not just field rules.
/// </summary>
public class ValidationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ValidationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Empty_title_is_rejected_with_field_error()
    {
        var client = await _factory.RegisterClientAsync();
        var team = await client.PersonalTeamIdAsync();

        var res = await client.PostAsJsonAsync("/api/tasks", new { teamId = team, title = "" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("title", out _));
    }

    [Fact]
    public async Task Whitespace_only_title_is_rejected()
    {
        var client = await _factory.RegisterClientAsync();
        var team = await client.PersonalTeamIdAsync();

        var res = await client.PostAsJsonAsync("/api/tasks", new { teamId = team, title = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Title_over_200_chars_is_rejected()
    {
        var client = await _factory.RegisterClientAsync();
        var team = await client.PersonalTeamIdAsync();

        var res = await client.PostAsJsonAsync("/api/tasks", new { teamId = team, title = new string('a', 201) });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Missing_team_is_rejected_with_field_error()
    {
        var client = await _factory.RegisterClientAsync();

        var res = await client.PostAsJsonAsync("/api/tasks", new { title = "No team" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("teamId", out _));
    }

    [Fact]
    public async Task Invalid_enum_value_returns_same_400_envelope()
    {
        var client = await _factory.RegisterClientAsync();
        var content = new StringContent("""{"title":"x","status":"Nope"}""", Encoding.UTF8, "application/json");

        var res = await client.PostAsync("/api/tasks", content);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out _)); // same ValidationProblemDetails shape
    }

    [Fact]
    public async Task Malformed_json_returns_same_400_envelope()
    {
        var client = await _factory.RegisterClientAsync();
        var content = new StringContent("{ not valid json ", Encoding.UTF8, "application/json");

        var res = await client.PostAsync("/api/tasks", content);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Valid_task_is_created_and_persists_across_a_fresh_dbcontext()
    {
        var client = await _factory.RegisterClientAsync();
        var team = await client.PersonalTeamIdAsync();

        var id = await client.CreateTaskAsync(new { teamId = team, title = "Persist me", priority = "High" });

        // Brand-new DbContext from the DI container — proves the row physically persisted.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await db.Tasks.SingleOrDefaultAsync(t => t.Id == id);

        Assert.NotNull(persisted);
        Assert.Equal("Persist me", persisted!.Title);
    }
}

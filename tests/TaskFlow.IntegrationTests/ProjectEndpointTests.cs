using System.Net;
using System.Text.Json;
using TaskFlow.IntegrationTests.Fixtures;
using TaskFlow.IntegrationTests.Helpers;

namespace TaskFlow.IntegrationTests;

/// <summary>
/// Tests the REST CRUD endpoints at /api/projects.
/// Each test is independent — unique data is generated to avoid cross-test collisions.
/// </summary>
public class ProjectEndpointTests(TaskFlowFactory factory) : IClassFixture<TaskFlowFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static object NewProjectBody(string? name = null) => new
    {
        workspaceId = "000000000000000000000001",
        name = name ?? $"Project-{Guid.NewGuid():N}",
        description = "A test project",
        ownerId = "000000000000000000000002"
    };

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_project_returns_201_with_location_header()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", NewProjectBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_project_returns_project_with_generated_id()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", NewProjectBody("Alpha"));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("name").GetString().Should().Be("Alpha");
    }

    [Fact]
    public async Task Create_project_defaults_to_active_status()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", NewProjectBody());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("Active");
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_project_by_id_returns_200()
    {
        var created = await _client.PostAsJsonAsync("/api/projects", NewProjectBody("Beta"));
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = createdBody.GetProperty("id").GetString()!;

        var response = await _client.GetAsync($"/api/projects/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().Be(id);
    }

    [Fact]
    public async Task Get_project_by_id_returns_404_for_unknown_id()
    {
        var response = await _client.GetAsync("/api/projects/000000000000000000000099");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_all_projects_returns_200_and_includes_created_project()
    {
        var uniqueName = $"AllTest-{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/api/projects", NewProjectBody(uniqueName));

        var response = await _client.GetAsync("/api/projects");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(uniqueName);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_project_returns_200_with_updated_fields()
    {
        var created = await _client.PostAsJsonAsync("/api/projects", NewProjectBody("Original"));
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = createdBody.GetProperty("id").GetString()!;

        var updateResponse = await _client.PutAsJsonAsync($"/api/projects/{id}", new
        {
            name = "Updated Name",
            description = "Updated description",
            status = "Completed"
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("Updated Name");
        body.GetProperty("status").GetString().Should().Be("Completed");
    }

    [Fact]
    public async Task Update_project_returns_404_for_unknown_id()
    {
        var response = await _client.PutAsJsonAsync("/api/projects/000000000000000000000099", new
        {
            name = "Ghost",
            description = "Does not exist",
            status = "Active"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_project_returns_204()
    {
        var created = await _client.PostAsJsonAsync("/api/projects", NewProjectBody("ToDelete"));
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = createdBody.GetProperty("id").GetString()!;

        var deleteResponse = await _client.DeleteAsync($"/api/projects/{id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_project_means_it_is_no_longer_retrievable()
    {
        var created = await _client.PostAsJsonAsync("/api/projects", NewProjectBody("Gone"));
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = createdBody.GetProperty("id").GetString()!;

        await _client.DeleteAsync($"/api/projects/{id}");
        var getResponse = await _client.GetAsync($"/api/projects/{id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_project_returns_404_for_unknown_id()
    {
        var response = await _client.DeleteAsync("/api/projects/000000000000000000000099");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

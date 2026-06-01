using System.Net;
using System.Text.Json;
using TaskFlow.IntegrationTests.Fixtures;

namespace TaskFlow.IntegrationTests;

/// <summary>
/// Tests the REST CRUD endpoints at /api/tasks.
/// Also covers the projectId query-string filter.
/// </summary>
public class TaskEndpointTests(TaskFlowFactory factory) : IClassFixture<TaskFlowFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static readonly string ProjectId = "000000000000000000000010";

    private static object NewTaskBody(string? title = null, string? projectId = null) => new
    {
        projectId = projectId ?? ProjectId,
        title = title ?? $"Task-{Guid.NewGuid():N}",
        description = "A test task",
        priority = "Medium",
        assigneeId = (string?)null,
        dueDate = (DateTime?)null,
        tags = (List<string>?)null
    };

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_task_returns_201_with_location_header()
    {
        var response = await _client.PostAsJsonAsync("/api/tasks", NewTaskBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_task_returns_task_with_generated_id_and_todo_status()
    {
        var response = await _client.PostAsJsonAsync("/api/tasks", NewTaskBody("My Task"));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("title").GetString().Should().Be("My Task");
        body.GetProperty("status").GetString().Should().Be("Todo");
    }

    [Fact]
    public async Task Create_task_with_tags_stores_tags()
    {
        var response = await _client.PostAsJsonAsync("/api/tasks", new
        {
            projectId = ProjectId,
            title = "Tagged Task",
            description = "Has tags",
            priority = "High",
            assigneeId = (string?)null,
            dueDate = (DateTime?)null,
            tags = new[] { "backend", "urgent" }
        });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var tags = body.GetProperty("tags").EnumerateArray()
            .Select(t => t.GetString()).ToList();
        tags.Should().Contain("backend");
        tags.Should().Contain("urgent");
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_task_by_id_returns_200()
    {
        var created = await _client.PostAsJsonAsync("/api/tasks", NewTaskBody());
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = createdBody.GetProperty("id").GetString()!;

        var response = await _client.GetAsync($"/api/tasks/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().Be(id);
    }

    [Fact]
    public async Task Get_task_by_id_returns_404_for_unknown_id()
    {
        var response = await _client.GetAsync("/api/tasks/000000000000000000000099");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_tasks_filtered_by_projectId_returns_only_that_projects_tasks()
    {
        var projectA = $"aaa{Guid.NewGuid():N}"[..24];
        var projectB = $"bbb{Guid.NewGuid():N}"[..24];

        await _client.PostAsJsonAsync("/api/tasks", NewTaskBody("Task A1", projectA));
        await _client.PostAsJsonAsync("/api/tasks", NewTaskBody("Task A2", projectA));
        await _client.PostAsJsonAsync("/api/tasks", NewTaskBody("Task B1", projectB));

        var response = await _client.GetAsync($"/api/tasks?projectId={projectA}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Task A1");
        body.Should().Contain("Task A2");
        body.Should().NotContain("Task B1");
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_task_changes_status_to_in_progress()
    {
        var created = await _client.PostAsJsonAsync("/api/tasks", NewTaskBody("Move Me"));
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = createdBody.GetProperty("id").GetString()!;

        var updateResponse = await _client.PutAsJsonAsync($"/api/tasks/{id}", new
        {
            title = "Move Me",
            description = "Moving",
            status = "InProgress",
            priority = "High",
            assigneeId = (string?)null,
            dueDate = (DateTime?)null,
            tags = (List<string>?)null
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("InProgress");
    }

    [Fact]
    public async Task Update_task_sets_updatedAt_to_recent_time()
    {
        var created = await _client.PostAsJsonAsync("/api/tasks", NewTaskBody("Timestamp Test"));
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = createdBody.GetProperty("id").GetString()!;
        var before = DateTime.UtcNow.AddSeconds(-1);

        await _client.PutAsJsonAsync($"/api/tasks/{id}", new
        {
            title = "Timestamp Test",
            description = "Updated",
            status = "Done",
            priority = "Low",
            assigneeId = (string?)null,
            dueDate = (DateTime?)null,
            tags = (List<string>?)null
        });

        var getResponse = await _client.GetAsync($"/api/tasks/{id}");
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        var updatedAt = body.GetProperty("updatedAt").GetDateTime();

        updatedAt.Should().BeAfter(before,
            "because the server must set updatedAt server-side on every update");
    }

    [Fact]
    public async Task Update_task_returns_404_for_unknown_id()
    {
        var response = await _client.PutAsJsonAsync("/api/tasks/000000000000000000000099", new
        {
            title = "Ghost",
            description = "Not found",
            status = "Todo",
            priority = "Low",
            assigneeId = (string?)null,
            dueDate = (DateTime?)null,
            tags = (List<string>?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_task_returns_204()
    {
        var created = await _client.PostAsJsonAsync("/api/tasks", NewTaskBody("DeleteMe"));
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = createdBody.GetProperty("id").GetString()!;

        var deleteResponse = await _client.DeleteAsync($"/api/tasks/{id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_task_means_it_is_no_longer_retrievable()
    {
        var created = await _client.PostAsJsonAsync("/api/tasks", NewTaskBody("Gone Task"));
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = createdBody.GetProperty("id").GetString()!;

        await _client.DeleteAsync($"/api/tasks/{id}");
        var getResponse = await _client.GetAsync($"/api/tasks/{id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_task_returns_404_for_unknown_id()
    {
        var response = await _client.DeleteAsync("/api/tasks/000000000000000000000099");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

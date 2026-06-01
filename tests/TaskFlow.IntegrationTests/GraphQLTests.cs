using System.Net;
using System.Text.Json;
using TaskFlow.IntegrationTests.Fixtures;

namespace TaskFlow.IntegrationTests;

/// <summary>
/// Tests the GraphQL endpoint at /graphql.
/// Covers queries, mutations, and error handling.
/// GraphQL always returns HTTP 200; errors are inside the response body.
/// </summary>
public class GraphQLTests(TaskFlowFactory factory) : IClassFixture<TaskFlowFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private Task<HttpResponseMessage> PostGraphQL(string query, object? variables = null) =>
        _client.PostAsJsonAsync("/graphql", new { query, variables });

    // ── Basic connectivity ────────────────────────────────────────────────────

    [Fact]
    public async Task GraphQL_endpoint_returns_200_for_valid_query()
    {
        var response = await PostGraphQL("{ __typename }");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GraphQL_returns_data_not_errors_for_valid_query()
    {
        var response = await PostGraphQL("{ __typename }");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.TryGetProperty("data", out _).Should().BeTrue();
        body.TryGetProperty("errors", out _).Should().BeFalse();
    }

    // ── Workspace mutations and queries ───────────────────────────────────────

    [Fact]
    public async Task CreateWorkspace_mutation_creates_and_returns_workspace()
    {
        var name = $"WS-{Guid.NewGuid():N[..8]}";
        var response = await PostGraphQL($$"""
            mutation {
                createWorkspace(input: {
                    name: "{{name}}",
                    ownerId: "000000000000000000000001"
                }) {
                    id
                    name
                }
            }
            """);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("errors", out _).Should().BeFalse();

        var workspace = body.GetProperty("data").GetProperty("createWorkspace");
        workspace.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        workspace.GetProperty("name").GetString().Should().Be(name);
    }

    [Fact]
    public async Task Workspaces_query_returns_list_including_newly_created()
    {
        var name = $"Queryable-{Guid.NewGuid():N[..8]}";
        await PostGraphQL($$"""
            mutation {
                createWorkspace(input: {
                    name: "{{name}}",
                    ownerId: "000000000000000000000001"
                }) { id }
            }
            """);

        var queryResponse = await PostGraphQL("{ workspaces { id name } }");
        var body = await queryResponse.Content.ReadFromJsonAsync<JsonElement>();
        var json = await queryResponse.Content.ReadAsStringAsync();

        json.Should().Contain(name);
    }

    // ── Project mutations ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateProject_mutation_returns_project_with_active_status()
    {
        // First create a workspace to get a valid ID
        var wsResponse = await PostGraphQL("""
            mutation {
                createWorkspace(input: {
                    name: "ProjectTestWS",
                    ownerId: "000000000000000000000001"
                }) { id }
            }
            """);
        var wsBody = await wsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var workspaceId = wsBody.GetProperty("data")
            .GetProperty("createWorkspace")
            .GetProperty("id").GetString()!;

        var projectName = $"Proj-{Guid.NewGuid():N[..8]}";
        var projResponse = await PostGraphQL($$"""
            mutation {
                createProject(input: {
                    workspaceId: "{{workspaceId}}",
                    name: "{{projectName}}",
                    description: "Test project",
                    ownerId: "000000000000000000000001"
                }) {
                    id
                    name
                    status
                }
            }
            """);

        var projBody = await projResponse.Content.ReadFromJsonAsync<JsonElement>();
        projBody.TryGetProperty("errors", out _).Should().BeFalse();

        var project = projBody.GetProperty("data").GetProperty("createProject");
        project.GetProperty("name").GetString().Should().Be(projectName);
        project.GetProperty("status").GetString().Should().Be("ACTIVE");
    }

    // ── Task mutations ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTask_mutation_returns_task_with_todo_status()
    {
        var response = await PostGraphQL("""
            mutation {
                createTask(input: {
                    projectId: "000000000000000000000010",
                    title: "GraphQL Task",
                    description: "Created via mutation",
                    priority: MEDIUM
                }) {
                    id
                    title
                    status
                    priority
                }
            }
            """);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("errors", out _).Should().BeFalse();

        var task = body.GetProperty("data").GetProperty("createTask");
        task.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        task.GetProperty("title").GetString().Should().Be("GraphQL Task");
        task.GetProperty("status").GetString().Should().Be("TODO");
        task.GetProperty("priority").GetString().Should().Be("MEDIUM");
    }

    [Fact]
    public async Task UpdateTask_mutation_changes_task_status()
    {
        // Create a task first
        var createResponse = await PostGraphQL("""
            mutation {
                createTask(input: {
                    projectId: "000000000000000000000010",
                    title: "Will Be Updated",
                    description: "",
                    priority: LOW
                }) { id }
            }
            """);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var taskId = createBody.GetProperty("data").GetProperty("createTask")
            .GetProperty("id").GetString()!;

        // Update status to DONE
        var updateResponse = await PostGraphQL($$"""
            mutation {
                updateTask(input: {
                    id: "{{taskId}}",
                    title: "Will Be Updated",
                    description: "",
                    status: DONE,
                    priority: LOW
                }) {
                    id
                    status
                }
            }
            """);

        var updateBody = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        updateBody.TryGetProperty("errors", out _).Should().BeFalse();

        var updated = updateBody.GetProperty("data").GetProperty("updateTask");
        updated.GetProperty("status").GetString().Should().Be("DONE");
    }

    [Fact]
    public async Task DeleteTask_mutation_returns_true()
    {
        var createResponse = await PostGraphQL("""
            mutation {
                createTask(input: {
                    projectId: "000000000000000000000010",
                    title: "Will Be Deleted",
                    description: "",
                    priority: LOW
                }) { id }
            }
            """);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var taskId = createBody.GetProperty("data").GetProperty("createTask")
            .GetProperty("id").GetString()!;

        var deleteResponse = await PostGraphQL($$"""
            mutation { deleteTask(id: "{{taskId}}") }
            """);

        var deleteBody = await deleteResponse.Content.ReadFromJsonAsync<JsonElement>();
        deleteBody.TryGetProperty("errors", out _).Should().BeFalse();
        deleteBody.GetProperty("data").GetProperty("deleteTask").GetBoolean().Should().BeTrue();
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public async Task Invalid_graphql_syntax_returns_errors_array()
    {
        var response = await PostGraphQL("{ this is not valid graphql !!!}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Querying_nonexistent_field_returns_errors_array()
    {
        var response = await PostGraphQL("{ doesNotExist }");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.GetArrayLength().Should().BeGreaterThan(0);
    }
}

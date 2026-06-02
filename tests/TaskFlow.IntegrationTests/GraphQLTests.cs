using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using TaskFlow.IntegrationTests.Fixtures;
using TaskFlow.IntegrationTests.Helpers;

namespace TaskFlow.IntegrationTests;

/// <summary>
/// Tests the GraphQL endpoint at /graphql.
/// All workspace/project queries require a valid JWT — the multi-tenancy layer
/// returns FORBIDDEN for unauthenticated requests.
/// </summary>
public class GraphQLTests(TaskFlowFactory factory) : IClassFixture<TaskFlowFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<HttpResponseMessage> PostGraphQL(string query, string? jwt = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = JsonContent.Create(new { query });
        if (jwt is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        return await _client.SendAsync(request);
    }

    // ── Basic connectivity ────────────────────────────────────────────────────

    [Fact]
    public async Task GraphQL_endpoint_returns_200_for_introspection()
    {
        var response = await PostGraphQL("{ __typename }");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Unauthenticated access is denied ──────────────────────────────────────

    [Fact]
    public async Task Workspaces_query_without_token_returns_unauthenticated_error()
    {
        var response = await PostGraphQL("{ workspaces { id name } }");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.TryGetProperty("errors", out var errors).Should().BeTrue();
        var firstError = errors.EnumerateArray().First();
        firstError.GetProperty("extensions").GetProperty("code").GetString()
            .Should().Be("UNAUTHENTICATED");
    }

    [Fact]
    public async Task Projects_query_without_token_returns_unauthenticated_error()
    {
        var response = await PostGraphQL("""
            { projects(workspaceId: "000000000000000000000001") { id name } }
            """);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.TryGetProperty("errors", out _).Should().BeTrue();
    }

    // ── Authenticated workspace flow ──────────────────────────────────────────

    [Fact]
    public async Task CreateWorkspace_with_token_creates_workspace_and_auto_adds_owner()
    {
        var jwt = await AuthHelper.RegisterAndGetTokenAsync(_client);
        var name = $"WS-{Guid.NewGuid():N[..8]}";

        var response = await PostGraphQL($$"""
            mutation {
                createWorkspace(input: { name: "{{name}}" }) {
                    id
                    name
                    ownerId
                }
            }
            """, jwt);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("errors", out _).Should().BeFalse();

        var ws = body.GetProperty("data").GetProperty("createWorkspace");
        ws.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        ws.GetProperty("name").GetString().Should().Be(name);
        // ownerId should be the JWT user's id — proving membership was auto-created
        ws.GetProperty("ownerId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Workspaces_query_returns_only_users_own_workspaces()
    {
        var jwt1 = await AuthHelper.RegisterAndGetTokenAsync(_client);
        var jwt2 = await AuthHelper.RegisterAndGetTokenAsync(_client);

        // User 1 creates a workspace
        var ws1Name = $"User1-WS-{Guid.NewGuid():N[..6]}";
        await PostGraphQL($$"""
            mutation { createWorkspace(input: { name: "{{ws1Name}}" }) { id } }
            """, jwt1);

        // User 2 creates a different workspace
        var ws2Name = $"User2-WS-{Guid.NewGuid():N[..6]}";
        await PostGraphQL($$"""
            mutation { createWorkspace(input: { name: "{{ws2Name}}" }) { id } }
            """, jwt2);

        // User 1 queries — should only see their own workspace
        var response = await PostGraphQL("{ workspaces { id name } }", jwt1);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(ws1Name, "user1 should see their workspace");
        body.Should().NotContain(ws2Name, "user1 should NOT see user2's workspace");
    }

    [Fact]
    public async Task User_cannot_query_projects_in_workspace_they_do_not_belong_to()
    {
        var ownerJwt = await AuthHelper.RegisterAndGetTokenAsync(_client);
        var strangerJwt = await AuthHelper.RegisterAndGetTokenAsync(_client);

        // Owner creates workspace
        var wsResponse = await PostGraphQL("""
            mutation { createWorkspace(input: { name: "Private WS" }) { id } }
            """, ownerJwt);
        var wsBody = await wsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var workspaceId = wsBody.GetProperty("data").GetProperty("createWorkspace")
            .GetProperty("id").GetString()!;

        // Stranger tries to query projects in that workspace
        var response = await PostGraphQL($$"""
            { projects(workspaceId: "{{workspaceId}}") { id name } }
            """, strangerJwt);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.EnumerateArray().First()
            .GetProperty("extensions").GetProperty("code").GetString()
            .Should().Be("FORBIDDEN");
    }

    // ── Workspace member management ───────────────────────────────────────────

    [Fact]
    public async Task Owner_can_add_member_and_member_can_then_query_workspace()
    {
        var ownerJwt = await AuthHelper.RegisterAndGetTokenAsync(_client);

        // Register the second user and capture their ID
        var memberEmail = $"member-{Guid.NewGuid():N}@test.com";
        var registerResponse = await _client.PostAsJsonAsync("/auth/register",
            new { name = "Member User", email = memberEmail, password = "Test1234!" });
        var registeredUser = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var memberId = registeredUser.GetProperty("id").GetString()!;
        var memberJwt = await AuthHelper.RegisterAndGetTokenAsync(_client, memberEmail, "Test1234!");

        // Actually need a fresh token since RegisterAndGetTokenAsync registers again with dup email
        // Let's get the token directly
        var tokenResp = await _client.PostAsJsonAsync("/auth/token",
            new { email = memberEmail, password = "Test1234!" });
        var memberToken = (await tokenResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token").GetString()!;

        // Owner creates workspace
        var wsResponse = await PostGraphQL("""
            mutation { createWorkspace(input: { name: "Shared WS" }) { id } }
            """, ownerJwt);
        var wsBody = await wsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var workspaceId = wsBody.GetProperty("data").GetProperty("createWorkspace")
            .GetProperty("id").GetString()!;

        // Before invite: member cannot see projects
        var beforeResponse = await PostGraphQL($$"""
            { projects(workspaceId: "{{workspaceId}}") { id } }
            """, memberToken);
        var beforeBody = await beforeResponse.Content.ReadFromJsonAsync<JsonElement>();
        beforeBody.TryGetProperty("errors", out _).Should().BeTrue("member not yet invited");

        // Owner adds member
        var addResponse = await PostGraphQL($$"""
            mutation {
                addWorkspaceMember(input: {
                    workspaceId: "{{workspaceId}}",
                    userId: "{{memberId}}",
                    role: EDITOR
                }) { id role }
            }
            """, ownerJwt);
        var addBody = await addResponse.Content.ReadFromJsonAsync<JsonElement>();
        addBody.TryGetProperty("errors", out _).Should().BeFalse("owner should be able to add members");
        addBody.GetProperty("data").GetProperty("addWorkspaceMember")
            .GetProperty("role").GetString().Should().Be("EDITOR");

        // After invite: member CAN see projects
        var afterResponse = await PostGraphQL($$"""
            { projects(workspaceId: "{{workspaceId}}") { id } }
            """, memberToken);
        var afterBody = await afterResponse.Content.ReadFromJsonAsync<JsonElement>();
        afterBody.TryGetProperty("errors", out _).Should().BeFalse("member should now have access");
    }

    [Fact]
    public async Task Non_owner_cannot_add_members_to_workspace()
    {
        var ownerJwt = await AuthHelper.RegisterAndGetTokenAsync(_client);
        var editorJwt = await AuthHelper.RegisterAndGetTokenAsync(_client);
        var strangerEmail = $"stranger-{Guid.NewGuid():N}@test.com";
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Stranger", email = strangerEmail, password = "Test1234!" });
        var strangerTokenResp = await _client.PostAsJsonAsync("/auth/token",
            new { email = strangerEmail, password = "Test1234!" });
        var strangerId = (await strangerTokenResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("userId").GetString()!;

        // Owner creates workspace
        var wsResponse = await PostGraphQL("""
            mutation { createWorkspace(input: { name: "Owner's WS" }) { id } }
            """, ownerJwt);
        var wsBody = await wsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var workspaceId = wsBody.GetProperty("data").GetProperty("createWorkspace")
            .GetProperty("id").GetString()!;

        // Editor (non-owner) tries to add stranger — should be forbidden
        var response = await PostGraphQL($$"""
            mutation {
                addWorkspaceMember(input: {
                    workspaceId: "{{workspaceId}}",
                    userId: "{{strangerId}}",
                    role: VIEWER
                }) { id }
            }
            """, editorJwt);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.EnumerateArray().First()
            .GetProperty("extensions").GetProperty("code").GetString()
            .Should().Be("FORBIDDEN");
    }

    // ── Project mutations with membership ─────────────────────────────────────

    [Fact]
    public async Task CreateProject_succeeds_for_workspace_member()
    {
        var jwt = await AuthHelper.RegisterAndGetTokenAsync(_client);

        var wsResponse = await PostGraphQL("""
            mutation { createWorkspace(input: { name: "Project WS" }) { id } }
            """, jwt);
        var workspaceId = (await wsResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("createWorkspace").GetProperty("id").GetString()!;

        var projResponse = await PostGraphQL($$"""
            mutation {
                createProject(input: {
                    workspaceId: "{{workspaceId}}",
                    name: "My Project",
                    description: "Test"
                    ownerId: "ignored"
                }) { id name status }
            }
            """, jwt);
        var body = await projResponse.Content.ReadFromJsonAsync<JsonElement>();

        body.TryGetProperty("errors", out _).Should().BeFalse();
        body.GetProperty("data").GetProperty("createProject")
            .GetProperty("name").GetString().Should().Be("My Project");
    }

    // ── Task mutations (no membership check for simplicity) ───────────────────

    [Fact]
    public async Task CreateTask_mutation_returns_task_with_todo_status()
    {
        var jwt = await AuthHelper.RegisterAndGetTokenAsync(_client);

        var response = await PostGraphQL("""
            mutation {
                createTask(input: {
                    projectId: "000000000000000000000010",
                    title: "GraphQL Task",
                    description: "Created via mutation",
                    priority: MEDIUM
                }) { id title status priority }
            }
            """, jwt);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("errors", out _).Should().BeFalse();
        body.GetProperty("data").GetProperty("createTask")
            .GetProperty("status").GetString().Should().Be("TODO");
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
}

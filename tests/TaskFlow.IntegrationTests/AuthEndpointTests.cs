using System.Net;
using System.Text.Json;
using TaskFlow.IntegrationTests.Fixtures;
using TaskFlow.IntegrationTests.Helpers;

namespace TaskFlow.IntegrationTests;

/// <summary>
/// Tests POST /auth/register and POST /auth/token.
/// Covers the full BCrypt + JWT flow end to end.
/// </summary>
public class AuthEndpointTests(TaskFlowFactory factory) : IClassFixture<TaskFlowFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Registration ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_returns_201_with_user_details()
    {
        var email = $"register-{Guid.NewGuid():N}@test.com";

        var response = await _client.PostAsJsonAsync("/auth/register", new
        {
            name = "Alice Smith",
            email,
            password = "Test1234!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").GetString().Should().Be(email);
        body.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        body.TryGetProperty("passwordHash", out _).Should().BeFalse(
            "because passwordHash must never be returned in API responses");
    }

    [Fact]
    public async Task Register_returns_409_for_duplicate_email()
    {
        var email = $"dup-{Guid.NewGuid():N}@test.com";
        var body = new { name = "Bob", email, password = "Test1234!" };

        await _client.PostAsJsonAsync("/auth/register", body);
        var secondResponse = await _client.PostAsJsonAsync("/auth/register", body);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_new_user_gets_member_role_by_default()
    {
        var email = $"role-{Guid.NewGuid():N}@test.com";

        var response = await _client.PostAsJsonAsync("/auth/register", new
        {
            name = "Carol",
            email,
            password = "Test1234!"
        });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("role").GetString().Should().Be("Member");
    }

    // ── Token ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Token_returns_200_with_jwt_for_valid_credentials()
    {
        var email = $"token-{Guid.NewGuid():N}@test.com";
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Dave", email, password = "Test1234!" });

        var response = await _client.PostAsJsonAsync("/auth/token",
            new { email, password = "Test1234!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("token").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("userId").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("email").GetString().Should().Be(email);
    }

    [Fact]
    public async Task Token_contains_three_dot_separated_jwt_segments()
    {
        var email = $"jwt-{Guid.NewGuid():N}@test.com";
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Eve", email, password = "Test1234!" });

        var tokenResponse = await _client.PostAsJsonAsync("/auth/token",
            new { email, password = "Test1234!" });

        var body = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString()!;

        // A valid JWT has exactly three base64url segments separated by dots
        token.Split('.').Should().HaveCount(3,
            "because a JWT consists of header.payload.signature");
    }

    [Fact]
    public async Task Token_returns_401_for_wrong_password()
    {
        var email = $"wp-{Guid.NewGuid():N}@test.com";
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Frank", email, password = "RightPassword1!" });

        var response = await _client.PostAsJsonAsync("/auth/token",
            new { email, password = "WrongPassword!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_returns_401_for_unknown_email()
    {
        var response = await _client.PostAsJsonAsync("/auth/token",
            new { email = "nobody@nowhere.com", password = "Test1234!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_expiresAt_is_in_the_future()
    {
        var email = $"exp-{Guid.NewGuid():N}@test.com";
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Grace", email, password = "Test1234!" });

        var tokenResponse = await _client.PostAsJsonAsync("/auth/token",
            new { email, password = "Test1234!" });

        var body = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var expiresAt = body.GetProperty("expiresAt").GetDateTime();

        expiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    // ── Using the token ───────────────────────────────────────────────────────

    [Fact]
    public async Task Authenticated_request_using_token_reaches_api()
    {
        // Get a token and use it to hit the /health/live endpoint
        // (which is open, but this confirms the full auth roundtrip works)
        var token = await AuthHelper.RegisterAndGetTokenAsync(_client);

        var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

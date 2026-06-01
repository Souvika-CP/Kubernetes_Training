namespace TaskFlow.IntegrationTests.Helpers;

public static class AuthHelper
{
    public record TokenResponse(string Token, string UserId, string Email, string Role, DateTime ExpiresAt);

    /// <summary>Registers a new user and returns a valid JWT token for that user.</summary>
    public static async Task<string> RegisterAndGetTokenAsync(
        HttpClient client,
        string? email = null,
        string password = "Test1234!")
    {
        email ??= $"user-{Guid.NewGuid():N}@test.com";

        var registerResponse = await client.PostAsJsonAsync("/auth/register", new
        {
            name = "Test User",
            email,
            password
        });
        registerResponse.EnsureSuccessStatusCode();

        var tokenResponse = await client.PostAsJsonAsync("/auth/token", new { email, password });
        tokenResponse.EnsureSuccessStatusCode();

        var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
        return token!.Token;
    }

    public static HttpClient WithBearerToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

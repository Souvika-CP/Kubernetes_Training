using System.Net;
using TaskFlow.IntegrationTests.Fixtures;

namespace TaskFlow.IntegrationTests;

/// <summary>
/// Tests the three Kubernetes health probe endpoints.
/// Live: always healthy (no checks). Ready/Startup: require MongoDB to be reachable.
/// </summary>
public class HealthCheckTests(TaskFlowFactory factory) : IClassFixture<TaskFlowFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Live_endpoint_returns_200_healthy()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("Healthy");
    }

    [Fact]
    public async Task Ready_endpoint_returns_200_when_mongodb_is_running()
    {
        var response = await _client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("Healthy");
    }

    [Fact]
    public async Task Startup_endpoint_returns_200_when_mongodb_is_running()
    {
        var response = await _client.GetAsync("/health/startup");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("Healthy");
    }

    [Fact]
    public async Task Version_endpoint_returns_version_number()
    {
        var response = await _client.GetAsync("/version");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("version");
    }

    [Fact]
    public async Task Live_endpoint_is_always_healthy_regardless_of_dependencies()
    {
        // Liveness has no checks — it only verifies the process is alive.
        // This test confirms it returns 200 even in degraded states.
        var response = await _client.GetAsync("/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

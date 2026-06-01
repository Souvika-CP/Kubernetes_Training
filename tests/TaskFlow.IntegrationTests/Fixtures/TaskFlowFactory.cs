using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.MongoDb;

namespace TaskFlow.IntegrationTests.Fixtures;

/// <summary>
/// Shared test server backed by a real MongoDB running in a Docker container (Testcontainers).
/// One instance is shared across all tests in a class via IClassFixture.
/// </summary>
public class TaskFlowFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder().Build();

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _mongo.StopAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDb:ConnectionString"] = _mongo.GetConnectionString(),
                ["MongoDb:DatabaseName"] = "taskflow_test",
                // Point OTel to a non-existent endpoint — exporter drops silently
                ["Otel:Endpoint"] = "http://localhost:14317"
            }));
    }
}

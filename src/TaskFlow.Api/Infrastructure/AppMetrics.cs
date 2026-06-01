using Prometheus;

namespace TaskFlow.Api.Infrastructure;

public static class AppMetrics
{
    public static readonly Counter TasksCreated = Metrics
        .CreateCounter("taskflow_tasks_created_total", "Total number of tasks created");

    public static readonly Gauge ActiveProjects = Metrics
        .CreateGauge("taskflow_active_projects", "Number of active projects");

    public static readonly Histogram GraphQlRequestDuration = Metrics
        .CreateHistogram(
            "taskflow_graphql_request_duration_seconds",
            "Duration of GraphQL mutation operations in seconds",
            new HistogramConfiguration
            {
                Buckets = Histogram.LinearBuckets(start: 0.01, width: 0.05, count: 10)
            });
}

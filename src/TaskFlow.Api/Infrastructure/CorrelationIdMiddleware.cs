using System.Diagnostics;
using Serilog.Context;

namespace TaskFlow.Api.Infrastructure;

public class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var correlationId))
            correlationId = Guid.NewGuid().ToString();

        context.Response.Headers[HeaderName] = correlationId.ToString();

        // Propagate correlation ID into the active trace span
        Activity.Current?.SetTag("correlation.id", correlationId.ToString());

        using (LogContext.PushProperty("CorrelationId", correlationId.ToString()))
            await next(context);
    }
}

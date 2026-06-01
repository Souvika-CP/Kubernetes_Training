using Microsoft.AspNetCore.Http;
using TaskFlow.Api.Infrastructure;

namespace TaskFlow.UnitTests.Infrastructure;

public class CorrelationIdMiddlewareTests
{
    private const string HeaderName = "X-Correlation-Id";

    [Fact]
    public async Task Sets_correlation_id_in_response_header_when_none_provided()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Response.Headers.Should().ContainKey(HeaderName);
        context.Response.Headers[HeaderName].ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Generated_correlation_id_is_a_valid_guid()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var correlationId = context.Response.Headers[HeaderName].ToString();
        Guid.TryParse(correlationId, out _).Should().BeTrue(
            "because when no header is supplied a new GUID should be generated");
    }

    [Fact]
    public async Task Echoes_incoming_correlation_id_back_in_response()
    {
        var incomingId = Guid.NewGuid().ToString();
        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderName] = incomingId;
        context.Response.Body = new MemoryStream();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Response.Headers[HeaderName].ToString().Should().Be(incomingId);
    }

    [Fact]
    public async Task Two_requests_without_headers_get_different_correlation_ids()
    {
        var context1 = new DefaultHttpContext();
        context1.Response.Body = new MemoryStream();
        var context2 = new DefaultHttpContext();
        context2.Response.Body = new MemoryStream();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context1);
        await middleware.InvokeAsync(context2);

        var id1 = context1.Response.Headers[HeaderName].ToString();
        var id2 = context2.Response.Headers[HeaderName].ToString();
        id1.Should().NotBe(id2);
    }

    [Fact]
    public async Task Calls_next_middleware_in_pipeline()
    {
        var nextWasCalled = false;
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextWasCalled.Should().BeTrue();
    }
}

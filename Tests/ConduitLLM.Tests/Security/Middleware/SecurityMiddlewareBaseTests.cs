using System.Text.Json;

using ConduitLLM.Security.Middleware;
using ConduitLLM.Security.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConduitLLM.Tests.Security.Middleware;

public sealed class SecurityMiddlewareBaseTests
{
    [Fact]
    public async Task RateLimitRejection_UsesOpenAIErrorContractAndCanonicalHeaders()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var resetsAt = DateTime.UtcNow.AddMilliseconds(4200);
        var result = SecurityCheckResult.RateLimited(
            "Discovery rate limit exceeded",
            resetsAt,
            limit: 10,
            remaining: 0,
            scope: "discovery");
        var middleware = new TestSecurityMiddleware();

        await middleware.InvokeAsync(context, result);

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.Equal("10", context.Response.Headers["X-RateLimit-Limit"]);
        Assert.Equal("0", context.Response.Headers["X-RateLimit-Remaining"]);
        Assert.Equal("discovery", context.Response.Headers["X-RateLimit-Scope"]);
        Assert.Equal("5", context.Response.Headers["Retry-After"]);
        Assert.True(context.Response.Headers.ContainsKey("X-RateLimit-Reset"));

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var error = document.RootElement.GetProperty("error");
        Assert.Equal("Discovery rate limit exceeded", error.GetProperty("message").GetString());
        Assert.Equal("rate_limit_exceeded", error.GetProperty("type").GetString());
        Assert.Equal("rate_limit_exceeded", error.GetProperty("code").GetString());
    }

    private sealed class TestSecurityMiddleware()
        : SecurityMiddlewareBase(_ => Task.CompletedTask, NullLogger.Instance)
    {
        public Task InvokeAsync(HttpContext context, SecurityCheckResult result) =>
            ProcessRequestAsync(context, _ => Task.FromResult(result));
    }
}

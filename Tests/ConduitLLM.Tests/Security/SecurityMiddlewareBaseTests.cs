using ConduitLLM.Security.Middleware;
using ConduitLLM.Security.Models;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ConduitLLM.Tests.Security;

public class SecurityMiddlewareBaseTests
{
    private sealed class RecordingSecurityMiddleware : SecurityMiddlewareBase
    {
        public RecordingSecurityMiddleware(RequestDelegate next)
            : base(next, NullLogger.Instance)
        {
        }

        public List<int> RecordedStatusCodes { get; } = new();

        public Task InvokeAsync(HttpContext context, SecurityCheckResult result)
            => ProcessRequestAsync(context, _ => Task.FromResult(result));

        protected override void RecordViolationMetric(int statusCode)
            => RecordedStatusCodes.Add(statusCode);
    }

    [Theory]
    [InlineData(StatusCodes.Status401Unauthorized)]
    [InlineData(StatusCodes.Status403Forbidden)]
    [InlineData(StatusCodes.Status429TooManyRequests)]
    [InlineData(StatusCodes.Status503ServiceUnavailable)]
    public async Task DeniedRequest_RecordsStatusSpecificViolationMetric(int statusCode)
    {
        var middleware = new RecordingSecurityMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, SecurityCheckResult.Denied("blocked", statusCode));

        middleware.RecordedStatusCodes.Should().Equal(statusCode);
        context.Response.StatusCode.Should().Be(statusCode);
    }

    [Fact]
    public async Task DeniedRequest_WithoutStatus_RecordsForbiddenMetric()
    {
        var middleware = new RecordingSecurityMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        var result = new SecurityCheckResult
        {
            IsAllowed = false,
            Reason = "blocked"
        };

        await middleware.InvokeAsync(context, result);

        middleware.RecordedStatusCodes.Should().Equal(StatusCodes.Status403Forbidden);
    }
}

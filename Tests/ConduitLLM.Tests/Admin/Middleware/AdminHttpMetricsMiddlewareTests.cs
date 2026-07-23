using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;
using Moq;
using ConduitLLM.Admin.Middleware;

namespace ConduitLLM.Tests.Admin.Middleware;

public class AdminHttpMetricsMiddlewareTests
{
    [Theory]
    [InlineData("/api/virtualkeys/123")]
    [InlineData("/api/virtualkeys/999999")]
    [InlineData("/api/virtualkeys/not-a-number")]
    public void GetNormalizedPath_MatchedEndpointUsesStableRouteTemplate(string requestPath)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = requestPath;
        context.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/api/virtualkeys/{id}"),
            0,
            new EndpointMetadataCollection(),
            "virtual-key"));
        var middleware = CreateMiddleware();

        var normalized = middleware.Normalize(context);

        Assert.Equal("/api/virtualkeys/{id}", normalized);
    }

    [Theory]
    [InlineData("/.env")]
    [InlineData("/health-attacker-controlled")]
    [InlineData("/api/unknown/one")]
    [InlineData("/api/unknown/two")]
    public void GetNormalizedPath_UnmatchedPathsShareSingleBucket(string requestPath)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = requestPath;
        var middleware = CreateMiddleware();

        var normalized = middleware.Normalize(context);

        Assert.Equal("__unmatched__", normalized);
        Assert.DoesNotContain(requestPath, normalized, StringComparison.OrdinalIgnoreCase);
    }

    private static TestAdminHttpMetricsMiddleware CreateMiddleware() =>
        new(Mock.Of<ILogger<AdminHttpMetricsMiddleware>>());

    private sealed class TestAdminHttpMetricsMiddleware : AdminHttpMetricsMiddleware
    {
        public TestAdminHttpMetricsMiddleware(ILogger<AdminHttpMetricsMiddleware> logger)
            : base(_ => Task.CompletedTask, logger)
        {
        }

        public string Normalize(HttpContext context) => GetNormalizedPath(context);
    }
}

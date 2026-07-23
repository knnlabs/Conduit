using System.Net;
using System.Text.Json;

using ConduitLLM.Admin.Endpoints;
using ConduitLLM.Admin.Interfaces;

using Microsoft.Extensions.DependencyInjection;

using Moq;

namespace ConduitLLM.Tests.Admin.Endpoints;

public sealed class AnalyticsEndpointsTests
{
    [Fact]
    public async Task InvalidateCache_ReportsTheNumberOfInvalidatedKeys()
    {
        var analyticsService = new Mock<IAnalyticsService>();
        var analyticsMetrics = new Mock<IAnalyticsMetrics>();
        analyticsService.Setup(service => service.InvalidateCache()).Returns(3);

        using var host = AdminEndpointTestHost.Create(services =>
        {
            services.AddHttpContextAccessor();
            services.AddSingleton(analyticsService.Object);
            services.AddSingleton(analyticsMetrics.Object);
            services.AddScoped<AnalyticsEndpoints>();
        }, endpoints => AnalyticsEndpoints.MapAnalyticsEndpoints(endpoints));

        var response = await host.Client.PostAsync(
            "/api/Analytics/cache/invalidate?reason=Data%20repair",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Analytics cache invalidated", body.RootElement.GetProperty("message").GetString());
        Assert.Equal("Data repair", body.RootElement.GetProperty("reason").GetString());
        Assert.Equal(3, body.RootElement.GetProperty("keysInvalidated").GetInt32());
        analyticsService.Verify(service => service.InvalidateCache(), Times.Once);
        analyticsMetrics.Verify(metrics => metrics.RecordCacheInvalidation("Data repair", 3), Times.Once);
    }
}

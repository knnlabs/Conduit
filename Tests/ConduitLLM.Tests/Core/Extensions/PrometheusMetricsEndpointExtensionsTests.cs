using System.Diagnostics.Metrics;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;

using ConduitLLM.Core.Extensions;
using ConduitLLM.Gateway.Extensions;
using ConduitLLM.Gateway.Middleware;

using FluentAssertions;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Prometheus;

namespace ConduitLLM.Tests.Core.Extensions;

public sealed class PrometheusMetricsEndpointExtensionsTests
{
    private const string RemoteIpHeader = "X-Test-Remote-Ip";
    private const string AuthenticatedHeader = "X-Test-Authenticated";

    private static readonly Counter RegistryCounter = Prometheus.Metrics.CreateCounter(
        "conduit_issue_1066_registry_total",
        "Verifies prometheus-net registry exposition.");

    private static readonly Meter TestMeter = new("ConduitLLM.Tests.Issue1066");
    private static readonly System.Diagnostics.Metrics.Counter<long> MeterCounter = TestMeter.CreateCounter<long>(
        "conduit.issue_1066.meter");

    [Fact]
    public async Task MetricsEndpoint_ExposesRegistryAndMeterMetrics_ToPrivateNetworks()
    {
        RegistryCounter.Inc();
        MeterCounter.Add(1);
        UsageMetrics.BillingRevenue.WithLabels("issue-1066", "test").Inc(0.01);

        using var host = await StartHostAsync();
        using var request = CreateRequest("/metrics", "10.10.0.8");

        using var response = await host.GetTestClient().SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("conduit_issue_1066_registry_total");
        body.Should().Contain("conduit_issue_1066_meter");
        body.Should().Contain("conduit_billing_revenue_dollars_total");
    }

    [Fact]
    public async Task MetricsEndpoint_ExposesMetrics_ToAuthenticatedExternalRequests()
    {
        RegistryCounter.Inc();

        using var host = await StartHostAsync();
        using var request = CreateRequest("/metrics", "203.0.113.10", authenticated: true);

        using var response = await host.GetTestClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("conduit_issue_1066_registry_total");
    }

    [Fact]
    public async Task MetricsEndpoint_DoesNotExposeMetrics_ToAnonymousExternalRequests()
    {
        using var host = await StartHostAsync();
        using var request = CreateRequest("/metrics", "203.0.113.10");

        using var response = await host.GetTestClient().SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        body.Should().NotContain("conduit_issue_1066_registry_total");
    }

    [Fact]
    public async Task MetricsEndpoint_DoesNotInterceptAdminJsonMetricsRoute()
    {
        using var host = await StartHostAsync();
        using var request = CreateRequest("/metrics/", "203.0.113.10", authenticated: true);

        using var response = await host.GetTestClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("admin-json-metrics");
    }

    [Fact]
    public void MeterAdapter_PreservesStreamingHistogramBuckets()
    {
        using var meter = new Meter("ConduitLLM.Tests.Issue1066.Buckets");
        var providerFirstChunk = meter.CreateHistogram<double>("conduit.stream.time_to_provider_first_chunk");
        var clientFirstFlush = meter.CreateHistogram<double>("conduit.stream.time_to_client_first_flush");
        var accountingFinalization = meter.CreateHistogram<double>("conduit.stream.accounting_finalization");

        ObservabilityExtensions.ResolvePrometheusHistogramBuckets(providerFirstChunk)
            .Should().Equal(0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10, 15);
        ObservabilityExtensions.ResolvePrometheusHistogramBuckets(clientFirstFlush)
            .Should().Equal(0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10, 15);
        ObservabilityExtensions.ResolvePrometheusHistogramBuckets(accountingFinalization)
            .Should().Equal(0.001, 0.002, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10);
    }

    private static HttpRequestMessage CreateRequest(string path, string remoteIp, bool authenticated = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(RemoteIpHeader, remoteIp);
        if (authenticated)
        {
            request.Headers.Add(AuthenticatedHeader, "true");
        }

        return request;
    }

    private static async Task<IHost> StartHostAsync()
    {
        return await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthentication("Test")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", null);
                    services.AddAuthorization();
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.Use(async (context, next) =>
                    {
                        context.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers[RemoteIpHeader]!);
                        await next(context);
                    });
                    app.UseAuthentication();
                    app.UseConduitPrometheusMetricsEndpoint();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/metrics/", () => "admin-json-metrics")
                            .RequireAuthorization();
                    });
                });
            })
            .StartAsync();
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey(AuthenticatedHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "metrics-scraper")], Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}

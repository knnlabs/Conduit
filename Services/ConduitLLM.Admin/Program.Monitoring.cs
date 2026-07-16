using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Utilities;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Prometheus;

namespace ConduitLLM.Admin;

public partial class Program
{
    /// <summary>
    /// Configures health checks, OpenTelemetry metrics/tracing, and monitoring services.
    /// </summary>
    private static void ConfigureMonitoringServices(WebApplicationBuilder builder, ILogger startupLogger)
    {
        // Add basic health checks
        builder.Services.AddHealthChecks();

        // Add connection pool warmer with coordinated warming to prevent thundering herd during deployments
        builder.Services.AddCoordinatedConnectionPoolWarming(builder.Configuration, "AdminAPI");

        // Configure OpenTelemetry metrics and tracing
        var otlpEndpoint = builder.Configuration["Telemetry:OtlpEndpoint"] ?? "http://localhost:4317";
        var tracingEnabled = builder.Configuration.GetValue<bool>("Telemetry:TracingEnabled", true);

        var otelBuilder = builder.Services.AddOpenTelemetry()
            .WithMetrics(meterProviderBuilder =>
            {
                meterProviderBuilder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(serviceName: "ConduitLLM.Admin", serviceVersion: "1.0.0"))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddMeter("System.Runtime")
                    .AddMeter("Microsoft.AspNetCore.Hosting")
                    .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
                    .AddMeter("ConduitLLM.Admin.Requests")
                    .AddMeter("ConduitLLM.Providers")
                    .AddPrometheusExporter();
            });

        // Add distributed tracing when enabled
        if (tracingEnabled)
        {
            otelBuilder.WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(serviceName: "ConduitLLM.Admin", serviceVersion: "1.0.0"))
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        // Filter out health check endpoints to reduce noise
                        options.Filter = httpContext =>
                            !httpContext.Request.Path.StartsWithSegments("/health") &&
                            !httpContext.Request.Path.StartsWithSegments("/metrics");
                    })
                    .AddHttpClientInstrumentation()
                    .AddSource("ConduitLLM.Admin.Requests")
                    .AddSource("ConduitLLM.Providers")
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    });
            });
            startupLogger.LogInformation("OpenTelemetry tracing enabled — exporting to {OtlpEndpoint}", otlpEndpoint);
        }
        else
        {
            startupLogger.LogInformation("OpenTelemetry tracing disabled (set Telemetry:TracingEnabled=true to enable)");
        }

        // Add monitoring services - with leader election
        builder.Services.AddLeaderElectedHostedService<ConduitLLM.Admin.Services.AdminOperationsMetricsService>("AdminOperationsMetricsService");
    }

    /// <summary>
    /// Maps health check, metrics, and Prometheus endpoints.
    /// </summary>
    private static void MapMonitoringEndpoints(WebApplication app)
    {
        // Map health check endpoints
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live")
        });
        app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready") || check.Tags.Count == 0
        });

        app.Logger.LogInformation("Health check endpoints registered: /health, /health/live, /health/ready");

        // Map Prometheus metrics endpoint
        app.UseOpenTelemetryPrometheusScrapingEndpoint(
            context => context.Request.Path == "/metrics" &&
                      (IpAddressHelper.IsPrivateNetworkRequest(context) ||
                       context.User.Identity?.IsAuthenticated == true));

        // For the prometheus-net library metrics
        app.UseHttpMetrics(options =>
        {
            options.ReduceStatusCodeCardinality();
            options.RequestDuration.Enabled = false;
            options.RequestCount.Enabled = false;
        });
    }
}

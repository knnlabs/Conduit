using ConduitLLM.Configuration.Interceptors;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ConduitLLM.Gateway.Extensions;

/// <summary>
/// Extension methods for registering observability services (OpenTelemetry, metrics, tracing)
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// Adds OpenTelemetry observability services including metrics, tracing, and query monitoring
    /// </summary>
    public static IServiceCollection AddObservabilityServices(this IServiceCollection services, IConfiguration configuration)
    {
        var otlpEndpoint = configuration["Telemetry:OtlpEndpoint"] ?? "http://localhost:4317";
        var tracingEnabled = configuration.GetValue<bool>("Telemetry:TracingEnabled", true);

        var otelBuilder = services.AddOpenTelemetry()
            .WithMetrics(meterProviderBuilder =>
            {
                meterProviderBuilder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(serviceName: "ConduitLLM.Gateway", serviceVersion: "1.0.0"))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddMeter("ConduitLLM.SignalR")
                    .AddMeter("ConduitLLM.MediaGeneration")
                    .AddMeter("ConduitLLM.Gateway.Requests")
                    .AddMeter(ConduitLLM.Gateway.Metrics.SseTransportMetrics.MeterName)
                    .AddView(
                        "conduit.stream.time_to_provider_first_chunk",
                        new ExplicitBucketHistogramConfiguration
                        {
                            Boundaries = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10, 15]
                        })
                    .AddView(
                        "conduit.stream.time_to_client_first_flush",
                        new ExplicitBucketHistogramConfiguration
                        {
                            Boundaries = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10, 15]
                        })
                    .AddView(
                        "conduit.stream.accounting_finalization",
                        new ExplicitBucketHistogramConfiguration
                        {
                            Boundaries = [0.001, 0.002, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10]
                        })
                    .AddMeter("ConduitLLM.Providers")
                    // Bus metrics (#931). Wolverine's meter is "Wolverine:{ServiceName}",
                    // so the wildcard is required; it emits sent/succeeded/failure
                    // counters, execution/effective-time histograms, and (on the Postgres
                    // transport) inbox/outbox/scheduled depth gauges + dead-letter counts.
                    .AddMeter("Wolverine*")
                    .AddPrometheusExporter();
            });

        // Add distributed tracing when enabled
        if (tracingEnabled)
        {
            otelBuilder.WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(serviceName: "ConduitLLM.Gateway", serviceVersion: "1.0.0"))
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        // Filter out health check endpoints to reduce noise
                        options.Filter = httpContext =>
                            !httpContext.Request.Path.StartsWithSegments("/health") &&
                            !httpContext.Request.Path.StartsWithSegments("/metrics");
                    })
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddRedisInstrumentation()
                    .AddSource("ConduitLLM.SignalR")
                    .AddSource("ConduitLLM.MediaGeneration")
                    .AddSource("ConduitLLM.Gateway.Requests")
                    .AddSource("ConduitLLM.Providers")
                    // Wolverine message-processing spans (#931).
                    .AddSource("Wolverine")
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    });
            });
        }

        // Configure query monitoring for performance tracking
        services.Configure<QueryMonitoringOptions>(
            configuration.GetSection(QueryMonitoringOptions.SectionName));
        services.AddSingleton<QueryMonitoringInterceptor>();

        // Register task processing metrics (per-instance)
        services.AddHostedService<Services.TaskProcessingMetricsService>();

        // Note: BusinessMetricsService and GatewayOperationsMetricsService are registered
        // in Program.Monitoring.cs with leader election to avoid duplicate metrics in scaled deployments

        return services;
    }
}

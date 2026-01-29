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
                        options.SetDbStatementForText = true;
                        options.RecordException = true;
                    })
                    .AddRedisInstrumentation()
                    .AddSource("ConduitLLM.SignalR")
                    .AddSource("ConduitLLM.MediaGeneration")
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    });
            });
            Console.WriteLine($"[Conduit] OpenTelemetry tracing enabled - exporting to {otlpEndpoint}");
        }
        else
        {
            Console.WriteLine("[Conduit] OpenTelemetry tracing disabled (set Telemetry:TracingEnabled=true to enable)");
        }

        // Configure query monitoring for performance tracking
        services.Configure<QueryMonitoringOptions>(
            configuration.GetSection(QueryMonitoringOptions.SectionName));
        services.AddSingleton<QueryMonitoringInterceptor>();

        // Register background metrics services
        services.AddHostedService<Services.TaskProcessingMetricsService>();
        services.AddHostedService<Services.BusinessMetricsService>();

        return services;
    }
}

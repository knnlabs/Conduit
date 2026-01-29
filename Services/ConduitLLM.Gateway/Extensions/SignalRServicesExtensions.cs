using ConduitLLM.Gateway.Services;

namespace ConduitLLM.Gateway.Extensions;

/// <summary>
/// Extension methods for registering SignalR reliability services
/// </summary>
public static class SignalRServicesExtensions
{
    /// <summary>
    /// Adds SignalR reliability services including acknowledgment, message queue, connection monitor, and batcher
    /// </summary>
    public static IServiceCollection AddSignalRReliabilityServices(this IServiceCollection services)
    {
        // Register SignalR acknowledgment service
        services.AddSingleton<ISignalRAcknowledgmentService, SignalRAcknowledgmentService>();
        services.AddHostedService<SignalRAcknowledgmentService>(provider =>
            (SignalRAcknowledgmentService)provider.GetRequiredService<ISignalRAcknowledgmentService>());

        // Register SignalR message queue service
        services.AddSingleton<ISignalRMessageQueueService, SignalRMessageQueueService>();
        services.AddHostedService<SignalRMessageQueueService>(provider =>
            (SignalRMessageQueueService)provider.GetRequiredService<ISignalRMessageQueueService>());

        // Register SignalR connection monitor
        services.AddSingleton<ISignalRConnectionMonitor, SignalRConnectionMonitor>();
        services.AddHostedService<SignalRConnectionMonitor>(provider =>
            (SignalRConnectionMonitor)provider.GetRequiredService<ISignalRConnectionMonitor>());

        // Register SignalR message batcher
        services.AddSingleton<ISignalRMessageBatcher, SignalRMessageBatcher>();
        services.AddHostedService<SignalRMessageBatcher>(provider =>
            (SignalRMessageBatcher)provider.GetRequiredService<ISignalRMessageBatcher>());

        // Register SignalR OpenTelemetry metrics
        services.AddSingleton<Metrics.SignalRMetrics>();
        services.AddHostedService<SignalROpenTelemetryService>();

        return services;
    }
}

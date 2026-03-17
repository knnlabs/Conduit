using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;

using ConduitLLM.Gateway.Interfaces;
using ConduitLLM.Gateway.Metrics;

namespace ConduitLLM.Gateway.Filters
{
    /// <summary>
    /// Hub filter that automatically collects metrics and adds structured logging for all SignalR operations.
    /// </summary>
    public class SignalRMetricsFilter : IHubFilter
    {
        private readonly ILogger<SignalRMetricsFilter> _logger;
        private readonly ISignalRMetrics _metrics;

        public SignalRMetricsFilter(ILogger<SignalRMetricsFilter> logger, ISignalRMetrics metrics)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        }

        public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
        {
            var connectionId = context.Context.ConnectionId;
            var hubName = context.Hub.GetType().Name;
            var virtualKeyId = GetVirtualKeyId(context);
            var correlationId = GetOrCreateCorrelationId(context);

            // TODO: Protocol detection - Hub filters don't have direct access to the negotiated protocol
            // The protocol is determined during connection negotiation but isn't exposed through HubLifetimeContext
            // For now, we default to "json" and rely on MessagePack being available for clients that request it
            // Future enhancement: Track protocol through a custom connection tracking service
            var protocol = "json";

            // Store protocol in connection items for later retrieval
            context.Context.Items["Protocol"] = protocol;

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["ConnectionId"] = connectionId,
                ["HubName"] = hubName,
                ["VirtualKeyId"] = virtualKeyId?.ToString() ?? "anonymous",
                ["CorrelationId"] = correlationId,
                ["Protocol"] = protocol,
                ["Operation"] = "OnConnectedAsync"
            }))
            {
                try
                {
                    _logger.LogInformation(
                        "SignalR connection established for hub {HubName} with connection {ConnectionId} using {Protocol} protocol",
                        hubName, connectionId, protocol);

                    _metrics.ConnectionsTotal.Add(1, new TagList { { "hub", hubName }, { "protocol", protocol } });
                    _metrics.ActiveConnections.Add(1, new TagList { { "hub", hubName }, { "protocol", protocol } });

                    await next(context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error during SignalR connection for hub {HubName} with connection {ConnectionId}",
                        hubName, connectionId);

                    _metrics.ConnectionErrors.Add(1, new TagList { { "hub", hubName }, { "protocol", protocol }, { "error_type", ex.GetType().Name } });
                    _metrics.ActiveConnections.Add(-1, new TagList { { "hub", hubName }, { "protocol", protocol } });

                    throw;
                }
            }
        }

        public async Task OnDisconnectedAsync(
            HubLifetimeContext context,
            Exception? exception,
            Func<HubLifetimeContext, Exception?, Task> next)
        {
            var connectionId = context.Context.ConnectionId;
            var hubName = context.Hub.GetType().Name;
            var virtualKeyId = GetVirtualKeyId(context);
            var correlationId = GetOrCreateCorrelationId(context);
            var protocol = GetProtocolName(context.Context);

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["ConnectionId"] = connectionId,
                ["HubName"] = hubName,
                ["VirtualKeyId"] = virtualKeyId?.ToString() ?? "anonymous",
                ["CorrelationId"] = correlationId,
                ["Protocol"] = protocol,
                ["Operation"] = "OnDisconnectedAsync"
            }))
            {
                if (exception != null)
                {
                    _logger.LogWarning(exception,
                        "SignalR connection disconnected with error for hub {HubName} with connection {ConnectionId}",
                        hubName, connectionId);
                }
                else
                {
                    _logger.LogInformation(
                        "SignalR connection disconnected for hub {HubName} with connection {ConnectionId}",
                        hubName, connectionId);
                }

                _metrics.ActiveConnections.Add(-1, new TagList { { "hub", hubName }, { "protocol", protocol } });

                await next(context, exception);
            }
        }

        public async ValueTask<object?> InvokeMethodAsync(
            HubInvocationContext invocationContext,
            Func<HubInvocationContext, ValueTask<object?>> next)
        {
            var hubName = invocationContext.Hub.GetType().Name;
            var methodName = invocationContext.HubMethodName;
            var connectionId = invocationContext.Context.ConnectionId;
            var virtualKeyId = GetVirtualKeyId(invocationContext.Context);
            var correlationId = GetOrCreateCorrelationId(invocationContext.Context);
            var protocol = GetProtocolName(invocationContext.Context);

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["ConnectionId"] = connectionId,
                ["HubName"] = hubName,
                ["MethodName"] = methodName,
                ["VirtualKeyId"] = virtualKeyId?.ToString() ?? "anonymous",
                ["CorrelationId"] = correlationId,
                ["Protocol"] = protocol,
                ["Operation"] = "InvokeMethod"
            }))
            {
                _logger.LogDebug(
                    "Invoking SignalR hub method {MethodName} on hub {HubName} for connection {ConnectionId}",
                    methodName, hubName, connectionId);

                using var activity = SignalRMetrics.StartMessageActivity(
                    $"SignalR.{hubName}.{methodName}", hubName, methodName);
                using var timer = _metrics.RecordHubMethodInvocation(hubName, methodName, virtualKeyId, protocol);

                try
                {
                    var result = await next(invocationContext);

                    _logger.LogDebug(
                        "Successfully invoked SignalR hub method {MethodName} on hub {HubName} for connection {ConnectionId}",
                        methodName, hubName, connectionId);

                    return result;
                }
                catch (HubException hubEx)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, hubEx.Message);

                    _logger.LogWarning(hubEx,
                        "Hub exception in method {MethodName} on hub {HubName} for connection {ConnectionId}: {Message}",
                        methodName, hubName, connectionId, hubEx.Message);

                    _metrics.HubErrors.Add(1, new TagList
                    {
                        { "hub", hubName },
                        { "method", methodName },
                        { "protocol", protocol },
                        { "error_type", "HubException" }
                    });

                    throw;
                }
                catch (Exception ex)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                    _logger.LogError(ex,
                        "Error invoking SignalR hub method {MethodName} on hub {HubName} for connection {ConnectionId}",
                        methodName, hubName, connectionId);

                    _metrics.HubErrors.Add(1, new TagList
                    {
                        { "hub", hubName },
                        { "method", methodName },
                        { "protocol", protocol },
                        { "error_type", ex.GetType().Name }
                    });

                    throw;
                }
            }
        }

        private static int? GetVirtualKeyId(HubLifetimeContext context)
        {
            if (context.Context.Items.TryGetValue("VirtualKeyId", out var value) && value is int id)
            {
                return id;
            }

            var claim = context.Context.User?.FindFirst("VirtualKeyId");
            if (claim != null && int.TryParse(claim.Value, out var claimId))
            {
                return claimId;
            }

            return null;
        }

        private static int? GetVirtualKeyId(HubCallerContext context)
        {
            if (context.Items.TryGetValue("VirtualKeyId", out var value) && value is int id)
            {
                return id;
            }

            var claim = context.User?.FindFirst("VirtualKeyId");
            if (claim != null && int.TryParse(claim.Value, out var claimId))
            {
                return claimId;
            }

            return null;
        }

        private static string GetOrCreateCorrelationId(HubLifetimeContext context)
        {
            if (context.Context.Items.TryGetValue("CorrelationId", out var value) && value is string correlationId)
            {
                return correlationId;
            }

            correlationId = Guid.NewGuid().ToString();
            context.Context.Items["CorrelationId"] = correlationId;
            return correlationId;
        }

        private static string GetOrCreateCorrelationId(HubCallerContext context)
        {
            if (context.Items.TryGetValue("CorrelationId", out var value) && value is string correlationId)
            {
                return correlationId;
            }

            correlationId = Guid.NewGuid().ToString();
            context.Items["CorrelationId"] = correlationId;
            return correlationId;
        }

        private static string GetProtocolName(HubCallerContext context)
        {
            // Retrieve protocol from connection items (stored during OnConnectedAsync)
            if (context.Items.TryGetValue("Protocol", out var value) && value is string protocol)
            {
                return protocol;
            }

            return "json"; // Default to JSON if protocol not stored
        }
    }
}
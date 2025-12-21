using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Gateway.Services;
using ConduitLLM.Core.Services;
using MassTransit;
using ConduitLLM.Core.Events;

namespace ConduitLLM.Gateway.Authentication
{
    /// <summary>
    /// Hub filter that applies distributed rate limiting to SignalR connections based on virtual keys
    /// Uses Redis for distributed tracking across all instances
    /// </summary>
    public class VirtualKeySignalRRateLimitFilter : IHubFilter
    {
        private readonly VirtualKeyRateLimitCache _rateLimitCache;
        private readonly ISignalRRateLimitService _signalRRateLimitService;
        private readonly ILogger<VirtualKeySignalRRateLimitFilter> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly SignalRConnectionOptions _connectionOptions;

        /// <summary>
        /// Initializes a new instance of VirtualKeySignalRRateLimitFilter
        /// </summary>
        public VirtualKeySignalRRateLimitFilter(
            VirtualKeyRateLimitCache rateLimitCache,
            ISignalRRateLimitService signalRRateLimitService,
            ILogger<VirtualKeySignalRRateLimitFilter> logger,
            IServiceProvider serviceProvider,
            IOptions<SignalRConnectionOptions> connectionOptions)
        {
            _rateLimitCache = rateLimitCache;
            _signalRRateLimitService = signalRRateLimitService ?? throw new ArgumentNullException(nameof(signalRRateLimitService));
            _logger = logger;
            _serviceProvider = serviceProvider;
            _connectionOptions = connectionOptions?.Value ?? new SignalRConnectionOptions();
        }

        /// <summary>
        /// Called when a hub method is invoked
        /// </summary>
        public async ValueTask<object?> InvokeMethodAsync(
            HubInvocationContext invocationContext,
            Func<HubInvocationContext, ValueTask<object?>> next)
        {
            var virtualKeyHash = GetVirtualKeyHash(invocationContext.Context);
            
            if (string.IsNullOrEmpty(virtualKeyHash))
            {
                // No virtual key, allow through (authentication should have caught this)
                return await next(invocationContext);
            }

            // Get rate limits from cache
            var rateLimits = _rateLimitCache.GetRateLimits(virtualKeyHash);
            if (rateLimits == null || (!rateLimits.RateLimitRpm.HasValue && !rateLimits.RateLimitRpd.HasValue))
            {
                // No rate limits configured
                return await next(invocationContext);
            }

            // Check rate limits using Redis service for distributed tracking
            var result = await _signalRRateLimitService.CheckMethodInvocationAsync(
                virtualKeyHash, 
                rateLimits.RateLimitRpm, 
                rateLimits.RateLimitRpd);

            if (!result.IsAllowed)
            {
                _logger.LogWarning("Virtual Key {KeyHash} exceeded {LimitType} limit for SignalR method {Method}. " +
                    "Current: {Current}/{Limit}, Connections: {Connections}",
                    virtualKeyHash, result.LimitType, invocationContext.HubMethodName,
                    result.Limit - result.RequestsRemaining, result.Limit, result.ActiveConnections);
                
                // Publish rate limit exceeded event
                var virtualKeyId = 0;
                if (invocationContext.Context.Items.TryGetValue("VirtualKeyId", out var keyIdObj) && keyIdObj is int keyId)
                {
                    virtualKeyId = keyId;
                }
                
                PublishRateLimitExceeded(
                    virtualKeyHash, 
                    result.LimitType, 
                    result.Limit, 
                    result.Limit - result.RequestsRemaining,
                    result.LimitType == "RPM" ? "minute" : "day",
                    result.ResetsAt,
                    invocationContext);
                
                throw new HubException(result.DenialReason);
            }

            return await next(invocationContext);
        }

        /// <summary>
        /// Called when a client connects
        /// </summary>
        public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
        {
            var virtualKeyHash = GetVirtualKeyHash(context.Context);

            if (!string.IsNullOrEmpty(virtualKeyHash))
            {
                // Check connection limit FIRST (before incrementing)
                if (_connectionOptions.EnforceLimits)
                {
                    var limitResult = await _signalRRateLimitService.CheckConnectionLimitAsync(
                        virtualKeyHash,
                        _connectionOptions.MaxConnectionsPerVirtualKey);

                    if (!limitResult.IsAllowed)
                    {
                        _logger.LogWarning(
                            "Virtual Key {KeyHash} connection rejected: {Reason}. Current: {Current}/{Max}",
                            virtualKeyHash, limitResult.DenialReason,
                            limitResult.CurrentConnections, limitResult.MaxConnections);

                        PublishConnectionLimitExceeded(virtualKeyHash, limitResult, context);
                        throw new HubException(limitResult.DenialReason);
                    }
                }

                // Use Redis service to track connections across all instances
                var connectionCount = await _signalRRateLimitService.IncrementConnectionCountAsync(virtualKeyHash);

                _logger.LogDebug("Virtual Key {KeyHash} connected. Active connections across all instances: {Count}",
                    virtualKeyHash, connectionCount);
            }

            await next(context);
        }

        /// <summary>
        /// Called when a client disconnects
        /// </summary>
        public async Task OnDisconnectedAsync(
            HubLifetimeContext context,
            Exception? exception,
            Func<HubLifetimeContext, Exception?, Task> next)
        {
            var virtualKeyHash = GetVirtualKeyHash(context.Context);
            
            if (!string.IsNullOrEmpty(virtualKeyHash))
            {
                // Use Redis service to track disconnections across all instances
                var connectionCount = await _signalRRateLimitService.DecrementConnectionCountAsync(virtualKeyHash);
                
                _logger.LogDebug("Virtual Key {KeyHash} disconnected. Active connections across all instances: {Count}",
                    virtualKeyHash, connectionCount);
                
                // Clean up stale connections if needed
                if (connectionCount == 0)
                {
                    await _signalRRateLimitService.CleanupStaleConnectionsAsync(virtualKeyHash);
                }
            }

            await next(context, exception);
        }

        /// <summary>
        /// Gets the virtual key hash from the connection context
        /// </summary>
        private string? GetVirtualKeyHash(HubCallerContext context)
        {
            // Try from Items first (set by hub filter)
            if (context.Items.TryGetValue("VirtualKeyHash", out var itemValue) && itemValue is string itemHash)
            {
                return itemHash;
            }
            
            // Try from User claims (set by authentication handler)
            var claim = context.User?.FindFirst("VirtualKeyHash");
            return claim?.Value;
        }
        
        /// <summary>
        /// Publishes a rate limit exceeded event
        /// </summary>
        private void PublishRateLimitExceeded(string virtualKeyHash, string limitType, int limitValue, 
            int currentUsage, string timeWindow, DateTime resetsAt, HubInvocationContext context)
        {
            // Try to get virtual key ID from context
            var virtualKeyId = 0;
            if (context.Context.Items.TryGetValue("VirtualKeyId", out var keyIdObj) && keyIdObj is int keyId)
            {
                virtualKeyId = keyId;
            }
            
            // Get IP address if available
            var ipAddress = context.Context.GetHttpContext()?.Connection?.RemoteIpAddress?.ToString();
            
            // Fire and forget - don't block the request
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var publishEndpoint = scope.ServiceProvider.GetService<IPublishEndpoint>();
                    
                    if (publishEndpoint != null)
                    {
                        await publishEndpoint.Publish(new RateLimitExceeded
                        {
                            VirtualKeyId = virtualKeyId,
                            VirtualKeyHash = virtualKeyHash,
                            LimitType = limitType,
                            LimitValue = limitValue,
                            CurrentUsage = currentUsage,
                            TimeWindow = timeWindow,
                            ResetsAt = resetsAt,
                            IpAddress = ipAddress,
                            RequestedModel = null, // Not applicable for SignalR
                            CorrelationId = Guid.NewGuid().ToString()
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to publish RateLimitExceeded event for key {KeyHash}", virtualKeyHash);
                }
            });
        }

        /// <summary>
        /// Publishes a connection limit exceeded event
        /// </summary>
        private void PublishConnectionLimitExceeded(
            string virtualKeyHash,
            ConnectionLimitResult limitResult,
            HubLifetimeContext context)
        {
            var virtualKeyId = 0;
            if (context.Context.Items.TryGetValue("VirtualKeyId", out var keyIdObj) && keyIdObj is int keyId)
            {
                virtualKeyId = keyId;
            }

            var ipAddress = context.Context.GetHttpContext()?.Connection?.RemoteIpAddress?.ToString();
            var hubName = context.Hub?.GetType().Name;

            // Fire and forget - don't block the connection rejection
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var publishEndpoint = scope.ServiceProvider.GetService<IPublishEndpoint>();

                    if (publishEndpoint != null)
                    {
                        await publishEndpoint.Publish(new ConnectionLimitExceeded
                        {
                            VirtualKeyId = virtualKeyId,
                            VirtualKeyHash = virtualKeyHash,
                            CurrentConnections = limitResult.CurrentConnections,
                            MaxConnections = limitResult.MaxConnections,
                            HubName = hubName,
                            IpAddress = ipAddress,
                            CorrelationId = Guid.NewGuid().ToString()
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to publish ConnectionLimitExceeded event for key {KeyHash}", virtualKeyHash);
                }
            });
        }
    }
}
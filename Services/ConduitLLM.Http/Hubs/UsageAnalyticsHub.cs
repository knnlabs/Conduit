using Microsoft.AspNetCore.SignalR;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Http.Metrics;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// SignalR hub for real-time usage analytics and monitoring.
    /// Provides real-time updates for API usage, model performance, and cost analytics.
    /// </summary>
    public class UsageAnalyticsHub : SecureHub
    {
        private readonly SignalRMetrics _metrics;
        private readonly ILogger<UsageAnalyticsHub> _logger;
        private readonly IVirtualKeyService _virtualKeyService;
        
        // Track analytics subscriptions
        private static readonly Dictionary<string, HashSet<string>> _analyticsSubscriptions = new();
        private static readonly object _subscriptionLock = new();
        
        /// <summary>
        /// Initializes a new instance of the <see cref="UsageAnalyticsHub"/> class.
        /// </summary>
        /// <param name="metrics">SignalR metrics collector.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="serviceProvider">Service provider for dependency injection.</param>
        /// <param name="virtualKeyService">Virtual key service for validation.</param>
        public UsageAnalyticsHub(
            SignalRMetrics metrics,
            ILogger<UsageAnalyticsHub> logger,
            IServiceProvider serviceProvider,
            IVirtualKeyService virtualKeyService) : base(logger, serviceProvider)
        {
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
        }

        /// <summary>
        /// Gets the hub name for logging and metrics.
        /// </summary>
        /// <returns>The hub name.</returns>
        protected override string GetHubName() => "UsageAnalyticsHub";

        /// <summary>
        /// Called when a client connects to the hub.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            
            var virtualKeyId = GetVirtualKeyId();
            if (virtualKeyId.HasValue)
            {
                _logger.LogInformation(
                    "Client connected to UsageAnalyticsHub: {ConnectionId} for VirtualKey: {VirtualKeyId}",
                    Context.ConnectionId,
                    virtualKeyId.Value);
            }
        }

        /// <summary>
        /// Called when a client disconnects from the hub.
        /// </summary>
        /// <param name="exception">The exception that caused the disconnect, if any.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Clean up subscriptions
            lock (_subscriptionLock)
            {
                _analyticsSubscriptions.Remove(Context.ConnectionId);
            }
            
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Subscribes to real-time usage analytics for the authenticated virtual key.
        /// </summary>
        /// <param name="analyticsType">The type of analytics to subscribe to (e.g., "usage", "cost", "performance", "all").</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SubscribeToAnalytics(string analyticsType)
        {
            var virtualKeyId = RequireVirtualKeyId();
            var correlationId = GetOrCreateCorrelationId();
            
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["VirtualKeyId"] = virtualKeyId,
                ["AnalyticsType"] = analyticsType
            }))
            {
                try
                {
                    // Validate analytics type
                    var validTypes = new[] { "usage", "cost", "performance", "errors", "all" };
                    if (!validTypes.Contains(analyticsType.ToLowerInvariant()))
                    {
                        await Clients.Caller.SendAsync("Error", new
                        {
                            message = $"Invalid analytics type. Valid types: {string.Join(", ", validTypes)}"
                        });
                        return;
                    }
                    
                    // Add to analytics groups
                    if (analyticsType.ToLowerInvariant() == "all")
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, $"analytics-usage-{virtualKeyId}");
                        await Groups.AddToGroupAsync(Context.ConnectionId, $"analytics-cost-{virtualKeyId}");
                        await Groups.AddToGroupAsync(Context.ConnectionId, $"analytics-performance-{virtualKeyId}");
                        await Groups.AddToGroupAsync(Context.ConnectionId, $"analytics-errors-{virtualKeyId}");
                        
                        // Track subscriptions
                        lock (_subscriptionLock)
                        {
                            _analyticsSubscriptions[Context.ConnectionId] = new HashSet<string> { "usage", "cost", "performance", "errors" };
                        }
                    }
                    else
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, $"analytics-{analyticsType}-{virtualKeyId}");
                        
                        // Track subscription
                        lock (_subscriptionLock)
                        {
                            if (!_analyticsSubscriptions.ContainsKey(Context.ConnectionId))
                            {
                                _analyticsSubscriptions[Context.ConnectionId] = new HashSet<string>();
                            }
                            _analyticsSubscriptions[Context.ConnectionId].Add(analyticsType);
                        }
                    }
                    
                    _logger.LogInformation(
                        "Virtual key {VirtualKeyId} subscribed to {AnalyticsType} analytics",
                        virtualKeyId,
                        analyticsType);
                    
                    await Clients.Caller.SendAsync("SubscribedToAnalytics", analyticsType);
                }
                catch (Exception ex)
                {
                    _metrics.HubErrors.Add(1,
                        new("hub", "UsageAnalyticsHub"),
                        new("error_type", ex.GetType().Name));
                    _logger.LogError(ex, "Error subscribing to analytics");
                    await Clients.Caller.SendAsync("Error", new
                    {
                        message = "Failed to subscribe to analytics"
                    });
                }
            }
        }

        /// <summary>
        /// Unsubscribes from real-time usage analytics.
        /// </summary>
        /// <param name="analyticsType">The type of analytics to unsubscribe from.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task UnsubscribeFromAnalytics(string analyticsType)
        {
            var virtualKeyId = RequireVirtualKeyId();
            
            try
            {
                if (analyticsType.ToLowerInvariant() == "all")
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"analytics-usage-{virtualKeyId}");
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"analytics-cost-{virtualKeyId}");
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"analytics-performance-{virtualKeyId}");
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"analytics-errors-{virtualKeyId}");
                    
                    lock (_subscriptionLock)
                    {
                        _analyticsSubscriptions.Remove(Context.ConnectionId);
                    }
                }
                else
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"analytics-{analyticsType}-{virtualKeyId}");
                    
                    lock (_subscriptionLock)
                    {
                        if (_analyticsSubscriptions.ContainsKey(Context.ConnectionId))
                        {
                            _analyticsSubscriptions[Context.ConnectionId].Remove(analyticsType);
                        }
                    }
                }
                
                _logger.LogInformation(
                    "Virtual key {VirtualKeyId} unsubscribed from {AnalyticsType} analytics",
                    virtualKeyId,
                    analyticsType);
                
                await Clients.Caller.SendAsync("UnsubscribedFromAnalytics", analyticsType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing from analytics");
            }
        }

        /// <summary>
        /// Subscribes to global analytics (admin only).
        /// </summary>
        /// <param name="analyticsType">The type of analytics to subscribe to.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SubscribeToGlobalAnalytics(string analyticsType)
        {
            var correlationId = GetOrCreateCorrelationId();
            
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["AnalyticsType"] = analyticsType
            }))
            {
                try
                {
                    // Check if the requesting key has admin privileges
                    if (!await IsAdminAsync())
                    {
                        await Clients.Caller.SendAsync("Error", new
                        {
                            message = "Unauthorized: Admin privileges required for global analytics"
                        });
                        return;
                    }
                    
                    // Add to global analytics groups
                    if (analyticsType.ToLowerInvariant() == "all")
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, "analytics-global-usage");
                        await Groups.AddToGroupAsync(Context.ConnectionId, "analytics-global-cost");
                        await Groups.AddToGroupAsync(Context.ConnectionId, "analytics-global-performance");
                        await Groups.AddToGroupAsync(Context.ConnectionId, "analytics-global-errors");
                    }
                    else
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, $"analytics-global-{analyticsType}");
                    }
                    
                    _logger.LogInformation(
                        "Admin subscribed to global {AnalyticsType} analytics",
                        analyticsType);
                    
                    await Clients.Caller.SendAsync("SubscribedToGlobalAnalytics", analyticsType);
                }
                catch (Exception ex)
                {
                    _metrics.HubErrors.Add(1,
                        new("hub", "UsageAnalyticsHub"),
                        new("error_type", ex.GetType().Name));
                    _logger.LogError(ex, "Error subscribing to global analytics");
                    await Clients.Caller.SendAsync("Error", new
                    {
                        message = "Failed to subscribe to global analytics"
                    });
                }
            }
        }

        /// <summary>
        /// Broadcasts real-time usage metrics for a virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID.</param>
        /// <param name="metrics">The usage metrics.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task BroadcastUsageMetrics(int virtualKeyId, UsageMetricsNotification metrics)
        {
            var correlationId = GetOrCreateCorrelationId();
            
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["VirtualKeyId"] = virtualKeyId,
                ["MetricType"] = "usage"
            }))
            {
                try
                {
                    // Send to virtual key's usage analytics group
                    await Clients.Group($"analytics-usage-{virtualKeyId}").SendAsync("UsageMetrics", metrics);
                    
                    // If significant usage, also send to global analytics
                    if (metrics.RequestsPerMinute > 100 || metrics.TokensPerMinute > 10000)
                    {
                        await Clients.Group("analytics-global-usage").SendAsync("GlobalUsageMetrics", new
                        {
                            VirtualKeyId = virtualKeyId,
                            Metrics = metrics
                        });
                    }
                    
                    _metrics.MessagesSent.Add(1,
                        new("hub", "UsageAnalyticsHub"),
                        new("message_type", "usage_metrics"));
                    
                    _logger.LogDebug(
                        "Broadcasted usage metrics for virtual key {VirtualKeyId}: {RequestsPerMinute} RPM, {TokensPerMinute} TPM",
                        virtualKeyId,
                        metrics.RequestsPerMinute,
                        metrics.TokensPerMinute);
                }
                catch (Exception ex)
                {
                    _metrics.HubErrors.Add(1,
                        new("hub", "UsageAnalyticsHub"),
                        new("error_type", ex.GetType().Name));
                    _logger.LogError(ex, "Error broadcasting usage metrics");
                    throw;
                }
            }
        }

        /// <summary>
        /// Broadcasts real-time cost analytics for a virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID.</param>
        /// <param name="analytics">The cost analytics.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task BroadcastCostAnalytics(int virtualKeyId, CostAnalyticsNotification analytics)
        {
            var correlationId = GetOrCreateCorrelationId();
            
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["VirtualKeyId"] = virtualKeyId,
                ["MetricType"] = "cost",
                ["TotalCost"] = analytics.TotalCost
            }))
            {
                try
                {
                    // Send to virtual key's cost analytics group
                    await Clients.Group($"analytics-cost-{virtualKeyId}").SendAsync("CostAnalytics", analytics);
                    
                    // If high cost rate, also send to global analytics
                    if (analytics.CostPerHour > 10.0m)
                    {
                        await Clients.Group("analytics-global-cost").SendAsync("GlobalCostAnalytics", new
                        {
                            VirtualKeyId = virtualKeyId,
                            Analytics = analytics
                        });
                    }
                    
                    _metrics.MessagesSent.Add(1,
                        new("hub", "UsageAnalyticsHub"),
                        new("message_type", "cost_analytics"));
                    
                    _logger.LogInformation(
                        "Broadcasted cost analytics for virtual key {VirtualKeyId}: ${TotalCost:F2} total, ${CostPerHour:F2}/hr",
                        virtualKeyId,
                        analytics.TotalCost,
                        analytics.CostPerHour);
                }
                catch (Exception ex)
                {
                    _metrics.HubErrors.Add(1,
                        new("hub", "UsageAnalyticsHub"),
                        new("error_type", ex.GetType().Name));
                    _logger.LogError(ex, "Error broadcasting cost analytics");
                    throw;
                }
            }
        }

        /// <summary>
        /// Broadcasts model performance metrics for a virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID.</param>
        /// <param name="metrics">The performance metrics.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task BroadcastPerformanceMetrics(int virtualKeyId, PerformanceMetricsNotification metrics)
        {
            var correlationId = GetOrCreateCorrelationId();
            
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["VirtualKeyId"] = virtualKeyId,
                ["MetricType"] = "performance",
                ["ModelName"] = metrics.ModelName
            }))
            {
                try
                {
                    // Send to virtual key's performance analytics group
                    await Clients.Group($"analytics-performance-{virtualKeyId}").SendAsync("PerformanceMetrics", metrics);
                    
                    // If poor performance, also send to global analytics
                    if (metrics.AverageLatencyMs > 5000 || metrics.ErrorRate > 0.05)
                    {
                        await Clients.Group("analytics-global-performance").SendAsync("GlobalPerformanceMetrics", new
                        {
                            VirtualKeyId = virtualKeyId,
                            Metrics = metrics
                        });
                    }
                    
                    _metrics.MessagesSent.Add(1,
                        new("hub", "UsageAnalyticsHub"),
                        new("message_type", "performance_metrics"));
                    
                    _logger.LogDebug(
                        "Broadcasted performance metrics for virtual key {VirtualKeyId}, model {Model}: {LatencyMs}ms avg latency",
                        virtualKeyId,
                        metrics.ModelName,
                        metrics.AverageLatencyMs);
                }
                catch (Exception ex)
                {
                    _metrics.HubErrors.Add(1,
                        new("hub", "UsageAnalyticsHub"),
                        new("error_type", ex.GetType().Name));
                    _logger.LogError(ex, "Error broadcasting performance metrics");
                    throw;
                }
            }
        }

        /// <summary>
        /// Broadcasts error analytics for a virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID.</param>
        /// <param name="analytics">The error analytics.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task BroadcastErrorAnalytics(int virtualKeyId, ErrorAnalyticsNotification analytics)
        {
            var correlationId = GetOrCreateCorrelationId();
            
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["VirtualKeyId"] = virtualKeyId,
                ["MetricType"] = "errors",
                ["ErrorCount"] = analytics.TotalErrors
            }))
            {
                try
                {
                    // Send to virtual key's error analytics group
                    await Clients.Group($"analytics-errors-{virtualKeyId}").SendAsync("ErrorAnalytics", analytics);
                    
                    // If high error rate, also send to global analytics
                    if (analytics.ErrorRate > 0.1 || analytics.TotalErrors > 100)
                    {
                        await Clients.Group("analytics-global-errors").SendAsync("GlobalErrorAnalytics", new
                        {
                            VirtualKeyId = virtualKeyId,
                            Analytics = analytics
                        });
                    }
                    
                    _metrics.MessagesSent.Add(1,
                        new("hub", "UsageAnalyticsHub"),
                        new("message_type", "error_analytics"));
                    
                    _logger.LogWarning(
                        "Broadcasted error analytics for virtual key {VirtualKeyId}: {ErrorCount} errors, {ErrorRate:P} error rate",
                        virtualKeyId,
                        analytics.TotalErrors,
                        analytics.ErrorRate);
                }
                catch (Exception ex)
                {
                    _metrics.HubErrors.Add(1,
                        new("hub", "UsageAnalyticsHub"),
                        new("error_type", ex.GetType().Name));
                    _logger.LogError(ex, "Error broadcasting error analytics");
                    throw;
                }
            }
        }

        /// <summary>
        /// Requests a summary of current analytics for the authenticated virtual key.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task GetAnalyticsSummary()
        {
            var virtualKeyId = RequireVirtualKeyId();
            
            try
            {
                // Get subscribed analytics types
                HashSet<string>? subscribedTypes = null;
                lock (_subscriptionLock)
                {
                    _analyticsSubscriptions.TryGetValue(Context.ConnectionId, out subscribedTypes);
                }
                
                var summary = new AnalyticsSummaryNotification
                {
                    VirtualKeyId = virtualKeyId,
                    Timestamp = DateTime.UtcNow,
                    SubscribedAnalytics = subscribedTypes?.ToList() ?? new List<string>(),
                    Message = "Analytics summary requested"
                };
                
                await Clients.Caller.SendAsync("AnalyticsSummary", summary);
                
                _logger.LogInformation(
                    "Sent analytics summary to virtual key {VirtualKeyId}",
                    virtualKeyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting analytics summary");
                await Clients.Caller.SendAsync("Error", new
                {
                    message = "Failed to get analytics summary"
                });
            }
        }
    }
}
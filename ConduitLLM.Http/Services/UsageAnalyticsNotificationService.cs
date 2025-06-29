using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Http.Hubs;
using ConduitLLM.Configuration.DTOs.SignalR;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service for sending real-time usage analytics notifications via SignalR.
    /// </summary>
    public interface IUsageAnalyticsNotificationService
    {
        /// <summary>
        /// Sends usage metrics for a virtual key.
        /// </summary>
        Task SendUsageMetricsAsync(int virtualKeyId, UsageMetricsNotification metrics);
        
        /// <summary>
        /// Sends cost analytics for a virtual key.
        /// </summary>
        Task SendCostAnalyticsAsync(int virtualKeyId, CostAnalyticsNotification analytics);
        
        /// <summary>
        /// Sends performance metrics for a virtual key.
        /// </summary>
        Task SendPerformanceMetricsAsync(int virtualKeyId, PerformanceMetricsNotification metrics);
        
        /// <summary>
        /// Sends error analytics for a virtual key.
        /// </summary>
        Task SendErrorAnalyticsAsync(int virtualKeyId, ErrorAnalyticsNotification analytics);
        
        /// <summary>
        /// Sends global usage metrics to admin subscribers.
        /// </summary>
        Task SendGlobalUsageMetricsAsync(UsageMetricsNotification metrics);
        
        /// <summary>
        /// Sends global cost analytics to admin subscribers.
        /// </summary>
        Task SendGlobalCostAnalyticsAsync(CostAnalyticsNotification analytics);
    }

    /// <summary>
    /// Implementation of usage analytics notification service using SignalR.
    /// </summary>
    public class UsageAnalyticsNotificationService : IUsageAnalyticsNotificationService
    {
        private readonly IHubContext<UsageAnalyticsHub> _hubContext;
        private readonly ILogger<UsageAnalyticsNotificationService> _logger;

        public UsageAnalyticsNotificationService(
            IHubContext<UsageAnalyticsHub> hubContext,
            ILogger<UsageAnalyticsNotificationService> logger)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendUsageMetricsAsync(int virtualKeyId, UsageMetricsNotification metrics)
        {
            try
            {
                // Send to virtual key's usage analytics group
                await _hubContext.Clients.Group($"analytics-usage-{virtualKeyId}").SendAsync("UsageMetrics", metrics);
                
                // If significant usage, also send to global analytics
                if (metrics.RequestsPerMinute > 100 || metrics.TokensPerMinute > 10000)
                {
                    await _hubContext.Clients.Group("analytics-global-usage").SendAsync("GlobalUsageMetrics", new
                    {
                        VirtualKeyId = virtualKeyId,
                        Metrics = metrics
                    });
                }
                
                _logger.LogDebug(
                    "Sent usage metrics for virtual key {VirtualKeyId}: {RequestsPerMinute} RPM, {TokensPerMinute} TPM",
                    virtualKeyId,
                    metrics.RequestsPerMinute,
                    metrics.TokensPerMinute);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send usage metrics for virtual key {VirtualKeyId}", virtualKeyId);
            }
        }

        public async Task SendCostAnalyticsAsync(int virtualKeyId, CostAnalyticsNotification analytics)
        {
            try
            {
                // Send to virtual key's cost analytics group
                await _hubContext.Clients.Group($"analytics-cost-{virtualKeyId}").SendAsync("CostAnalytics", analytics);
                
                // If high cost rate, also send to global analytics
                if (analytics.CostPerHour > 10.0m)
                {
                    await _hubContext.Clients.Group("analytics-global-cost").SendAsync("GlobalCostAnalytics", new
                    {
                        VirtualKeyId = virtualKeyId,
                        Analytics = analytics
                    });
                }
                
                _logger.LogInformation(
                    "Sent cost analytics for virtual key {VirtualKeyId}: ${TotalCost:F2} total, ${CostPerHour:F2}/hr",
                    virtualKeyId,
                    analytics.TotalCost,
                    analytics.CostPerHour);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send cost analytics for virtual key {VirtualKeyId}", virtualKeyId);
            }
        }

        public async Task SendPerformanceMetricsAsync(int virtualKeyId, PerformanceMetricsNotification metrics)
        {
            try
            {
                // Send to virtual key's performance analytics group
                await _hubContext.Clients.Group($"analytics-performance-{virtualKeyId}").SendAsync("PerformanceMetrics", metrics);
                
                // If poor performance, also send to global analytics
                if (metrics.AverageLatencyMs > 5000 || metrics.ErrorRate > 0.05)
                {
                    await _hubContext.Clients.Group("analytics-global-performance").SendAsync("GlobalPerformanceMetrics", new
                    {
                        VirtualKeyId = virtualKeyId,
                        Metrics = metrics
                    });
                }
                
                _logger.LogDebug(
                    "Sent performance metrics for virtual key {VirtualKeyId}, model {Model}: {LatencyMs}ms avg latency",
                    virtualKeyId,
                    metrics.ModelName,
                    metrics.AverageLatencyMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send performance metrics for virtual key {VirtualKeyId}", virtualKeyId);
            }
        }

        public async Task SendErrorAnalyticsAsync(int virtualKeyId, ErrorAnalyticsNotification analytics)
        {
            try
            {
                // Send to virtual key's error analytics group
                await _hubContext.Clients.Group($"analytics-errors-{virtualKeyId}").SendAsync("ErrorAnalytics", analytics);
                
                // If high error rate, also send to global analytics
                if (analytics.ErrorRate > 0.1 || analytics.TotalErrors > 100)
                {
                    await _hubContext.Clients.Group("analytics-global-errors").SendAsync("GlobalErrorAnalytics", new
                    {
                        VirtualKeyId = virtualKeyId,
                        Analytics = analytics
                    });
                }
                
                _logger.LogWarning(
                    "Sent error analytics for virtual key {VirtualKeyId}: {ErrorCount} errors, {ErrorRate:P} error rate",
                    virtualKeyId,
                    analytics.TotalErrors,
                    analytics.ErrorRate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send error analytics for virtual key {VirtualKeyId}", virtualKeyId);
            }
        }

        public async Task SendGlobalUsageMetricsAsync(UsageMetricsNotification metrics)
        {
            try
            {
                await _hubContext.Clients.Group("analytics-global-usage").SendAsync("GlobalUsageMetrics", new
                {
                    Metrics = metrics
                });
                
                _logger.LogInformation(
                    "Sent global usage metrics: {RequestsPerMinute} RPM, {TokensPerMinute} TPM",
                    metrics.RequestsPerMinute,
                    metrics.TokensPerMinute);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send global usage metrics");
            }
        }

        public async Task SendGlobalCostAnalyticsAsync(CostAnalyticsNotification analytics)
        {
            try
            {
                await _hubContext.Clients.Group("analytics-global-cost").SendAsync("GlobalCostAnalytics", new
                {
                    Analytics = analytics
                });
                
                _logger.LogInformation(
                    "Sent global cost analytics: ${TotalCost:F2} total, ${CostPerHour:F2}/hr",
                    analytics.TotalCost,
                    analytics.CostPerHour);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send global cost analytics");
            }
        }
    }
}
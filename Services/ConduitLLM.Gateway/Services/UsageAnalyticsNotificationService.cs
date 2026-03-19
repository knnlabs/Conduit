using Microsoft.AspNetCore.SignalR;
using ConduitLLM.Gateway.Hubs;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Gateway.Services
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
    /// Inherits from SignalRNotificationServiceBase for standardized error handling.
    /// </summary>
    public class UsageAnalyticsNotificationService
        : SignalRNotificationServiceBase<UsageAnalyticsHub>,
          IUsageAnalyticsNotificationService
    {
        public UsageAnalyticsNotificationService(
            IHubContext<UsageAnalyticsHub> hubContext,
            ILogger<UsageAnalyticsNotificationService> logger)
            : base(hubContext, logger)
        {
        }

        public async Task SendUsageMetricsAsync(int virtualKeyId, UsageMetricsNotification metrics)
        {
            await SendToGroupAsync($"analytics-usage-{virtualKeyId}", "UsageMetrics", metrics);

            // If significant usage, also send to global analytics
            if (metrics.RequestsPerMinute > 100 || metrics.TokensPerMinute > 10000)
            {
                await SendToGroupAsync("analytics-global-usage", "GlobalUsageMetrics", new
                {
                    VirtualKeyId = virtualKeyId,
                    Metrics = metrics
                });
            }

            Logger.LogDebug(
                "Sent usage metrics for virtual key {VirtualKeyId}: {RequestsPerMinute} RPM, {TokensPerMinute} TPM",
                virtualKeyId,
                metrics.RequestsPerMinute,
                metrics.TokensPerMinute);
        }

        public async Task SendCostAnalyticsAsync(int virtualKeyId, CostAnalyticsNotification analytics)
        {
            await SendToGroupAsync($"analytics-cost-{virtualKeyId}", "CostAnalytics", analytics);

            // If high cost rate, also send to global analytics
            if (analytics.CostPerHour > 10.0m)
            {
                await SendToGroupAsync("analytics-global-cost", "GlobalCostAnalytics", new
                {
                    VirtualKeyId = virtualKeyId,
                    Analytics = analytics
                });
            }

            Logger.LogInformation(
                "Sent cost analytics for virtual key {VirtualKeyId}: ${TotalCost:F2} total, ${CostPerHour:F2}/hr",
                virtualKeyId,
                analytics.TotalCost,
                analytics.CostPerHour);
        }

        public async Task SendPerformanceMetricsAsync(int virtualKeyId, PerformanceMetricsNotification metrics)
        {
            await SendToGroupAsync($"analytics-performance-{virtualKeyId}", "PerformanceMetrics", metrics);

            // If poor performance, also send to global analytics
            if (metrics.AverageLatencyMs > 5000 || metrics.ErrorRate > 0.05)
            {
                await SendToGroupAsync("analytics-global-performance", "GlobalPerformanceMetrics", new
                {
                    VirtualKeyId = virtualKeyId,
                    Metrics = metrics
                });
            }

            Logger.LogDebug(
                "Sent performance metrics for virtual key {VirtualKeyId}, model {Model}: {LatencyMs}ms avg latency",
                virtualKeyId,
                metrics.ModelName,
                metrics.AverageLatencyMs);
        }

        public async Task SendErrorAnalyticsAsync(int virtualKeyId, ErrorAnalyticsNotification analytics)
        {
            await SendToGroupAsync($"analytics-errors-{virtualKeyId}", "ErrorAnalytics", analytics);

            // If high error rate, also send to global analytics
            if (analytics.ErrorRate > 0.1 || analytics.TotalErrors > 100)
            {
                await SendToGroupAsync("analytics-global-errors", "GlobalErrorAnalytics", new
                {
                    VirtualKeyId = virtualKeyId,
                    Analytics = analytics
                });
            }

            Logger.LogWarning(
                "Sent error analytics for virtual key {VirtualKeyId}: {ErrorCount} errors, {ErrorRate:P} error rate",
                virtualKeyId,
                analytics.TotalErrors,
                analytics.ErrorRate);
        }

        public async Task SendGlobalUsageMetricsAsync(UsageMetricsNotification metrics)
        {
            await SendToGroupAsync("analytics-global-usage", "GlobalUsageMetrics", new
            {
                Metrics = metrics
            });

            Logger.LogInformation(
                "Sent global usage metrics: {RequestsPerMinute} RPM, {TokensPerMinute} TPM",
                metrics.RequestsPerMinute,
                metrics.TokensPerMinute);
        }

        public async Task SendGlobalCostAnalyticsAsync(CostAnalyticsNotification analytics)
        {
            await SendToGroupAsync("analytics-global-cost", "GlobalCostAnalytics", new
            {
                Analytics = analytics
            });

            Logger.LogInformation(
                "Sent global cost analytics: ${TotalCost:F2} total, ${CostPerHour:F2}/hr",
                analytics.TotalCost,
                analytics.CostPerHour);
        }
    }
}

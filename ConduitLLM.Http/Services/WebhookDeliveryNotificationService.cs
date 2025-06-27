using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Http.Hubs;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service interface for sending webhook delivery notifications through SignalR.
    /// </summary>
    public interface IWebhookDeliveryNotificationService
    {
        /// <summary>
        /// Notifies about a webhook delivery attempt.
        /// </summary>
        Task NotifyDeliveryAttemptAsync(string webhookUrl, string taskId, string taskType, string eventType, int attemptNumber);
        
        /// <summary>
        /// Notifies about a successful webhook delivery.
        /// </summary>
        Task NotifyDeliverySuccessAsync(string webhookUrl, string taskId, int statusCode, long responseTimeMs, int totalAttempts);
        
        /// <summary>
        /// Notifies about a failed webhook delivery.
        /// </summary>
        Task NotifyDeliveryFailureAsync(string webhookUrl, string taskId, string errorMessage, int? statusCode, int attemptNumber, bool isPermanent);
        
        /// <summary>
        /// Notifies about a scheduled retry.
        /// </summary>
        Task NotifyRetryScheduledAsync(string webhookUrl, string taskId, DateTime retryTime, int retryNumber, int maxRetries);
        
        /// <summary>
        /// Notifies about circuit breaker state change.
        /// </summary>
        Task NotifyCircuitBreakerStateChangeAsync(string webhookUrl, string newState, string previousState, string reason, int failureCount);
        
        /// <summary>
        /// Gets current webhook delivery statistics.
        /// </summary>
        Task<WebhookStatistics> GetStatisticsAsync(string period = "last_hour");
        
        /// <summary>
        /// Records a delivery attempt for statistics.
        /// </summary>
        void RecordDeliveryAttempt(string webhookUrl);
        
        /// <summary>
        /// Records a successful delivery for statistics.
        /// </summary>
        void RecordDeliverySuccess(string webhookUrl, long responseTimeMs);
        
        /// <summary>
        /// Records a failed delivery for statistics.
        /// </summary>
        void RecordDeliveryFailure(string webhookUrl, bool isPermanent);
    }

    /// <summary>
    /// Implementation of webhook delivery notification service.
    /// </summary>
    public class WebhookDeliveryNotificationService : IWebhookDeliveryNotificationService, IHostedService
    {
        private readonly IHubContext<WebhookDeliveryHub> _hubContext;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WebhookDeliveryNotificationService> _logger;
        
        // Statistics tracking
        private readonly ConcurrentDictionary<string, WebhookUrlMetrics> _urlMetrics = new();
        private readonly ConcurrentQueue<DeliveryEvent> _recentEvents = new();
        private Timer? _statisticsTimer;
        private const int MaxRecentEvents = 1000;

        public WebhookDeliveryNotificationService(
            IHubContext<WebhookDeliveryHub> hubContext,
            IServiceProvider serviceProvider,
            ILogger<WebhookDeliveryNotificationService> logger)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Start periodic statistics broadcasting
            _statisticsTimer = new Timer(
                async _ => await BroadcastStatisticsAsync(),
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(1));
            
            _logger.LogInformation("WebhookDeliveryNotificationService started");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _statisticsTimer?.Dispose();
            _logger.LogInformation("WebhookDeliveryNotificationService stopped");
            return Task.CompletedTask;
        }

        public async Task NotifyDeliveryAttemptAsync(
            string webhookUrl, 
            string taskId, 
            string taskType, 
            string eventType, 
            int attemptNumber)
        {
            try
            {
                var attempt = new WebhookDeliveryAttempt
                {
                    WebhookId = GenerateWebhookId(webhookUrl, taskId),
                    TaskId = taskId,
                    TaskType = taskType,
                    Url = webhookUrl,
                    EventType = eventType,
                    AttemptNumber = attemptNumber,
                    Timestamp = DateTime.UtcNow
                };
                
                // Get the hub directly to use broadcast method
                using var scope = _serviceProvider.CreateScope();
                var hub = scope.ServiceProvider.GetService<WebhookDeliveryHub>();
                if (hub != null)
                {
                    await hub.BroadcastDeliveryAttempt(webhookUrl, attempt);
                }
                
                RecordDeliveryAttempt(webhookUrl);
                
                _logger.LogDebug(
                    "Sent delivery attempt notification for {WebhookUrl}, attempt {AttemptNumber}",
                    webhookUrl, attemptNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending delivery attempt notification");
            }
        }

        public async Task NotifyDeliverySuccessAsync(
            string webhookUrl, 
            string taskId, 
            int statusCode, 
            long responseTimeMs, 
            int totalAttempts)
        {
            try
            {
                var success = new WebhookDeliverySuccess
                {
                    WebhookId = GenerateWebhookId(webhookUrl, taskId),
                    TaskId = taskId,
                    Url = webhookUrl,
                    StatusCode = statusCode,
                    ResponseTimeMs = responseTimeMs,
                    TotalAttempts = totalAttempts,
                    Timestamp = DateTime.UtcNow
                };
                
                // Broadcast to webhook-specific group
                var groupName = GetWebhookGroupName(webhookUrl);
                await _hubContext.Clients.Group(groupName).SendAsync("DeliverySucceeded", success);
                
                RecordDeliverySuccess(webhookUrl, responseTimeMs);
                
                _logger.LogInformation(
                    "Sent delivery success notification for {WebhookUrl}, response time: {ResponseTime}ms",
                    webhookUrl, responseTimeMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending delivery success notification");
            }
        }

        public async Task NotifyDeliveryFailureAsync(
            string webhookUrl, 
            string taskId, 
            string errorMessage, 
            int? statusCode, 
            int attemptNumber, 
            bool isPermanent)
        {
            try
            {
                var failure = new WebhookDeliveryFailure
                {
                    WebhookId = GenerateWebhookId(webhookUrl, taskId),
                    TaskId = taskId,
                    Url = webhookUrl,
                    ErrorMessage = errorMessage,
                    StatusCode = statusCode,
                    AttemptNumber = attemptNumber,
                    IsPermanentFailure = isPermanent,
                    Timestamp = DateTime.UtcNow
                };
                
                // Broadcast to webhook-specific group
                var groupName = GetWebhookGroupName(webhookUrl);
                await _hubContext.Clients.Group(groupName).SendAsync("DeliveryFailed", failure);
                
                RecordDeliveryFailure(webhookUrl, isPermanent);
                
                _logger.LogWarning(
                    "Sent delivery failure notification for {WebhookUrl}, attempt {AttemptNumber}, permanent: {IsPermanent}",
                    webhookUrl, attemptNumber, isPermanent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending delivery failure notification");
            }
        }

        public async Task NotifyRetryScheduledAsync(
            string webhookUrl, 
            string taskId, 
            DateTime retryTime, 
            int retryNumber, 
            int maxRetries)
        {
            try
            {
                var retry = new WebhookRetryInfo
                {
                    WebhookId = GenerateWebhookId(webhookUrl, taskId),
                    DeliveryId = $"{taskId}-retry-{retryNumber}",
                    Url = webhookUrl,
                    EventType = "webhook.delivery",
                    ScheduledAt = retryTime,
                    NextAttemptNumber = retryNumber,
                    DelaySeconds = (retryTime - DateTime.UtcNow).TotalSeconds,
                    Reason = $"Retry {retryNumber} of {maxRetries}"
                };
                
                // Broadcast to webhook-specific group
                var groupName = GetWebhookGroupName(webhookUrl);
                await _hubContext.Clients.Group(groupName).SendAsync("RetryScheduled", retry);
                
                _logger.LogInformation(
                    "Sent retry scheduled notification for {WebhookUrl}, retry {RetryNumber}/{MaxRetries} at {RetryTime}",
                    webhookUrl, retryNumber, maxRetries, retryTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending retry scheduled notification");
            }
        }

        public async Task NotifyCircuitBreakerStateChangeAsync(
            string webhookUrl, 
            string newState, 
            string previousState, 
            string reason, 
            int failureCount)
        {
            try
            {
                var stateChange = new WebhookCircuitBreakerState
                {
                    Url = webhookUrl,
                    CurrentState = newState,
                    PreviousState = previousState,
                    Reason = reason,
                    FailureCount = failureCount,
                    SuccessCount = 0,
                    Timestamp = DateTime.UtcNow
                };
                
                // Broadcast to webhook-specific group and all clients
                var groupName = GetWebhookGroupName(webhookUrl);
                await _hubContext.Clients.Group(groupName).SendAsync("CircuitBreakerStateChanged", stateChange);
                await _hubContext.Clients.All.SendAsync("CircuitBreakerStateChanged", stateChange);
                
                _logger.LogWarning(
                    "Circuit breaker state changed for {WebhookUrl}: {PreviousState} -> {NewState}, reason: {Reason}",
                    webhookUrl, previousState, newState, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending circuit breaker state change notification");
            }
        }

        public void RecordDeliveryAttempt(string webhookUrl)
        {
            var metrics = _urlMetrics.GetOrAdd(webhookUrl, _ => new WebhookUrlMetrics());
            Interlocked.Increment(ref metrics.TotalAttempts);
            
            RecordEvent(new DeliveryEvent
            {
                Url = webhookUrl,
                Type = DeliveryEventType.Attempt,
                Timestamp = DateTime.UtcNow
            });
        }

        public void RecordDeliverySuccess(string webhookUrl, long responseTimeMs)
        {
            var metrics = _urlMetrics.GetOrAdd(webhookUrl, _ => new WebhookUrlMetrics());
            Interlocked.Increment(ref metrics.Successes);
            metrics.RecordResponseTime(responseTimeMs);
            
            RecordEvent(new DeliveryEvent
            {
                Url = webhookUrl,
                Type = DeliveryEventType.Success,
                ResponseTimeMs = responseTimeMs,
                Timestamp = DateTime.UtcNow
            });
        }

        public void RecordDeliveryFailure(string webhookUrl, bool isPermanent)
        {
            var metrics = _urlMetrics.GetOrAdd(webhookUrl, _ => new WebhookUrlMetrics());
            Interlocked.Increment(ref metrics.Failures);
            if (!isPermanent)
            {
                Interlocked.Increment(ref metrics.PendingRetries);
            }
            
            RecordEvent(new DeliveryEvent
            {
                Url = webhookUrl,
                Type = DeliveryEventType.Failure,
                IsPermanent = isPermanent,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task<WebhookStatistics> GetStatisticsAsync(string period = "last_hour")
        {
            var stats = new WebhookStatistics
            {
                Period = period,
                UrlStatistics = new List<WebhookUrlStatistics>()
            };
            
            // Calculate cutoff time based on period
            var cutoffTime = period switch
            {
                "last_hour" => DateTime.UtcNow.AddHours(-1),
                "last_day" => DateTime.UtcNow.AddDays(-1),
                "last_week" => DateTime.UtcNow.AddDays(-7),
                _ => DateTime.UtcNow.AddHours(-1)
            };
            
            // Get recent events within the period
            var recentEvents = _recentEvents.Where(e => e.Timestamp >= cutoffTime).ToList();
            
            // Calculate overall statistics
            foreach (var urlMetrics in _urlMetrics)
            {
                var urlStats = new WebhookUrlStatistics
                {
                    Url = urlMetrics.Key,
                    TotalDeliveries = urlMetrics.Value.TotalAttempts,
                    SuccessfulDeliveries = urlMetrics.Value.Successes,
                    FailedDeliveries = urlMetrics.Value.Failures,
                    AverageResponseTimeMs = urlMetrics.Value.GetAverageResponseTime(),
                    SuccessRate = urlMetrics.Value.TotalAttempts > 0 ? 
                        (double)urlMetrics.Value.Successes / urlMetrics.Value.TotalAttempts * 100 : 0,
                    IsHealthy = true
                };
                
                stats.UrlStatistics.Add(urlStats);
                
                stats.TotalDeliveries += urlStats.TotalDeliveries;
                stats.SuccessfulDeliveries += urlStats.SuccessfulDeliveries;
                stats.FailedDeliveries += urlStats.FailedDeliveries;
            }
            
            stats.PendingDeliveries = _urlMetrics.Values.Sum(m => m.PendingRetries);
            stats.SuccessRate = stats.TotalDeliveries > 0 
                ? (double)stats.SuccessfulDeliveries / stats.TotalDeliveries * 100 
                : 0;
            
            // Calculate average response time from recent successful events
            var successfulEvents = recentEvents
                .Where(e => e.Type == DeliveryEventType.Success && e.ResponseTimeMs.HasValue)
                .ToList();
            
            stats.AverageResponseTimeMs = successfulEvents.Any()
                ? successfulEvents.Average(e => e.ResponseTimeMs!.Value)
                : 0;
            
            return await Task.FromResult(stats);
        }

        private async Task BroadcastStatisticsAsync()
        {
            try
            {
                var stats = await GetStatisticsAsync();
                await _hubContext.Clients.All.SendAsync("DeliveryStatisticsUpdated", stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting webhook statistics");
            }
        }

        private void RecordEvent(DeliveryEvent evt)
        {
            _recentEvents.Enqueue(evt);
            
            // Keep queue size limited
            while (_recentEvents.Count > MaxRecentEvents && _recentEvents.TryDequeue(out _))
            {
                // Remove oldest events
            }
        }

        private static string GenerateWebhookId(string webhookUrl, string taskId)
        {
            return $"{taskId}_{webhookUrl.GetHashCode():X8}";
        }

        private static string GetWebhookGroupName(string webhookUrl)
        {
            var uri = new Uri(webhookUrl);
            return $"webhook-{uri.Host.Replace(".", "-")}-{uri.AbsolutePath.Replace("/", "-")}";
        }

        /// <summary>
        /// Internal class for tracking metrics per URL.
        /// </summary>
        private class WebhookUrlMetrics
        {
            public int TotalAttempts;
            public int Successes;
            public int Failures;
            public int PendingRetries;
            private readonly ConcurrentQueue<long> _responseTimes = new();
            private const int MaxResponseTimes = 100;

            public void RecordResponseTime(long responseTimeMs)
            {
                _responseTimes.Enqueue(responseTimeMs);
                while (_responseTimes.Count > MaxResponseTimes && _responseTimes.TryDequeue(out _))
                {
                    // Keep queue size limited
                }
            }

            public double GetAverageResponseTime()
            {
                var times = _responseTimes.ToArray();
                return times.Length > 0 ? times.Average() : 0;
            }
        }

        /// <summary>
        /// Internal class for tracking delivery events.
        /// </summary>
        private class DeliveryEvent
        {
            public string Url { get; set; } = string.Empty;
            public DeliveryEventType Type { get; set; }
            public DateTime Timestamp { get; set; }
            public long? ResponseTimeMs { get; set; }
            public bool IsPermanent { get; set; }
        }

        private enum DeliveryEventType
        {
            Attempt,
            Success,
            Failure
        }
    }
}
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Gateway.Hubs;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Gateway.Services
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
        private IWebhookMetricsService? _metricsService;
        
        private Timer? _statisticsTimer;

        public WebhookDeliveryNotificationService(
            IHubContext<WebhookDeliveryHub> hubContext,
            IServiceProvider serviceProvider,
            ILogger<WebhookDeliveryNotificationService> logger)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Metrics service will be resolved on first use, not in constructor
            _metricsService = null;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Initialize metrics service now that DI container is fully built
            using var scope = _serviceProvider.CreateScope();
            _metricsService = scope.ServiceProvider.GetService<IWebhookMetricsService>();

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
                
                // Record metrics if service is available
                if (_metricsService != null)
                {
                    await _metricsService.RecordAttemptAsync(webhookUrl, taskId, taskType, eventType);
                }
                else
                {
                    RecordDeliveryAttempt(webhookUrl);
                }
                
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
                var groupName = SignalRConstants.Groups.Webhook(webhookUrl);
                await _hubContext.Clients.Group(groupName).SendAsync("DeliverySucceeded", success);
                
                // Record metrics if service is available
                if (_metricsService != null)
                {
                    await _metricsService.RecordSuccessAsync(webhookUrl, taskId, responseTimeMs);
                }
                else
                {
                    RecordDeliverySuccess(webhookUrl, responseTimeMs);
                }
                
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
                var groupName = SignalRConstants.Groups.Webhook(webhookUrl);
                await _hubContext.Clients.Group(groupName).SendAsync("DeliveryFailed", failure);
                
                // Record metrics if service is available
                if (_metricsService != null)
                {
                    await _metricsService.RecordFailureAsync(webhookUrl, taskId, isPermanent);
                }
                else
                {
                    RecordDeliveryFailure(webhookUrl, isPermanent);
                }
                
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
                var groupName = SignalRConstants.Groups.Webhook(webhookUrl);
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
                var groupName = SignalRConstants.Groups.Webhook(webhookUrl);
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
            // This is now handled by the metrics service when available
            // Keep as fallback for when Redis is not available
            _logger.LogDebug("Recording delivery attempt for {WebhookUrl} (fallback mode)", LoggingSanitizer.S(webhookUrl));
        }

        public void RecordDeliverySuccess(string webhookUrl, long responseTimeMs)
        {
            // This is now handled by the metrics service when available
            // Keep as fallback for when Redis is not available
            _logger.LogDebug("Recording delivery success for {WebhookUrl} (fallback mode)", LoggingSanitizer.S(webhookUrl));
        }

        public void RecordDeliveryFailure(string webhookUrl, bool isPermanent)
        {
            // This is now handled by the metrics service when available
            // Keep as fallback for when Redis is not available
            _logger.LogDebug("Recording delivery failure for {WebhookUrl} (fallback mode)", LoggingSanitizer.S(webhookUrl));
        }

        public async Task<WebhookStatistics> GetStatisticsAsync(string period = "last_hour")
        {
            // Use Redis metrics service if available
            if (_metricsService != null)
            {
                return await _metricsService.GetStatisticsAsync(period);
            }
            
            // Fallback to basic statistics when Redis is not available
            var stats = new WebhookStatistics
            {
                Period = period,
                UrlStatistics = new List<WebhookUrlStatistics>(),
                TotalDeliveries = 0,
                SuccessfulDeliveries = 0,
                FailedDeliveries = 0,
                PendingDeliveries = 0,
                SuccessRate = 0,
                AverageResponseTimeMs = 0
            };
            
            _logger.LogDebug("Returning empty statistics (Redis metrics service not available)");
            return stats;
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

        private static string GenerateWebhookId(string webhookUrl, string taskId)
        {
            return $"{taskId}_{webhookUrl.GetHashCode():X8}";
        }

    }
}

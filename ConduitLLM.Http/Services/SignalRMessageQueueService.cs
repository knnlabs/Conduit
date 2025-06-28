using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using ConduitLLM.Core.Models.SignalR;
using ConduitLLM.Core.Constants;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Manages queued SignalR messages with retry logic and circuit breaker
    /// </summary>
    public class SignalRMessageQueueService : BackgroundService
    {
        private readonly ILogger<SignalRMessageQueueService> _logger;
        private readonly IHubContext<Hubs.ImageGenerationHub> _imageHub;
        private readonly IHubContext<Hubs.VideoGenerationHub> _videoHub;
        private readonly ConcurrentQueue<QueuedMessage> _messageQueue;
        private readonly SemaphoreSlim _processingSemaphore;
        private readonly IAsyncPolicy _retryPolicy;

        private record QueuedMessage(
            string HubType,
            string GroupName,
            string MethodName,
            object Message,
            int AttemptCount = 0,
            DateTime? NextRetryTime = null);

        public SignalRMessageQueueService(
            ILogger<SignalRMessageQueueService> logger,
            IHubContext<Hubs.ImageGenerationHub> imageHub,
            IHubContext<Hubs.VideoGenerationHub> videoHub)
        {
            _logger = logger;
            _imageHub = imageHub;
            _videoHub = videoHub;
            _messageQueue = new ConcurrentQueue<QueuedMessage>();
            _processingSemaphore = new SemaphoreSlim(1, 1);

            // Configure retry policy with circuit breaker
            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(exception,
                            "SignalR message delivery failed. Retry {RetryCount} after {TimeSpan}s",
                            retryCount, timeSpan.TotalSeconds);
                    })
                .WrapAsync(
                    Policy.Handle<Exception>()
                        .AdvancedCircuitBreakerAsync(
                            failureThreshold: 0.5, // 50% failure rate
                            samplingDuration: TimeSpan.FromMinutes(1),
                            minimumThroughput: 5,
                            durationOfBreak: TimeSpan.FromSeconds(30),
                            onBreak: (exception, duration) =>
                            {
                                _logger.LogError(exception,
                                    "SignalR circuit breaker opened for {Duration}s",
                                    duration.TotalSeconds);
                            },
                            onReset: () =>
                            {
                                _logger.LogInformation("SignalR circuit breaker reset");
                            }));
        }

        /// <summary>
        /// Queue a message for delivery with retry support
        /// </summary>
        public void QueueMessage(string hubType, string groupName, string methodName, object message)
        {
            var queuedMessage = new QueuedMessage(hubType, groupName, methodName, message);
            _messageQueue.Enqueue(queuedMessage);
            _logger.LogDebug("Queued SignalR message for {HubType}/{GroupName}/{MethodName}",
                hubType, groupName, methodName);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SignalR message queue service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessQueuedMessages(stoppingToken);
                    await Task.Delay(100, stoppingToken); // Small delay between processing cycles
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in SignalR message queue processing");
                    await Task.Delay(1000, stoppingToken); // Back off on error
                }
            }

            _logger.LogInformation("SignalR message queue service stopped");
        }

        private async Task ProcessQueuedMessages(CancellationToken cancellationToken)
        {
            const int maxBatchSize = 100;
            var processedCount = 0;

            while (_messageQueue.TryDequeue(out var message) && processedCount < maxBatchSize)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                // Check if message should be retried yet
                if (message.NextRetryTime.HasValue && message.NextRetryTime.Value > DateTime.UtcNow)
                {
                    // Re-queue for later
                    _messageQueue.Enqueue(message);
                    continue;
                }

                try
                {
                    await _retryPolicy.ExecuteAsync(async () =>
                    {
                        await SendMessage(message, cancellationToken);
                    });

                    _logger.LogDebug("Successfully delivered SignalR message to {GroupName}",
                        message.GroupName);
                    processedCount++;
                }
                catch (BrokenCircuitException)
                {
                    // Circuit is open, re-queue the message for later
                    var retriedMessage = message with
                    {
                        AttemptCount = message.AttemptCount + 1,
                        NextRetryTime = DateTime.UtcNow.AddSeconds(30)
                    };
                    _messageQueue.Enqueue(retriedMessage);
                    _logger.LogWarning("Circuit breaker is open. Message re-queued for {GroupName}",
                        message.GroupName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deliver SignalR message after retries to {GroupName}",
                        message.GroupName);

                    // Re-queue if under max attempts
                    if (message.AttemptCount < 5)
                    {
                        var retriedMessage = message with
                        {
                            AttemptCount = message.AttemptCount + 1,
                            NextRetryTime = DateTime.UtcNow.AddSeconds(Math.Pow(2, message.AttemptCount + 1))
                        };
                        _messageQueue.Enqueue(retriedMessage);
                    }
                }
            }
        }

        private async Task SendMessage(QueuedMessage message, CancellationToken cancellationToken)
        {
            IClientProxy clients = message.HubType.ToLower() switch
            {
                "image" => _imageHub.Clients.Group(message.GroupName),
                "video" => _videoHub.Clients.Group(message.GroupName),
                _ => throw new InvalidOperationException($"Unknown hub type: {message.HubType}")
            };

            await clients.SendAsync(message.MethodName, message.Message, cancellationToken);
        }

        public override void Dispose()
        {
            _processingSemaphore?.Dispose();
            base.Dispose();
        }
    }
}
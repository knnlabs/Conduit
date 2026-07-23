using ConduitLLM.Gateway.Models;

using Microsoft.AspNetCore.SignalR;

using Polly;
using Polly.CircuitBreaker;

namespace ConduitLLM.Gateway.Services
{
    public partial class SignalRMessageQueueService
    {
        private async Task<bool> ProcessSingleMessageAsync(QueuedMessage queuedMessage)
        {
            queuedMessage.DeliveryAttempts++;
            queuedMessage.LastAttemptAt = DateTime.UtcNow;

            var context = new Context();
            context["message"] = queuedMessage;

            try
            {
                var result = await _circuitBreaker.ExecuteAsync(async (ctx) =>
                {
                    return await _retryPolicy.ExecuteAsync(async (retryCtx) =>
                    {
                        return await DeliverMessageAsync(queuedMessage);
                    }, ctx);
                }, context);

                if (result)
                {
                    Interlocked.Increment(ref _processedMessages);
                    Interlocked.Exchange(ref _consecutiveFailures, 0);
                    _logger.LogInformation(
                        "Successfully delivered message {MessageId} after {Attempts} attempts",
                        queuedMessage.Message.MessageId, queuedMessage.DeliveryAttempts);
                }
                else
                {
                    Interlocked.Increment(ref _failedMessages);
                    Interlocked.Increment(ref _consecutiveFailures);
                }

                return result;
            }
            catch (BrokenCircuitException)
            {
                _logger.LogWarning(
                    "Circuit breaker is open, message {MessageId} delivery postponed",
                    queuedMessage.Message.MessageId);
                queuedMessage.LastError = "Circuit breaker open";
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error delivering message {MessageId}",
                    queuedMessage.Message.MessageId);
                queuedMessage.LastError = ex.Message;
                Interlocked.Increment(ref _failedMessages);
                Interlocked.Increment(ref _consecutiveFailures);
                return false;
            }
        }

        private async Task<bool> DeliverMessageAsync(QueuedMessage queuedMessage)
        {
            using var scope = _serviceProvider.CreateScope();
            var hubContext = GetHubContext(scope, queuedMessage.HubName);

            if (hubContext == null)
            {
                _logger.LogError("Could not find hub context for {HubName}", queuedMessage.HubName);
                queuedMessage.LastError = $"Hub {queuedMessage.HubName} not found";
                return false;
            }

            try
            {
                // Update retry count
                queuedMessage.Message.RetryCount = queuedMessage.DeliveryAttempts - 1;

                // Send the message
                if (!string.IsNullOrEmpty(queuedMessage.ConnectionId))
                {
                    // Direct message to specific connection
                    await hubContext.Clients.Client(queuedMessage.ConnectionId)
                        .SendAsync(queuedMessage.MethodName, queuedMessage.Message);
                }
                else if (!string.IsNullOrEmpty(queuedMessage.GroupName))
                {
                    // Message to group
                    await hubContext.Clients.Group(queuedMessage.GroupName)
                        .SendAsync(queuedMessage.MethodName, queuedMessage.Message);
                }
                else
                {
                    _logger.LogError("Message {MessageId} has no target connection or group",
                        queuedMessage.Message.MessageId);
                    return false;
                }

                // Register for acknowledgment if it's a critical message
                if (queuedMessage.Message.IsCritical)
                {
                    var pending = await _acknowledgmentService.RegisterMessageAsync(
                        queuedMessage.Message,
                        queuedMessage.ConnectionId ?? "group-message",
                        queuedMessage.HubName,
                        queuedMessage.MethodName,
                        queuedMessage.AcknowledgmentTimeout);

                    // Wait for acknowledgment
                    var acknowledged = await pending.CompletionSource.Task;
                    return acknowledged;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error delivering message {MessageId} to {HubName}.{MethodName}",
                    queuedMessage.Message.MessageId, queuedMessage.HubName, queuedMessage.MethodName);
                queuedMessage.LastError = ex.Message;
                return false;
            }
        }

        private static IHubContext<Hub>? GetHubContext(IServiceScope scope, string hubName)
            => Utilities.SignalRHubContextResolver.Resolve(scope.ServiceProvider, hubName);

        private DateTime CalculateNextDeliveryTime(int attempts)
        {
            var delay = TimeSpan.FromSeconds(Math.Min(
                _initialRetryDelay.TotalSeconds * Math.Pow(2, Math.Max(0, attempts - 1)),
                _maxRetryDelay.TotalSeconds));

            return DateTime.UtcNow.Add(delay);
        }
    }
}

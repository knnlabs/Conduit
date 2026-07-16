using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Polly;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Base class for SignalR notification services that provides common functionality
    /// for sending notifications with optional resilience policies.
    /// </summary>
    /// <typeparam name="THub">The SignalR hub type</typeparam>
    public abstract class SignalRNotificationServiceBase<THub> where THub : Hub
    {
        /// <summary>
        /// The SignalR hub context for sending messages
        /// </summary>
        protected readonly IHubContext<THub> HubContext;

        /// <summary>
        /// Logger for recording notification events
        /// </summary>
        protected readonly ILogger Logger;

        /// <summary>
        /// Optional resilience policy for retry/circuit breaker support
        /// </summary>
        private readonly IAsyncPolicy? _resiliencePolicy;

        /// <summary>
        /// Initializes a new instance without resilience (simple notifications)
        /// </summary>
        protected SignalRNotificationServiceBase(
            IHubContext<THub> hubContext,
            ILogger logger)
        {
            HubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _resiliencePolicy = null;
        }

        /// <summary>
        /// Initializes a new instance with an optional resilience policy
        /// </summary>
        protected SignalRNotificationServiceBase(
            IHubContext<THub> hubContext,
            ILogger logger,
            IAsyncPolicy? resiliencePolicy)
            : this(hubContext, logger)
        {
            _resiliencePolicy = resiliencePolicy;
        }

        /// <summary>
        /// Sends a notification to a specific group.
        /// Handles errors gracefully and logs appropriately.
        /// </summary>
        /// <param name="groupName">The SignalR group name</param>
        /// <param name="methodName">The hub method name to invoke</param>
        /// <param name="payload">The notification payload</param>
        /// <param name="operationName">Operation name for logging (auto-filled by caller)</param>
        protected async Task SendToGroupAsync(
            string groupName,
            string methodName,
            object payload,
            [CallerMemberName] string operationName = "")
        {
            await ExecuteWithHandlingAsync(
                async () => await HubContext.Clients.Group(groupName).SendAsync(methodName, payload),
                operationName,
                $"group {groupName}");
        }

        /// <summary>
        /// Sends a notification to a specific group with multiple arguments.
        /// </summary>
        protected async Task SendToGroupAsync(
            string groupName,
            string methodName,
            object[] args,
            [CallerMemberName] string operationName = "")
        {
            await ExecuteWithHandlingAsync(
                async () =>
                {
                    // Use reflection-based SendCoreAsync for multiple arguments
                    await HubContext.Clients.Group(groupName).SendCoreAsync(methodName, args);
                },
                operationName,
                $"group {groupName}");
        }

        /// <summary>
        /// Sends a notification to all connected clients.
        /// </summary>
        protected async Task SendToAllAsync(
            string methodName,
            object payload,
            [CallerMemberName] string operationName = "")
        {
            await ExecuteWithHandlingAsync(
                async () => await HubContext.Clients.All.SendAsync(methodName, payload),
                operationName,
                "all clients");
        }

        /// <summary>
        /// Sends a notification to all connected clients with multiple arguments.
        /// </summary>
        protected async Task SendToAllAsync(
            string methodName,
            object[] args,
            [CallerMemberName] string operationName = "")
        {
            await ExecuteWithHandlingAsync(
                async () => await HubContext.Clients.All.SendCoreAsync(methodName, args),
                operationName,
                "all clients");
        }

        /// <summary>
        /// Sends a notification to a specific connection.
        /// </summary>
        protected async Task SendToConnectionAsync(
            string connectionId,
            string methodName,
            object payload,
            [CallerMemberName] string operationName = "")
        {
            await ExecuteWithHandlingAsync(
                async () => await HubContext.Clients.Client(connectionId).SendAsync(methodName, payload),
                operationName,
                $"connection {connectionId}");
        }

        /// <summary>
        /// Executes a SignalR operation with error handling and optional resilience.
        /// </summary>
        private async Task ExecuteWithHandlingAsync(
            Func<Task> operation,
            string operationName,
            string target)
        {
            try
            {
                if (_resiliencePolicy != null)
                {
                    await _resiliencePolicy.ExecuteAsync(operation);
                }
                else
                {
                    await operation();
                }

                Logger.LogDebug("{Operation} notification sent to {Target}", operationName, target);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to send {Operation} notification to {Target}", operationName, target);
                // Don't rethrow - notifications should not break the main flow
            }
        }

        /// <summary>
        /// Executes a SignalR operation with error handling but rethrows exceptions.
        /// Use this when the caller needs to know about failures.
        /// </summary>
        protected async Task ExecuteWithThrowAsync(
            Func<Task> operation,
            string operationName,
            string target)
        {
            try
            {
                if (_resiliencePolicy != null)
                {
                    await _resiliencePolicy.ExecuteAsync(operation);
                }
                else
                {
                    await operation();
                }

                Logger.LogDebug("{Operation} notification sent to {Target}", operationName, target);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to send {Operation} notification to {Target}", operationName, target);
                throw;
            }
        }
    }

    /// <summary>
    /// Provides pre-configured resilience policies for SignalR notifications.
    /// </summary>
    public static class SignalRResiliencePolicies
    {
        /// <summary>
        /// Creates a standard resilience policy with retry and circuit breaker.
        /// </summary>
        /// <param name="maxRetries">Maximum number of retries (default: 3)</param>
        /// <param name="circuitBreakerThreshold">Number of failures before circuit opens (default: 5)</param>
        /// <param name="circuitBreakerDuration">Duration circuit stays open (default: 30 seconds)</param>
        public static IAsyncPolicy CreateStandardPolicy(
            int maxRetries = 3,
            int circuitBreakerThreshold = 5,
            TimeSpan? circuitBreakerDuration = null)
        {
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    maxRetries,
                    attempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt - 1)));

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    circuitBreakerThreshold,
                    circuitBreakerDuration ?? TimeSpan.FromSeconds(30));

            return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
        }

        /// <summary>
        /// Creates a simple retry policy without circuit breaker.
        /// </summary>
        public static IAsyncPolicy CreateRetryOnlyPolicy(int maxRetries = 3)
        {
            return Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    maxRetries,
                    attempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt - 1)));
        }
    }
}

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using ConduitLLM.Core.Interfaces;
namespace ConduitLLM.Core.Configuration
{
    /// <summary>
    /// Default implementation of IOperationTimeoutProvider that reads timeout configurations from settings.
    /// </summary>
    public class OperationTimeoutProvider : IOperationTimeoutProvider
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<OperationTimeoutProvider> _logger;
        private readonly Dictionary<string, TimeSpan> _timeoutCache = new();
        private readonly object _cacheLock = new();

        public OperationTimeoutProvider(IConfiguration configuration, ILogger<OperationTimeoutProvider> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public TimeSpan GetTimeout(string operationType)
        {
            if (string.IsNullOrWhiteSpace(operationType))
            {
                throw new ArgumentException("Operation type cannot be null or empty.", nameof(operationType));
            }

            lock (_cacheLock)
            {
                if (_timeoutCache.TryGetValue(operationType, out var cachedTimeout))
                {
                    return cachedTimeout;
                }

                var configKey = $"ConduitLLM:Timeouts:{operationType}";
                var timeoutValue = _configuration.GetValue<int?>($"{configKey}:Seconds");

                TimeSpan timeout;
                if (timeoutValue.HasValue)
                {
                    timeout = TimeSpan.FromSeconds(timeoutValue.Value);
                    _logger.LogInformation("Timeout for operation '{OperationType}': {Timeout} seconds", operationType, timeoutValue.Value);
                }
                else
                {
                    // Default timeouts based on operation type
                    timeout = GetDefaultTimeout(operationType);
                    _logger.LogInformation("Using default timeout for operation '{OperationType}': {Timeout} seconds", operationType, timeout.TotalSeconds);
                }

                _timeoutCache[operationType] = timeout;
                return timeout;
            }
        }

        /// <inheritdoc />
        public bool ShouldApplyTimeout(string operationType)
        {
            if (string.IsNullOrWhiteSpace(operationType))
            {
                return true; // Apply timeout by default
            }

            var configKey = $"ConduitLLM:Timeouts:{operationType}:Enabled";
            var enabled = _configuration.GetValue<bool?>(configKey);

            // Some operations should never have timeouts
            if (operationType.Equals("streaming", StringComparison.OrdinalIgnoreCase) ||
                operationType.Equals("websocket", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return enabled ?? true; // Apply timeout by default
        }

        /// <inheritdoc />
        public TimeSpan GetTimeoutOrDefault(string operationType, TimeSpan defaultTimeout)
        {
            try
            {
                return GetTimeout(operationType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get timeout for operation '{OperationType}', using default: {Default} seconds", 
                    operationType, defaultTimeout.TotalSeconds);
                return defaultTimeout;
            }
        }

        private TimeSpan GetDefaultTimeout(string operationType)
        {
            return operationType?.ToLowerInvariant() switch
            {
                "chat" => TimeSpan.FromSeconds(30),
                "completion" => TimeSpan.FromSeconds(60),
                "image-generation" => TimeSpan.FromSeconds(120),
                "video-generation" => TimeSpan.FromMinutes(10),
                "video-polling" => TimeSpan.FromMinutes(15),
                "polling" => TimeSpan.FromMinutes(5),
                "health-check" => TimeSpan.FromSeconds(5),
                "model-discovery" => TimeSpan.FromSeconds(10),
                _ => TimeSpan.FromSeconds(100) // Default fallback
            };
        }
    }
}
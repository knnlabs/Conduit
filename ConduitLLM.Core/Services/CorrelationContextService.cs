using System.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Service for managing correlation context across service boundaries.
    /// </summary>
    public interface ICorrelationContextService
    {
        /// <summary>
        /// Gets the current correlation ID.
        /// </summary>
        string? CorrelationId { get; }

        /// <summary>
        /// Gets the current trace ID (W3C format).
        /// </summary>
        string? TraceId { get; }

        /// <summary>
        /// Gets the current span ID.
        /// </summary>
        string? SpanId { get; }

        /// <summary>
        /// Gets headers to propagate correlation context.
        /// </summary>
        /// <returns>Dictionary of header names and values.</returns>
        Dictionary<string, string> GetPropagationHeaders();

        /// <summary>
        /// Creates a new correlation scope.
        /// </summary>
        /// <param name="correlationId">The correlation ID to use.</param>
        /// <returns>A disposable scope.</returns>
        IDisposable CreateScope(string correlationId);
    }

    /// <summary>
    /// Implementation of correlation context service.
    /// </summary>
    public class CorrelationContextService : ICorrelationContextService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<CorrelationContextService> _logger;
        private static readonly AsyncLocal<string?> _asyncLocalCorrelationId = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="CorrelationContextService"/> class.
        /// </summary>
        public CorrelationContextService(
            IHttpContextAccessor httpContextAccessor,
            ILogger<CorrelationContextService> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        /// <inheritdoc />
        public string? CorrelationId
        {
            get
            {
                // First, try to get from HTTP context
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null)
                {
                    if (httpContext.Items.TryGetValue("CorrelationId", out var contextCorrelationId))
                    {
                        return contextCorrelationId as string;
                    }
                    
                    // Fallback to TraceIdentifier
                    if (!string.IsNullOrEmpty(httpContext.TraceIdentifier))
                    {
                        return httpContext.TraceIdentifier;
                    }
                }

                // Try AsyncLocal storage (for background tasks)
                if (!string.IsNullOrEmpty(_asyncLocalCorrelationId.Value))
                {
                    return _asyncLocalCorrelationId.Value;
                }

                // Try Activity
                var activity = Activity.Current;
                if (activity != null)
                {
                    var baggage = activity.GetBaggageItem("correlation.id");
                    if (!string.IsNullOrEmpty(baggage))
                    {
                        return baggage;
                    }
                }

                return null;
            }
        }

        /// <inheritdoc />
        public string? TraceId
        {
            get
            {
                var activity = Activity.Current;
                if (activity != null && activity.TraceId != default)
                {
                    return activity.TraceId.ToString();
                }
                return null;
            }
        }

        /// <inheritdoc />
        public string? SpanId
        {
            get
            {
                var activity = Activity.Current;
                if (activity != null && activity.SpanId != default)
                {
                    return activity.SpanId.ToString();
                }
                return null;
            }
        }

        /// <inheritdoc />
        public Dictionary<string, string> GetPropagationHeaders()
        {
            var headers = new Dictionary<string, string>();

            // Add correlation ID
            var correlationId = CorrelationId;
            if (!string.IsNullOrEmpty(correlationId))
            {
                headers["X-Correlation-ID"] = correlationId;
                headers["X-Request-ID"] = correlationId;
            }

            // Add W3C Trace Context if available
            var activity = Activity.Current;
            if (activity != null)
            {
                // Add traceparent header
                if (activity.TraceId != default && activity.SpanId != default)
                {
                    var traceparent = $"00-{activity.TraceId}-{activity.SpanId}-{(activity.Recorded ? "01" : "00")}";
                    headers["traceparent"] = traceparent;
                }

                // Add tracestate if present
                var traceState = activity.TraceStateString;
                if (!string.IsNullOrEmpty(traceState))
                {
                    headers["tracestate"] = traceState;
                }

                // Add baggage
                foreach (var baggage in activity.Baggage)
                {
                    if (baggage.Key.StartsWith("correlation.") || baggage.Key.StartsWith("context."))
                    {
                        headers[$"X-Context-{baggage.Key}"] = baggage.Value ?? string.Empty;
                    }
                }
            }

            _logger.LogDebug("Generated {Count} propagation headers for correlation context", headers.Count);

            return headers;
        }

        /// <inheritdoc />
        public IDisposable CreateScope(string correlationId)
        {
            return new CorrelationScope(correlationId);
        }

        /// <summary>
        /// Correlation scope for managing correlation context.
        /// </summary>
        private class CorrelationScope : IDisposable
        {
            private readonly string? _previousCorrelationId;
            private readonly Activity? _activity;
            private readonly string? _previousBaggage;

            public CorrelationScope(string correlationId)
            {
                _previousCorrelationId = _asyncLocalCorrelationId.Value;
                _asyncLocalCorrelationId.Value = correlationId;

                // Also set on current activity if available
                _activity = Activity.Current;
                if (_activity != null)
                {
                    _previousBaggage = _activity.GetBaggageItem("correlation.id");
                    _activity.SetBaggage("correlation.id", correlationId);
                }
                else
                {
                    _previousBaggage = null;
                }
            }

            public void Dispose()
            {
                _asyncLocalCorrelationId.Value = _previousCorrelationId;
                
                if (_activity != null)
                {
                    if (_previousBaggage != null)
                    {
                        _activity.SetBaggage("correlation.id", _previousBaggage);
                    }
                    else
                    {
                        // Remove the baggage item if there was no previous value
                        _activity.SetBaggage("correlation.id", null);
                    }
                }
            }
        }
    }
}
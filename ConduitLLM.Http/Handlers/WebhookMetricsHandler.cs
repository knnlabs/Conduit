using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace ConduitLLM.Http.Handlers
{
    /// <summary>
    /// HTTP message handler that collects metrics for webhook deliveries.
    /// </summary>
    public class WebhookMetricsHandler : DelegatingHandler
    {
        private static readonly Counter WebhookRequests = Prometheus.Metrics
            .CreateCounter("conduit_webhook_requests_total", "Total webhook requests", 
                new CounterConfiguration
                {
                    LabelNames = new[] { "status", "endpoint" }
                });

        private static readonly Counter WebhookTimeouts = Prometheus.Metrics
            .CreateCounter("conduit_webhook_timeouts_total", "Total webhook timeouts", 
                new CounterConfiguration
                {
                    LabelNames = new[] { "endpoint" }
                });

        private static readonly Histogram WebhookDuration = Prometheus.Metrics
            .CreateHistogram("conduit_webhook_duration_ms", "Webhook request duration in milliseconds",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "status", "endpoint" },
                    Buckets = Histogram.ExponentialBuckets(10, 2, 10) // 10ms to ~10s
                });

        private static readonly Gauge ActiveWebhookRequests = Prometheus.Metrics
            .CreateGauge("conduit_webhook_active_requests", "Number of active webhook requests");

        private readonly ILogger<WebhookMetricsHandler> _logger;

        public WebhookMetricsHandler(ILogger<WebhookMetricsHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var endpoint = request.RequestUri?.Host ?? "unknown";
            HttpResponseMessage? response = null;

            ActiveWebhookRequests.Inc();

            try
            {
                response = await base.SendAsync(request, cancellationToken);

                // Record successful request
                var statusCode = ((int)response.StatusCode).ToString();
                WebhookRequests.WithLabels(statusCode, endpoint).Inc();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Webhook request to {Endpoint} returned {StatusCode}", 
                        endpoint, response.StatusCode);
                }

                return response;
            }
            catch (TaskCanceledException ex)
            {
                // Record timeout
                WebhookTimeouts.WithLabels(endpoint).Inc();
                WebhookRequests.WithLabels("timeout", endpoint).Inc();
                
                _logger.LogWarning(ex, "Webhook request to {Endpoint} timed out after {ElapsedMs}ms", 
                    endpoint, stopwatch.ElapsedMilliseconds);
                throw;
            }
            catch (HttpRequestException ex)
            {
                // Record connection error
                WebhookRequests.WithLabels("error", endpoint).Inc();
                
                _logger.LogError(ex, "HTTP error sending webhook to {Endpoint}", endpoint);
                throw;
            }
            finally
            {
                ActiveWebhookRequests.Dec();

                // Record duration
                var status = response?.StatusCode.ToString() ?? "error";
                WebhookDuration.WithLabels(status, endpoint).Observe(stopwatch.ElapsedMilliseconds);

                // Log slow requests
                if (stopwatch.ElapsedMilliseconds > 5000)
                {
                    _logger.LogWarning("Slow webhook request to {Endpoint}: {ElapsedMs}ms", 
                        endpoint, stopwatch.ElapsedMilliseconds);
                }
            }
        }
    }
}
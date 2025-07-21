using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.HealthChecks
{
    /// <summary>
    /// Health check for monitoring API endpoint availability and performance
    /// </summary>
    public class ApiEndpointHealthCheck : IHealthCheck
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _endpointUrl;
        private readonly string _endpointName;
        private readonly ILogger<ApiEndpointHealthCheck> _logger;
        private readonly int _timeoutMs;
        private readonly int _warningThresholdMs;

        public ApiEndpointHealthCheck(
            IHttpClientFactory httpClientFactory,
            ILogger<ApiEndpointHealthCheck> logger,
            string endpointUrl,
            string endpointName,
            int timeoutMs = 5000,
            int warningThresholdMs = 1000)
        {
            _httpClientFactory = httpClientFactory;
            _endpointUrl = endpointUrl;
            _endpointName = endpointName;
            _logger = logger;
            _timeoutMs = timeoutMs;
            _warningThresholdMs = warningThresholdMs;
        }

        /// <summary>
        /// Check API endpoint health
        /// </summary>
        public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var data = new Dictionary<string, object>
            {
                ["endpoint"] = _endpointName,
                ["url"] = _endpointUrl
            };

            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_timeoutMs);

                var response = await httpClient.GetAsync(_endpointUrl, cts.Token);
                stopwatch.Stop();

                data["responseTime"] = stopwatch.ElapsedMilliseconds;
                data["statusCode"] = (int)response.StatusCode;
                data["reasonPhrase"] = response.ReasonPhrase ?? string.Empty;

                if (response.IsSuccessStatusCode)
                {
                    if (stopwatch.ElapsedMilliseconds > _warningThresholdMs)
                    {
                        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                            $"API endpoint {_endpointName} is slow (response time: {stopwatch.ElapsedMilliseconds}ms)",
                            data: data);
                    }

                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                        $"API endpoint {_endpointName} is healthy (response time: {stopwatch.ElapsedMilliseconds}ms)",
                        data: data);
                }

                // Check for specific status codes
                if ((int)response.StatusCode >= 500)
                {
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                        $"API endpoint {_endpointName} returned server error: {response.StatusCode}",
                        data: data);
                }

                if ((int)response.StatusCode >= 400)
                {
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                        $"API endpoint {_endpointName} returned client error: {response.StatusCode}",
                        data: data);
                }

                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                    $"API endpoint {_endpointName} returned unexpected status: {response.StatusCode}",
                    data: data);
            }
            catch (TaskCanceledException)
            {
                stopwatch.Stop();
                data["responseTime"] = stopwatch.ElapsedMilliseconds;
                data["error"] = "Timeout";

                _logger.LogError("API endpoint {Endpoint} health check timed out after {Timeout}ms",
                    _endpointName, _timeoutMs);

                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                    $"API endpoint {_endpointName} timed out after {_timeoutMs}ms",
                    data: data);
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                data["responseTime"] = stopwatch.ElapsedMilliseconds;
                data["error"] = ex.Message;

                _logger.LogError(ex, "API endpoint {Endpoint} health check failed", _endpointName);

                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                    $"API endpoint {_endpointName} is unreachable",
                    exception: ex,
                    data: data);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                data["responseTime"] = stopwatch.ElapsedMilliseconds;
                data["error"] = ex.Message;

                _logger.LogError(ex, "Unexpected error during API endpoint {Endpoint} health check", _endpointName);

                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                    $"API endpoint {_endpointName} health check failed",
                    exception: ex,
                    data: data);
            }
        }
    }
}
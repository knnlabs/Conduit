using System.Text.Json;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Http.Middleware
{
    /// <summary>
    /// Static helper methods for billing policy decisions and audit logging.
    /// Implements customer-friendly billing policy following Anthropic's approach.
    /// </summary>
    public static class BillingPolicyHandler
    {
        /// <summary>
        /// Logs billing decisions for transparency and audit purposes.
        /// Tracks when billing is skipped due to error responses or other policy reasons.
        /// </summary>
        public static Task LogBillingDecisionAsync(HttpContext context, IBillingAuditService billingAuditService, ILogger logger)
        {
            // Only log for API endpoints that would normally be tracked
            if (!context.Request.Path.StartsWithSegments("/v1"))
                return Task.CompletedTask;

            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
            var isTrackableEndpoint = path.Contains("/completions") || 
                                    path.Contains("/embeddings") || 
                                    path.Contains("/images/generations") ||
                                    path.Contains("/audio/transcriptions") ||
                                    path.Contains("/audio/speech") ||
                                    path.Contains("/videos/generations");

            if (!isTrackableEndpoint)
                return Task.CompletedTask;

            var virtualKeyId = context.Items.TryGetValue("VirtualKeyId", out var keyId) ? keyId : "none";
            var statusCode = context.Response.StatusCode;
            var requestId = context.TraceIdentifier;

            // Log reason for skipping billing
            if (statusCode >= 400)
            {
                logger.LogDebug(
                    "Billing Policy: Skipping billing for error response - " +
                    "Status={StatusCode}, VirtualKey={VirtualKeyId}, Path={Path}, RequestId={RequestId}, " +
                    "Reason=ErrorResponse_NoChargePolicy", 
                    statusCode, virtualKeyId, context.Request.Path, requestId);
                
                // Audit log error response skipped
                var providerType = context.Items.TryGetValue("ProviderType", out var pt) ? pt?.ToString() : "unknown";
                billingAuditService.LogBillingEvent(new BillingAuditEvent
                {
                    EventType = BillingAuditEventType.ErrorResponseSkipped,
                    VirtualKeyId = virtualKeyId is int vkId ? vkId : null,
                    RequestId = requestId,
                    RequestPath = context.Request.Path.ToString(),
                    HttpStatusCode = statusCode,
                    FailureReason = $"HTTP {statusCode} error response - no billing per policy",
                    ProviderType = providerType
                });
                
                // Increment metrics
                UsageMetrics.BillingAuditEvents.WithLabels("ErrorResponseSkipped", providerType ?? "unknown").Inc();
            }
            else if (!context.Items.ContainsKey("VirtualKeyId"))
            {
                logger.LogDebug(
                    "Billing Policy: Skipping billing - no virtual key found - " +
                    "Status={StatusCode}, Path={Path}, RequestId={RequestId}, " +
                    "Reason=NoVirtualKey", 
                    statusCode, context.Request.Path, requestId);
                
                // Audit log no virtual key
                billingAuditService.LogBillingEvent(new BillingAuditEvent
                {
                    EventType = BillingAuditEventType.NoVirtualKey,
                    RequestId = requestId,
                    RequestPath = context.Request.Path.ToString(),
                    HttpStatusCode = statusCode,
                    FailureReason = "No virtual key found for request"
                });
                
                // Increment metrics
                UsageMetrics.BillingAuditEvents.WithLabels("NoVirtualKey", "unknown").Inc();
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Logs successful billing event with usage data.
        /// </summary>
        public static void LogSuccessfulBilling(HttpContext context, string model, Usage usage, decimal cost, 
            string providerType, IBillingAuditService billingAuditService, ILogger logger)
        {
            var virtualKeyId = (int)context.Items["VirtualKeyId"]!;
            
            billingAuditService.LogBillingEvent(new BillingAuditEvent
            {
                EventType = BillingAuditEventType.UsageTracked,
                VirtualKeyId = virtualKeyId,
                Model = model,
                RequestId = context.TraceIdentifier,
                UsageJson = JsonSerializer.Serialize(usage),
                CalculatedCost = cost,
                ProviderType = providerType,
                RequestPath = context.Request.Path.ToString(),
                HttpStatusCode = context.Response.StatusCode
            });
            
            // Increment metrics
            UsageMetrics.BillingAuditEvents.WithLabels("UsageTracked", providerType ?? "unknown").Inc();
            UsageMetrics.BillingRevenue.WithLabels(model ?? "unknown", providerType ?? "unknown").Inc(Convert.ToDouble(cost));
            UsageMetrics.BillingCostDistribution.WithLabels(model ?? "unknown", providerType ?? "unknown").Observe(Convert.ToDouble(cost));
        }

        /// <summary>
        /// Logs billing event for zero cost calculations.
        /// </summary>
        public static void LogZeroCostBilling(HttpContext context, string model, Usage usage, decimal cost, 
            string providerType, IBillingAuditService billingAuditService)
        {
            var virtualKeyId = (int)context.Items["VirtualKeyId"]!;
            
            billingAuditService.LogBillingEvent(new BillingAuditEvent
            {
                EventType = BillingAuditEventType.ZeroCostSkipped,
                VirtualKeyId = virtualKeyId,
                Model = model,
                RequestId = context.TraceIdentifier,
                UsageJson = JsonSerializer.Serialize(usage),
                CalculatedCost = cost,
                ProviderType = providerType,
                RequestPath = context.Request.Path.ToString(),
                HttpStatusCode = context.Response.StatusCode
            });
            
            // Increment metrics
            UsageMetrics.BillingAuditEvents.WithLabels("ZeroCostSkipped", providerType ?? "unknown").Inc();
            UsageMetrics.ZeroCostEvents.WithLabels(model ?? "unknown", "calculated_zero").Inc();
        }

        /// <summary>
        /// Logs billing event for missing usage data.
        /// </summary>
        public static void LogMissingUsageData(HttpContext context, IBillingAuditService billingAuditService)
        {
            var vkId = context.Items.ContainsKey("VirtualKeyId") ? (int?)context.Items["VirtualKeyId"] : null;
            var providerType = context.Items.TryGetValue("ProviderType", out var pt) ? pt?.ToString() : "unknown";
            
            billingAuditService.LogBillingEvent(new BillingAuditEvent
            {
                EventType = BillingAuditEventType.MissingUsageData,
                VirtualKeyId = vkId,
                RequestId = context.TraceIdentifier,
                RequestPath = context.Request.Path.ToString(),
                HttpStatusCode = context.Response.StatusCode,
                ProviderType = providerType
            });
            
            // Increment metrics
            UsageMetrics.BillingAuditEvents.WithLabels("MissingUsageData", providerType ?? "unknown").Inc();
            UsageMetrics.BillingRevenueLoss.WithLabels("MissingUsageData", "no_usage_in_response").Inc();
        }

        /// <summary>
        /// Logs billing event for streaming usage.
        /// </summary>
        public static void LogStreamingBilling(HttpContext context, string model, Usage usage, decimal cost, 
            string providerType, bool isEstimated, IBillingAuditService billingAuditService, ILogger logger)
        {
            var virtualKeyId = (int)context.Items["VirtualKeyId"]!;
            var eventType = isEstimated ? BillingAuditEventType.UsageEstimated : BillingAuditEventType.UsageTracked;
            
            billingAuditService.LogBillingEvent(new BillingAuditEvent
            {
                EventType = eventType,
                VirtualKeyId = virtualKeyId,
                Model = model,
                RequestId = context.TraceIdentifier,
                UsageJson = JsonSerializer.Serialize(usage),
                CalculatedCost = cost,
                ProviderType = providerType,
                RequestPath = context.Request.Path.ToString(),
                HttpStatusCode = context.Response.StatusCode,
                IsEstimated = isEstimated,
                FailureReason = isEstimated ? "Provider did not return usage data - usage was estimated conservatively" : null
            });
            
            // Increment metrics
            UsageMetrics.BillingAuditEvents.WithLabels(eventType.ToString(), providerType ?? "unknown").Inc();
            
            // Track estimated vs actual usage metrics
            if (isEstimated)
            {
                logger.LogInformation("Successfully billed estimated usage for streaming response: Cost={Cost:C}", cost);
                // Track that we recovered revenue through estimation
                UsageMetrics.BillingRevenue.WithLabels(model ?? "unknown", providerType ?? "unknown_estimated").Inc(Convert.ToDouble(cost));
            }
            UsageMetrics.BillingRevenue.WithLabels(model ?? "unknown", providerType ?? "unknown").Inc(Convert.ToDouble(cost));
            UsageMetrics.BillingCostDistribution.WithLabels(model ?? "unknown", providerType ?? "unknown").Observe(Convert.ToDouble(cost));
        }

        /// <summary>
        /// Logs billing event for missing streaming usage data.
        /// </summary>
        public static void LogMissingStreamingUsage(HttpContext context, IBillingAuditService billingAuditService)
        {
            var vkId = context.Items.ContainsKey("VirtualKeyId") ? (int?)context.Items["VirtualKeyId"] : null;
            var providerType = context.Items.TryGetValue("ProviderType", out var pt) ? pt?.ToString() : "unknown";
            
            billingAuditService.LogBillingEvent(new BillingAuditEvent
            {
                EventType = BillingAuditEventType.StreamingUsageMissing,
                VirtualKeyId = vkId,
                RequestId = context.TraceIdentifier,
                RequestPath = context.Request.Path.ToString(),
                HttpStatusCode = context.Response.StatusCode,
                FailureReason = "No StreamingUsage in HttpContext.Items - estimation service may not be configured",
                ProviderType = providerType
            });
            
            // Increment metrics
            UsageMetrics.BillingAuditEvents.WithLabels("StreamingUsageMissing", providerType ?? "unknown").Inc();
            UsageMetrics.BillingRevenueLoss.WithLabels("StreamingUsageMissing", "streaming_no_usage").Inc();
        }

        /// <summary>
        /// Logs billing event for JSON parsing errors.
        /// </summary>
        public static void LogJsonParseError(HttpContext context, Exception ex, IBillingAuditService billingAuditService)
        {
            var virtualKeyId = context.Items.ContainsKey("VirtualKeyId") ? (int?)context.Items["VirtualKeyId"] : null;
            var providerType = context.Items.TryGetValue("ProviderType", out var pt) ? pt?.ToString() : "unknown";
            
            billingAuditService.LogBillingEvent(new BillingAuditEvent
            {
                EventType = BillingAuditEventType.JsonParseError,
                VirtualKeyId = virtualKeyId,
                RequestId = context.TraceIdentifier,
                RequestPath = context.Request.Path.ToString(),
                HttpStatusCode = context.Response.StatusCode,
                FailureReason = ex.Message,
                ProviderType = providerType
            });
            
            // Increment metrics
            UsageMetrics.BillingAuditEvents.WithLabels("JsonParseError", providerType ?? "unknown").Inc();
            UsageMetrics.BillingRevenueLoss.WithLabels("JsonParseError", "parsing_failed").Inc();
        }

        /// <summary>
        /// Logs billing event for unexpected errors.
        /// </summary>
        public static void LogUnexpectedError(HttpContext context, Exception ex, IBillingAuditService billingAuditService)
        {
            var virtualKeyId = context.Items.ContainsKey("VirtualKeyId") ? (int?)context.Items["VirtualKeyId"] : null;
            var providerType = context.Items.TryGetValue("ProviderType", out var pt) ? pt?.ToString() : "unknown";
            
            billingAuditService.LogBillingEvent(new BillingAuditEvent
            {
                EventType = BillingAuditEventType.UnexpectedError,
                VirtualKeyId = virtualKeyId,
                RequestId = context.TraceIdentifier,
                RequestPath = context.Request.Path.ToString(),
                HttpStatusCode = context.Response.StatusCode,
                FailureReason = ex.Message,
                ProviderType = providerType
            });
            
            // Increment metrics
            UsageMetrics.BillingAuditEvents.WithLabels("UnexpectedError", providerType ?? "unknown").Inc();
            UsageMetrics.BillingRevenueLoss.WithLabels("UnexpectedError", "exception").Inc();
        }
    }
}
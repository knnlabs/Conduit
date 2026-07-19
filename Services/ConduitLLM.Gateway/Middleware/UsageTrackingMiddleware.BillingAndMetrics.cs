using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Gateway.Constants;
using ConduitLLM.Gateway.Metrics;
using ConduitLLM.Gateway.Utilities;

namespace ConduitLLM.Gateway.Middleware
{
    public partial class UsageTrackingMiddleware
    {
        private async Task LogRequestAsync(
            HttpContext context,
            int virtualKeyId,
            string model,
            Usage usage,
            decimal cost,
            IRequestLogService requestLogService,
            string? metadata = null)
        {
            try
            {
                var requestType = UsageExtractor.DetermineRequestType(context.Request.Path);

                // Extract provider info from HttpContext.Items (set by controllers)
                int? providerId = context.Items.TryGetValue("ProviderId", out var providerIdObj) && providerIdObj is int pid
                    ? pid
                    : null;
                var providerType = context.Items.TryGetValue("ProviderType", out var providerTypeObj)
                    ? providerTypeObj?.ToString()
                    : null;

                metadata = AppendFailoverMetadata(context, metadata);

                var logRequest = new LogRequestDto
                {
                    VirtualKeyId = virtualKeyId,
                    ModelName = model,
                    ProviderId = providerId,
                    ProviderType = providerType,
                    RequestType = requestType,
                    InputTokens = usage.PromptTokens ?? 0,
                    OutputTokens = usage.CompletionTokens ?? 0,
                    CachedInputTokens = usage.CachedInputTokens,
                    CachedWriteTokens = usage.CachedWriteTokens,
                    Cost = cost,
                    ResponseTimeMs = UsageExtractor.GetResponseTime(context),
                    UserId = context.User?.Identity?.Name,
                    ClientIp = context.Connection.RemoteIpAddress?.ToString(),
                    RequestPath = context.Request.Path.ToString(),
                    StatusCode = context.Response.StatusCode,
                    Metadata = metadata
                };

                await requestLogService.LogRequestAsync(logRequest);

                _logger.LogInformation(
                    "Tracked usage for VirtualKey {VirtualKeyId}: Model={Model}, PromptTokens={PromptTokens}, CompletionTokens={CompletionTokens}, CachedInput={CachedInput}, CachedWrite={CachedWrite}, Cost={Cost:C}",
                    virtualKeyId, model, usage.PromptTokens, usage.CompletionTokens, usage.CachedInputTokens, usage.CachedWriteTokens, cost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log request for VirtualKey {VirtualKeyId}", virtualKeyId);
                // Don't throw - logging failure shouldn't break the request
            }
        }

        /// <summary>
        /// Records in-request failover details (attempts + serving key/provider) into the
        /// RequestLog metadata JSON so operators can audit which candidate actually served.
        /// Attribution overrides for cost (provider-level failover) ship separately.
        /// </summary>
        private string? AppendFailoverMetadata(HttpContext context, string? metadata)
        {
            try
            {
                var attribution = context.RequestServices
                    .GetService<ConduitLLM.Core.Services.IFailoverAttributionAccessor>();
                if (attribution is not { FailoverOccurred: true } || attribution.Current == null)
                {
                    return metadata;
                }

                var failover = new
                {
                    failover = new
                    {
                        attempts = attribution.AttemptCount,
                        servingProviderId = attribution.Current.ProviderId,
                        servingKeyCredentialId = attribution.Current.KeyCredentialId,
                    },
                };

                if (string.IsNullOrEmpty(metadata))
                {
                    return System.Text.Json.JsonSerializer.Serialize(failover);
                }

                // Merge into existing metadata JSON object if possible; otherwise leave as-is.
                var existing = System.Text.Json.Nodes.JsonNode.Parse(metadata);
                if (existing is System.Text.Json.Nodes.JsonObject obj)
                {
                    obj["failover"] = System.Text.Json.Nodes.JsonNode.Parse(
                        System.Text.Json.JsonSerializer.Serialize(failover.failover));
                    return obj.ToJsonString();
                }

                return metadata;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to append failover metadata to request log");
                return metadata;
            }
        }

        #region Billing Audit Logging

        private async Task LogBillingDecisionAsync(HttpContext context, IBillingAuditService billingAuditService)
        {
            await BillingPolicyHandler.LogBillingDecisionAsync(context, billingAuditService, _logger);
        }

        private void LogSuccessfulBilling(HttpContext context, string model, Usage usage, decimal cost,
            string providerType, IBillingAuditService billingAuditService, string? toolUsageJson = null, decimal? toolCost = null)
        {
            BillingPolicyHandler.LogSuccessfulBilling(context, model, usage, cost, providerType, billingAuditService, _logger, toolUsageJson, toolCost);
        }

        private void LogZeroCostBilling(HttpContext context, string model, Usage usage, decimal cost,
            string providerType, IBillingAuditService billingAuditService, string? toolUsageJson = null, decimal? toolCost = null)
        {
            BillingPolicyHandler.LogZeroCostBilling(context, model, usage, cost, providerType, billingAuditService, toolUsageJson, toolCost, _logger);
        }

        private void LogMissingUsageData(HttpContext context, IBillingAuditService billingAuditService)
        {
            BillingPolicyHandler.LogMissingUsageData(context, billingAuditService);
        }

        private void LogStreamingBilling(HttpContext context, string model, Usage usage, decimal cost,
            string providerType, bool isEstimated, IBillingAuditService billingAuditService, string? toolUsageJson = null, decimal? toolCost = null)
        {
            BillingPolicyHandler.LogStreamingBilling(context, model, usage, cost, providerType, isEstimated, billingAuditService, _logger, toolUsageJson, toolCost);
        }

        private void LogMissingStreamingUsage(HttpContext context, IBillingAuditService billingAuditService)
        {
            BillingPolicyHandler.LogMissingStreamingUsage(context, billingAuditService);
        }

        private void LogJsonParseError(HttpContext context, Exception ex, IBillingAuditService billingAuditService)
        {
            BillingPolicyHandler.LogJsonParseError(context, ex, billingAuditService);
        }

        private void LogUnexpectedError(HttpContext context, Exception ex, IBillingAuditService billingAuditService)
        {
            BillingPolicyHandler.LogUnexpectedError(context, ex, billingAuditService);
        }

        #endregion

        #region Prompt Caching Metrics

        /// <summary>
        /// Records prompt caching request-level metrics (hit/miss/disabled).
        /// </summary>
        private static void RecordPromptCachingMetrics(Usage usage, string model, string provider)
        {
            if (usage.CachedInputTokens.HasValue && usage.CachedInputTokens.Value > 0)
            {
                PromptCachingMetrics.RecordCacheHit(model, provider);
            }
            else if (usage.CachedWriteTokens.HasValue && usage.CachedWriteTokens.Value > 0)
            {
                // Cache write but no read — first request building the cache
                PromptCachingMetrics.RecordCacheMiss(model, provider);
            }
            else
            {
                PromptCachingMetrics.RecordCacheDisabled(model, provider);
            }
        }

        /// <summary>
        /// Calculates and records prompt caching cost savings.
        /// </summary>
        private static async Task RecordPromptCachingSavingsAsync(
            HttpContext context,
            ICostCalculationService costCalculationService,
            string model,
            Usage usage)
        {
            if (!usage.CachedInputTokens.HasValue || usage.CachedInputTokens.Value <= 0)
                return;

            try
            {
                decimal savings;
                var providerType = context.Items.TryGetValue("ProviderType", out var pt)
                    ? pt?.ToString() ?? "unknown"
                    : "unknown";

                if (context.Items.TryGetValue(HttpContextKeys.ModelCostId, out var mcIdObj) &&
                    mcIdObj is int mcId)
                {
                    savings = await costCalculationService.CalculateCacheSavingsByIdAsync(mcId, usage);
                }
                else
                {
                    savings = await costCalculationService.CalculateCacheSavingsAsync(model, usage);
                }

                PromptCachingMetrics.RecordSavings(model, providerType, Convert.ToDouble(savings));
            }
            catch
            {
                // Non-critical — don't fail the request pipeline for savings calculation
            }
        }

        #endregion
    }
}

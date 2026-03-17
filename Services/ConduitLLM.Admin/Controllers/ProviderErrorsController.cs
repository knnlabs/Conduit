using ConduitLLM.Admin.DTOs;
using ConduitLLM.Configuration.Events;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing provider error tracking and key status
    /// </summary>
    [ApiController]
    [Route("api/provider-errors")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class ProviderErrorsController : AdminControllerBase
    {
        private readonly IProviderErrorTrackingService _errorService;
        private readonly IProviderKeyCredentialRepository _keyRepo;
        private readonly IProviderRepository _providerRepo;
        private readonly IPublishEndpoint _publishEndpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderErrorsController"/> class.
        /// </summary>
        public ProviderErrorsController(
            IProviderErrorTrackingService errorService,
            IProviderKeyCredentialRepository keyRepo,
            IProviderRepository providerRepo,
            IPublishEndpoint publishEndpoint,
            ILogger<ProviderErrorsController> logger)
            : base(publishEndpoint, logger)
        {
            _errorService = errorService ?? throw new ArgumentNullException(nameof(errorService));
            _keyRepo = keyRepo ?? throw new ArgumentNullException(nameof(keyRepo));
            _providerRepo = providerRepo ?? throw new ArgumentNullException(nameof(providerRepo));
            _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        }

        /// <summary>
        /// Get recent errors across all providers
        /// </summary>
        /// <param name="providerId">Optional provider ID filter</param>
        /// <param name="keyId">Optional key ID filter</param>
        /// <param name="limit">Maximum number of errors to return (default: 100)</param>
        /// <returns>List of recent provider errors</returns>
        [HttpGet("recent")]
        public Task<IActionResult> GetRecentErrors(
            [FromQuery] int? providerId = null,
            [FromQuery] int? keyId = null,
            [FromQuery] int limit = 100)
        {
            return ExecuteAsync(
                async () =>
                {
                    if (limit > 1000)
                        limit = 1000; // Cap at 1000 for performance

                    var errors = await _errorService.GetRecentErrorsAsync(providerId, keyId, limit);

                    // Get provider names for display using efficient lookup
                    var providerMap = await _providerRepo.GetProviderNameMapAsync();

                    var dtos = errors.Select(e => new ProviderErrorDto
                    {
                        KeyCredentialId = e.KeyCredentialId,
                        ProviderId = e.ProviderId,
                        ProviderName = providerMap.GetValueOrDefault(e.ProviderId),
                        ErrorType = e.ErrorType.ToString(),
                        ErrorMessage = e.ErrorMessage,
                        HttpStatusCode = e.HttpStatusCode,
                        OccurredAt = e.OccurredAt,
                        IsFatal = e.IsFatal,
                        ModelName = e.ModelName
                    }).ToList();

                    return dtos;
                },
                result => Ok(result),
                "GetRecentErrors");
        }

        /// <summary>
        /// Get error summary for all providers
        /// </summary>
        /// <returns>List of provider error summaries</returns>
        [HttpGet("summary")]
        public Task<IActionResult> GetErrorSummary()
        {
            return ExecuteAsync(
                async () =>
                {
                    // Use paginated retrieval - get all providers in batches
                    var allProviders = new List<ConduitLLM.Configuration.Entities.Provider>();
                    var pageNumber = 1;
                    const int pageSize = 100;
                    int totalCount;

                    do
                    {
                        var (items, count) = await _providerRepo.GetPaginatedAsync(pageNumber, pageSize);
                        allProviders.AddRange(items);
                        totalCount = count;
                        pageNumber++;
                    } while (allProviders.Count < totalCount);

                    // Fetch all provider summaries in parallel to avoid N+1
                    var summaryTasks = allProviders.Select(async provider =>
                    {
                        var summary = await _errorService.GetProviderSummaryAsync(provider.Id);
                        return (provider, summary);
                    });

                    var results = await Task.WhenAll(summaryTasks);

                    var summaries = results
                        .Where(r => r.summary != null)
                        .Select(r => new ProviderErrorSummaryDto
                        {
                            ProviderId = r.provider.Id,
                            ProviderName = r.provider.ProviderName,
                            TotalErrors = r.summary!.TotalErrors,
                            FatalErrors = r.summary.FatalErrors,
                            Warnings = r.summary.Warnings,
                            DisabledKeyIds = r.summary.DisabledKeyIds,
                            LastError = r.summary.LastError
                        })
                        .ToList();

                    return summaries;
                },
                result => Ok(result),
                "GetErrorSummary");
        }

        /// <summary>
        /// Get detailed error information for a specific key
        /// </summary>
        /// <param name="keyId">ID of the key</param>
        /// <returns>Detailed error information for the key</returns>
        [HttpGet("keys/{keyId}")]
        public Task<IActionResult> GetKeyErrors(int keyId)
        {
            return ExecuteAsync(
                async () =>
                {
                    var details = await _errorService.GetKeyErrorDetailsAsync(keyId);
                    if (details == null)
                    {
                        throw new KeyNotFoundException($"No error data found for key {keyId}");
                    }

                    var dto = new KeyErrorDetailsDto
                    {
                        KeyId = details.KeyId,
                        KeyName = details.KeyName,
                        IsDisabled = details.IsDisabled,
                        DisabledAt = details.DisabledAt
                    };

                    if (details.FatalError != null)
                    {
                        dto.FatalError = new FatalErrorDto
                        {
                            ErrorType = details.FatalError.ErrorType.ToString(),
                            Count = details.FatalError.Count,
                            FirstSeen = details.FatalError.FirstSeen,
                            LastSeen = details.FatalError.LastSeen,
                            LastErrorMessage = details.FatalError.LastErrorMessage,
                            LastStatusCode = details.FatalError.LastStatusCode
                        };
                    }

                    dto.RecentWarnings = details.RecentWarnings.Select(w => new WarningErrorDto
                    {
                        Type = w.Type.ToString(),
                        Message = w.Message,
                        Timestamp = w.Timestamp
                    }).ToList();

                    return dto;
                },
                result => Ok(result),
                "GetKeyErrors",
                new { KeyId = keyId });
        }

        /// <summary>
        /// Clear errors and optionally re-enable a key
        /// </summary>
        /// <param name="keyId">ID of the key</param>
        /// <param name="request">Clear errors request</param>
        /// <returns>Operation result</returns>
        [HttpPost("keys/{keyId}/clear")]
        public Task<IActionResult> ClearKeyErrors(
            int keyId,
            [FromBody] ClearErrorsRequest request)
        {
            if (!request.ConfirmReenable && request.ReenableKey)
            {
                return Task.FromResult<IActionResult>(BadRequest(new { error = "Must confirm re-enabling the key" }));
            }

            return ExecuteAsync(
                async () =>
                {
                    // Look up the key to get its providerId for proper cleanup
                    var key = await _keyRepo.GetByIdAsync(keyId);
                    int? providerId = key?.ProviderId;

                    // Clear errors from Redis (including provider disabled keys cleanup)
                    await _errorService.ClearErrorsForKeyAsync(keyId, providerId);

                    // Re-enable the key if requested
                    if (request.ReenableKey && key != null && !key.IsEnabled)
                    {
                        key.IsEnabled = true;
                        await _keyRepo.UpdateAsync(key);

                        // Publish event for UI update
                        PublishEventFireAndForget(new ProviderKeyReenabledEvent
                        {
                            KeyId = keyId,
                            ProviderId = key.ProviderId,
                            ReenabledBy = User.Identity?.Name ?? "Admin",
                            Reason = request.Reason ?? "Manual re-enable after error resolution",
                            ReenabledAt = DateTime.UtcNow
                        }, "ClearKeyErrors");

                        LogAdminAudit("ClearedErrorsAndReenabled", "ProviderKeyCredential", keyId,
                            $"ProviderId: {key.ProviderId}");
                    }
                    else
                    {
                        LogAdminAudit("ClearedErrors", "ProviderKeyCredential", keyId);
                    }

                    return new
                    {
                        message = request.ReenableKey
                            ? "Errors cleared and key re-enabled successfully"
                            : "Errors cleared successfully",
                        keyId = keyId,
                        reenabled = request.ReenableKey
                    };
                },
                result => Ok(result),
                "ClearKeyErrors",
                new { KeyId = keyId });
        }

        /// <summary>
        /// Get error statistics for dashboard
        /// </summary>
        /// <param name="hours">Time window in hours (default: 24)</param>
        /// <returns>Error statistics</returns>
        [HttpGet("stats")]
        public Task<IActionResult> GetErrorStatistics(
            [FromQuery] int hours = 24)
        {
            return ExecuteAsync(
                async () =>
                {
                    if (hours > 168) // Cap at 1 week
                        hours = 168;

                    var window = TimeSpan.FromHours(hours);
                    var stats = await _errorService.GetErrorStatisticsAsync(window);

                    // Map provider IDs to names for the statistics
                    var providerNameMap = await _providerRepo.GetProviderNameMapAsync();
                    var errorsByProviderName = stats.ErrorsByProvider.ToDictionary(
                        kvp => providerNameMap.GetValueOrDefault(int.Parse(kvp.Key), $"Provider {kvp.Key}"),
                        kvp => kvp.Value);

                    var dto = new ErrorStatisticsDto
                    {
                        TotalErrors = stats.TotalErrors,
                        FatalErrors = stats.FatalErrors,
                        Warnings = stats.Warnings,
                        DisabledKeys = stats.DisabledKeys,
                        ErrorsByType = stats.ErrorsByType,
                        ErrorsByProvider = errorsByProviderName,
                        TimeWindow = window,
                        GeneratedAt = DateTime.UtcNow
                    };

                    return dto;
                },
                result => Ok(result),
                "GetErrorStatistics");
        }

        /// <summary>
        /// Get error counts by key for a specific provider
        /// </summary>
        /// <param name="providerId">Provider ID</param>
        /// <param name="hours">Time window in hours (default: 1)</param>
        /// <returns>Dictionary of key ID to error count</returns>
        [HttpGet("providers/{providerId}/key-errors")]
        public Task<IActionResult> GetErrorCountsByKey(
            int providerId,
            [FromQuery] int hours = 1)
        {
            return ExecuteAsync(
                async () =>
                {
                    if (hours > 24)
                        hours = 24; // Cap at 24 hours

                    var window = TimeSpan.FromHours(hours);
                    var counts = await _errorService.GetErrorCountsByKeyAsync(providerId, window);

                    return counts;
                },
                result => Ok(result),
                "GetErrorCountsByKey",
                new { ProviderId = providerId });
        }

        /// <summary>
        /// Manually disable a key due to errors
        /// </summary>
        /// <param name="keyId">ID of the key to disable</param>
        /// <param name="reason">Reason for disabling</param>
        /// <returns>Operation result</returns>
        [HttpPost("keys/{keyId}/disable")]
        public Task<IActionResult> DisableKey(
            int keyId,
            [FromBody] string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return Task.FromResult<IActionResult>(BadRequest(new { error = "Reason is required for disabling a key" }));
            }

            return ExecuteAsync(
                async () =>
                {
                    await _errorService.DisableKeyAsync(keyId, $"Manual disable: {reason}");

                    LogAdminAudit("Disabled", "ProviderKeyCredential", keyId,
                        $"Reason: {reason}");

                    return new
                    {
                        message = "Key disabled successfully",
                        keyId = keyId
                    };
                },
                result => Ok(result),
                "DisableKey",
                new { KeyId = keyId });
        }
    }
}

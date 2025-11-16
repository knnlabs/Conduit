using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Configuration.Events;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing provider error tracking and key status
    /// </summary>
    [ApiController]
    [Route("api/provider-errors")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class ProviderErrorsController : ControllerBase
    {
        private readonly IProviderErrorTrackingService _errorService;
        private readonly IProviderKeyCredentialRepository _keyRepo;
        private readonly IProviderRepository _providerRepo;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<ProviderErrorsController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderErrorsController"/> class.
        /// </summary>
        public ProviderErrorsController(
            IProviderErrorTrackingService errorService,
            IProviderKeyCredentialRepository keyRepo,
            IProviderRepository providerRepo,
            IPublishEndpoint publishEndpoint,
            ILogger<ProviderErrorsController> logger)
        {
            _errorService = errorService ?? throw new ArgumentNullException(nameof(errorService));
            _keyRepo = keyRepo ?? throw new ArgumentNullException(nameof(keyRepo));
            _providerRepo = providerRepo ?? throw new ArgumentNullException(nameof(providerRepo));
            _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get recent errors across all providers
        /// </summary>
        /// <param name="providerId">Optional provider ID filter</param>
        /// <param name="keyId">Optional key ID filter</param>
        /// <param name="limit">Maximum number of errors to return (default: 100)</param>
        /// <returns>List of recent provider errors</returns>
        [HttpGet("recent")]
        public async Task<ActionResult<List<ProviderErrorDto>>> GetRecentErrors(
            [FromQuery] int? providerId = null,
            [FromQuery] int? keyId = null,
            [FromQuery] int limit = 100)
        {
            try
            {
                if (limit > 1000)
                    limit = 1000; // Cap at 1000 for performance

                var errors = await _errorService.GetRecentErrorsAsync(providerId, keyId, limit);
                
                // Get provider and key names for display
                var providers = await _providerRepo.GetAllAsync();
                var providerMap = providers.ToDictionary(p => p.Id, p => p.ProviderName);
                
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

                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get recent errors");
                return StatusCode(500, new { error = "Failed to retrieve error data" });
            }
        }

        /// <summary>
        /// Get error summary for all providers
        /// </summary>
        /// <returns>List of provider error summaries</returns>
        [HttpGet("summary")]
        public async Task<ActionResult<List<ProviderErrorSummaryDto>>> GetErrorSummary()
        {
            try
            {
                var providers = await _providerRepo.GetAllAsync();
                var summaries = new List<ProviderErrorSummaryDto>();

                foreach (var provider in providers)
                {
                    var summary = await _errorService.GetProviderSummaryAsync(provider.Id);
                    if (summary != null)
                    {
                        summaries.Add(new ProviderErrorSummaryDto
                        {
                            ProviderId = provider.Id,
                            ProviderName = provider.ProviderName,
                            TotalErrors = summary.TotalErrors,
                            FatalErrors = summary.FatalErrors,
                            Warnings = summary.Warnings,
                            DisabledKeyIds = summary.DisabledKeyIds,
                            LastError = summary.LastError
                        });
                    }
                }

                return Ok(summaries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get error summary");
                return StatusCode(500, new { error = "Failed to retrieve error summary" });
            }
        }

        /// <summary>
        /// Get detailed error information for a specific key
        /// </summary>
        /// <param name="keyId">ID of the key</param>
        /// <returns>Detailed error information for the key</returns>
        [HttpGet("keys/{keyId}")]
        public async Task<ActionResult<KeyErrorDetailsDto>> GetKeyErrors(int keyId)
        {
            try
            {
                var details = await _errorService.GetKeyErrorDetailsAsync(keyId);
                if (details == null)
                {
                    return NotFound(new { error = $"No error data found for key {keyId}" });
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

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get key errors for key {KeyId}", keyId);
                return StatusCode(500, new { error = "Failed to retrieve key error data" });
            }
        }

        /// <summary>
        /// Clear errors and optionally re-enable a key
        /// </summary>
        /// <param name="keyId">ID of the key</param>
        /// <param name="request">Clear errors request</param>
        /// <returns>Operation result</returns>
        [HttpPost("keys/{keyId}/clear")]
        public async Task<IActionResult> ClearKeyErrors(
            int keyId,
            [FromBody] ClearErrorsRequest request)
        {
            try
            {
                if (!request.ConfirmReenable && request.ReenableKey)
                {
                    return BadRequest(new { error = "Must confirm re-enabling the key" });
                }

                // Clear errors from Redis
                await _errorService.ClearErrorsForKeyAsync(keyId);
                _logger.LogInformation("Cleared errors for key {KeyId}", keyId);

                // Re-enable the key if requested
                if (request.ReenableKey)
                {
                    var key = await _keyRepo.GetByIdAsync(keyId);
                    if (key == null)
                    {
                        return NotFound(new { error = $"Key {keyId} not found" });
                    }

                    if (!key.IsEnabled)
                    {
                        key.IsEnabled = true;
                        await _keyRepo.UpdateAsync(key);

                        // Publish event for UI update
                        await _publishEndpoint.Publish(new ProviderKeyReenabledEvent
                        {
                            KeyId = keyId,
                            ProviderId = key.ProviderId,
                            ReenabledBy = User.Identity?.Name ?? "Admin",
                            Reason = request.Reason ?? "Manual re-enable after error resolution",
                            ReenabledAt = DateTime.UtcNow
                        });

                        _logger.LogInformation(
                            "Re-enabled key {KeyId} for provider {ProviderId} by {User}",
                            keyId, key.ProviderId, User.Identity?.Name);
                    }
                }

                return Ok(new 
                { 
                    message = request.ReenableKey 
                        ? "Errors cleared and key re-enabled successfully" 
                        : "Errors cleared successfully",
                    keyId = keyId,
                    reenabled = request.ReenableKey
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear errors for key {KeyId}", keyId);
                return StatusCode(500, new { error = "Failed to clear key errors" });
            }
        }

        /// <summary>
        /// Get error statistics for dashboard
        /// </summary>
        /// <param name="hours">Time window in hours (default: 24)</param>
        /// <returns>Error statistics</returns>
        [HttpGet("stats")]
        public async Task<ActionResult<ErrorStatisticsDto>> GetErrorStatistics(
            [FromQuery] int hours = 24)
        {
            try
            {
                if (hours > 168) // Cap at 1 week
                    hours = 168;

                var window = TimeSpan.FromHours(hours);
                var stats = await _errorService.GetErrorStatisticsAsync(window);
                
                // Get provider names for the statistics
                var providers = await _providerRepo.GetAllAsync();
                var providerNames = providers.ToDictionary(p => p.Id.ToString(), p => p.ProviderName);

                var dto = new ErrorStatisticsDto
                {
                    TotalErrors = stats.TotalErrors,
                    FatalErrors = stats.FatalErrors,
                    Warnings = stats.Warnings,
                    DisabledKeys = stats.DisabledKeys,
                    ErrorsByType = stats.ErrorsByType,
                    ErrorsByProvider = stats.ErrorsByProvider,
                    TimeWindow = window,
                    GeneratedAt = DateTime.UtcNow
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get error statistics");
                return StatusCode(500, new { error = "Failed to retrieve error statistics" });
            }
        }

        /// <summary>
        /// Get error counts by key for a specific provider
        /// </summary>
        /// <param name="providerId">Provider ID</param>
        /// <param name="hours">Time window in hours (default: 1)</param>
        /// <returns>Dictionary of key ID to error count</returns>
        [HttpGet("providers/{providerId}/key-errors")]
        public async Task<ActionResult<Dictionary<int, int>>> GetErrorCountsByKey(
            int providerId,
            [FromQuery] int hours = 1)
        {
            try
            {
                if (hours > 24)
                    hours = 24; // Cap at 24 hours

                var window = TimeSpan.FromHours(hours);
                var counts = await _errorService.GetErrorCountsByKeyAsync(providerId, window);

                return Ok(counts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get error counts for provider {ProviderId}", providerId);
                return StatusCode(500, new { error = "Failed to retrieve error counts" });
            }
        }

        /// <summary>
        /// Manually disable a key due to errors
        /// </summary>
        /// <param name="keyId">ID of the key to disable</param>
        /// <param name="reason">Reason for disabling</param>
        /// <returns>Operation result</returns>
        [HttpPost("keys/{keyId}/disable")]
        public async Task<IActionResult> DisableKey(
            int keyId,
            [FromBody] string reason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason))
                {
                    return BadRequest(new { error = "Reason is required for disabling a key" });
                }

                await _errorService.DisableKeyAsync(keyId, $"Manual disable: {reason}");
                
                _logger.LogInformation(
                    "Manually disabled key {KeyId} by {User}: {Reason}",
                    keyId, User.Identity?.Name, reason);

                return Ok(new 
                { 
                    message = "Key disabled successfully",
                    keyId = keyId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disable key {KeyId}", keyId);
                return StatusCode(500, new { error = "Failed to disable key" });
            }
        }
    }
}
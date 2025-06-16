using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Contains models for provider health status data.
    /// </summary>
    public static class Models
    {
        /// <summary>
        /// Represents the current status of a provider
        /// </summary>
        public class ProviderStatus
        {
            /// <summary>
            /// The status type
            /// </summary>
            public StatusType Status { get; set; }

            /// <summary>
            /// A descriptive message about the status
            /// </summary>
            public string? StatusMessage { get; set; }

            /// <summary>
            /// The response time in milliseconds
            /// </summary>
            public double ResponseTimeMs { get; set; }

            /// <summary>
            /// When the status was last checked
            /// </summary>
            public DateTime LastCheckedUtc { get; set; }

            /// <summary>
            /// Error category if the provider is offline
            /// </summary>
            public string? ErrorCategory { get; set; }

            /// <summary>
            /// Status types for providers
            /// </summary>
            public enum StatusType
            {
                /// <summary>
                /// Provider is online and responsive
                /// </summary>
                Online,

                /// <summary>
                /// Provider is offline or unresponsive
                /// </summary>
                Offline,

                /// <summary>
                /// Provider status is unknown
                /// </summary>
                Unknown
            }
        }
    }
    /// <summary>
    /// Controller for managing provider health monitoring
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class ProviderHealthController : ControllerBase
    {
        private readonly IAdminProviderHealthService _providerHealthService;
        private readonly ILogger<ProviderHealthController> _logger;

        /// <summary>
        /// Initializes a new instance of the ProviderHealthController
        /// </summary>
        /// <param name="providerHealthService">The provider health service</param>
        /// <param name="logger">The logger</param>
        public ProviderHealthController(
            IAdminProviderHealthService providerHealthService,
            ILogger<ProviderHealthController> logger)
        {
            _providerHealthService = providerHealthService ?? throw new ArgumentNullException(nameof(providerHealthService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets health configurations for all providers
        /// </summary>
        /// <returns>List of all provider health configurations</returns>
        [HttpGet("configurations")]
        [ProducesResponseType(typeof(IEnumerable<ProviderHealthConfigurationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllConfigurations()
        {
            try
            {
                var configurations = await _providerHealthService.GetAllConfigurationsAsync();
                return Ok(configurations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all provider health configurations");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Gets health configuration for a specific provider
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <returns>The provider health configuration</returns>
        [HttpGet("configurations/{providerName}")]
        [ProducesResponseType(typeof(ProviderHealthConfigurationDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetConfigurationByProviderName(string providerName)
        {
            try
            {
                var configuration = await _providerHealthService.GetConfigurationByProviderNameAsync(providerName);

                if (configuration == null)
                {
                    return NotFound("Provider health configuration not found");
                }

                return Ok(configuration);
            }
            catch (Exception ex)
            {
_logger.LogError(ex, "Error getting health configuration for provider '{ProviderName}'".Replace(Environment.NewLine, ""), providerName.Replace(Environment.NewLine, ""));
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Creates a new provider health configuration
        /// </summary>
        /// <param name="configuration">The configuration to create</param>
        /// <returns>The created configuration</returns>
        [HttpPost("configurations")]
        [ProducesResponseType(typeof(ProviderHealthConfigurationDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateConfiguration([FromBody] CreateProviderHealthConfigurationDto configuration)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var createdConfiguration = await _providerHealthService.CreateConfigurationAsync(configuration);
                return CreatedAtAction(
                    nameof(GetConfigurationByProviderName),
                    new { providerName = createdConfiguration.ProviderName },
                    createdConfiguration);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation when creating provider health configuration");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating provider health configuration");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Updates a provider health configuration
        /// </summary>
        /// <param name="configuration">The configuration to update</param>
        /// <returns>No content if successful</returns>
        [HttpPut("configurations")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateConfiguration([FromBody] UpdateProviderHealthConfigurationDto configuration)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var success = await _providerHealthService.UpdateConfigurationAsync(configuration);

                if (!success)
                {
                    return NotFound($"Provider health configuration not found for provider");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating provider health configuration");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Gets the latest health status for all providers
        /// </summary>
        /// <returns>Dictionary of provider names to their latest health status</returns>
        [HttpGet("statuses")]
        [ProducesResponseType(typeof(Dictionary<string, ProviderHealthRecordDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllLatestStatuses()
        {
            try
            {
                var statuses = await _providerHealthService.GetAllLatestStatusesAsync();
                return Ok(statuses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest health statuses for all providers");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Gets the latest health status for a specific provider
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <returns>The latest health status</returns>
        [HttpGet("statuses/{providerName}")]
        [ProducesResponseType(typeof(ProviderHealthRecordDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetLatestStatus(string providerName)
        {
            try
            {
                var status = await _providerHealthService.GetLatestStatusAsync(providerName);

                if (status == null)
                {
                    return NotFound("Health status not found");
                }

                return Ok(status);
            }
            catch (Exception ex)
            {
_logger.LogError(ex, "Error getting latest health status for provider '{ProviderName}'".Replace(Environment.NewLine, ""), providerName.Replace(Environment.NewLine, ""));
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Gets health status history for a provider
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <param name="hours">Number of hours to look back (default: 24)</param>
        /// <param name="limit">Maximum number of records to return (default: 100)</param>
        /// <returns>List of health status records</returns>
        [HttpGet("history/{providerName}")]
        [ProducesResponseType(typeof(IEnumerable<ProviderHealthRecordDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetStatusHistory(
            string providerName,
            [FromQuery] int hours = 24,
            [FromQuery] int limit = 100)
        {
            if (hours <= 0)
            {
                return BadRequest("Hours must be greater than zero");
            }

            if (limit <= 0)
            {
                return BadRequest("Limit must be greater than zero");
            }

            try
            {
                var history = await _providerHealthService.GetStatusHistoryAsync(providerName, hours, limit);
                return Ok(history);
            }
            catch (Exception ex)
            {
_logger.LogError(ex, "Error getting health status history for provider '{ProviderName}'".Replace(Environment.NewLine, ""), providerName.Replace(Environment.NewLine, ""));
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Gets health summary for all providers
        /// </summary>
        /// <param name="hours">Number of hours to include in the summary (default: 24)</param>
        /// <returns>List of provider health summaries</returns>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(IEnumerable<ProviderHealthSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetHealthSummary([FromQuery] int hours = 24)
        {
            if (hours <= 0)
            {
                return BadRequest("Hours must be greater than zero");
            }

            try
            {
                var summary = await _providerHealthService.GetHealthSummaryAsync(hours);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting health summary for providers");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Gets health statistics across all providers
        /// </summary>
        /// <param name="hours">Number of hours to include in the statistics (default: 24)</param>
        /// <returns>Provider health statistics</returns>
        [HttpGet("statistics")]
        [ProducesResponseType(typeof(ProviderHealthStatisticsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetHealthStatistics([FromQuery] int hours = 24)
        {
            if (hours <= 0)
            {
                return BadRequest("Hours must be greater than zero");
            }

            try
            {
                var statistics = await _providerHealthService.GetHealthStatisticsAsync(hours);
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting health statistics for providers");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Triggers an immediate health check for a provider
        /// </summary>
        /// <param name="providerName">The name of the provider to check</param>
        /// <returns>The health check result</returns>
        [HttpPost("check/{providerName}")]
        [ProducesResponseType(typeof(ProviderHealthRecordDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> TriggerHealthCheck(string providerName)
        {
            try
            {
                var result = await _providerHealthService.TriggerHealthCheckAsync(providerName);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
_logger.LogWarning(ex, "Invalid operation when triggering health check for provider '{ProviderName}'".Replace(Environment.NewLine, ""), providerName.Replace(Environment.NewLine, ""));
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
_logger.LogError(ex, "Error triggering health check for provider '{ProviderName}'".Replace(Environment.NewLine, ""), providerName.Replace(Environment.NewLine, ""));
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Purges health records older than the specified time
        /// </summary>
        /// <param name="days">Number of days to keep records for (default: 30)</param>
        /// <returns>Number of records purged</returns>
        [HttpDelete("purge")]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> PurgeOldRecords([FromQuery] int days = 30)
        {
            if (days <= 0)
            {
                return BadRequest("Days must be greater than zero");
            }

            try
            {
                var purgedCount = await _providerHealthService.PurgeOldRecordsAsync(days);
                return Ok(purgedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error purging old health records");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Gets all provider health records
        /// </summary>
        /// <param name="providerName">Optional provider name to filter records</param>
        /// <returns>List of health records</returns>
        [HttpGet("records")]
        [ProducesResponseType(typeof(IEnumerable<ProviderHealthRecordDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetHealthRecords([FromQuery] string? providerName = null)
        {
            try
            {
                IEnumerable<ProviderHealthRecordDto> records;

                if (string.IsNullOrEmpty(providerName))
                {
                    records = await _providerHealthService.GetAllRecordsAsync();
                }
                else
                {
                    var history = await _providerHealthService.GetStatusHistoryAsync(providerName, 24 * 30, 1000); // 30 days, 1000 records max
                    records = history;
                }

                return Ok(records);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving provider health records");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Gets provider status for all providers that have been checked
        /// </summary>
        /// <returns>Dictionary of provider names to their status</returns>
        [HttpGet("status")]
        [ProducesResponseType(typeof(Dictionary<string, Models.ProviderStatus>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetProviderStatus()
        {
            try
            {
                var statuses = await _providerHealthService.GetAllLatestStatusesAsync();

                // Convert to a dictionary of provider name to status model
                var result = statuses.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new Models.ProviderStatus
                    {
                        Status = kvp.Value.IsOnline ? Models.ProviderStatus.StatusType.Online : Models.ProviderStatus.StatusType.Offline,
                        StatusMessage = kvp.Value.StatusMessage ?? (kvp.Value.IsOnline ? "Online" : "Offline"),
                        ResponseTimeMs = kvp.Value.ResponseTimeMs,
                        LastCheckedUtc = kvp.Value.TimestampUtc,
                        ErrorCategory = kvp.Value.ErrorCategory
                    }
                );

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving provider status");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Gets provider status for a specific provider
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <returns>The provider status</returns>
        [HttpGet("status/{providerName}")]
        [ProducesResponseType(typeof(Models.ProviderStatus), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetProviderStatus(string providerName)
        {
            try
            {
                var statusRecord = await _providerHealthService.GetLatestStatusAsync(providerName);

                if (statusRecord == null)
                {
                    return NotFound("Provider status not found");
                }

                var status = new Models.ProviderStatus
                {
                    Status = statusRecord.IsOnline ? Models.ProviderStatus.StatusType.Online : Models.ProviderStatus.StatusType.Offline,
                    StatusMessage = statusRecord.StatusMessage ?? (statusRecord.IsOnline ? "Online" : "Offline"),
                    ResponseTimeMs = statusRecord.ResponseTimeMs,
                    LastCheckedUtc = statusRecord.TimestampUtc,
                    ErrorCategory = statusRecord.ErrorCategory
                };

                return Ok(status);
            }
            catch (Exception ex)
            {
_logger.LogError(ex, "Error retrieving status for provider '{ProviderName}'".Replace(Environment.NewLine, ""), providerName.Replace(Environment.NewLine, ""));
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }
    }
}

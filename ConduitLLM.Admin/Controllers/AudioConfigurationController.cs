using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Audio;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing audio provider configurations, costs, and usage analytics.
    /// </summary>
    [ApiController]
    [Route("api/admin/audio")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class AudioConfigurationController : ControllerBase
    {
        private readonly IAdminAudioProviderService _providerService;
        private readonly IAdminAudioCostService _costService;
        private readonly IAdminAudioUsageService _usageService;
        private readonly ILogger<AudioConfigurationController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioConfigurationController"/> class.
        /// </summary>
        public AudioConfigurationController(
            IAdminAudioProviderService providerService,
            IAdminAudioCostService costService,
            IAdminAudioUsageService usageService,
            ILogger<AudioConfigurationController> logger)
        {
            _providerService = providerService;
            _costService = costService;
            _usageService = usageService;
            _logger = logger;
        }

        #region Provider Configuration Endpoints

        /// <summary>
        /// Gets all audio provider configurations.
        /// </summary>
        /// <response code="200">Returns the list of audio provider configurations</response>
        [HttpGet("providers")]
        [ProducesResponseType(typeof(List<AudioProviderConfigDto>), 200)]
        public async Task<IActionResult> GetProviders()
        {
            var providers = await _providerService.GetAllAsync();
            return Ok(providers);
        }

        /// <summary>
        /// Gets a specific audio provider configuration.
        /// </summary>
        /// <param name="id">The provider configuration ID</param>
        /// <response code="200">Returns the audio provider configuration</response>
        /// <response code="404">If the provider configuration is not found</response>
        [HttpGet("providers/{id}")]
        [ProducesResponseType(typeof(AudioProviderConfigDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetProvider(int id)
        {
            var provider = await _providerService.GetByIdAsync(id);
            if (provider == null)
                return NotFound();

            return Ok(provider);
        }

        /// <summary>
        /// Gets audio provider configurations by provider name.
        /// </summary>
        /// <param name="providerName">The provider name</param>
        /// <response code="200">Returns the list of configurations for the provider</response>
        [HttpGet("providers/by-name/{providerName}")]
        [ProducesResponseType(typeof(List<AudioProviderConfigDto>), 200)]
        public async Task<IActionResult> GetProvidersByName(string providerName)
        {
            var providers = await _providerService.GetByProviderAsync(providerName);
            return Ok(providers);
        }

        /// <summary>
        /// Gets enabled providers for a specific audio operation.
        /// </summary>
        /// <param name="operationType">The operation type (transcription, tts, realtime)</param>
        /// <response code="200">Returns the list of enabled providers</response>
        [HttpGet("providers/enabled/{operationType}")]
        [ProducesResponseType(typeof(List<AudioProviderConfigDto>), 200)]
        public async Task<IActionResult> GetEnabledProviders(string operationType)
        {
            var providers = await _providerService.GetEnabledForOperationAsync(operationType);
            return Ok(providers);
        }

        /// <summary>
        /// Creates a new audio provider configuration.
        /// </summary>
        /// <param name="dto">The provider configuration to create</param>
        /// <response code="201">Returns the created provider configuration</response>
        /// <response code="400">If the configuration is invalid</response>
        [HttpPost("providers")]
        [ProducesResponseType(typeof(AudioProviderConfigDto), 201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> CreateProvider([FromBody] CreateAudioProviderConfigDto dto)
        {
            try
            {
                var provider = await _providerService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetProvider), new { id = provider.Id }, provider);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Updates an audio provider configuration.
        /// </summary>
        /// <param name="id">The provider configuration ID</param>
        /// <param name="dto">The updated configuration</param>
        /// <response code="200">Returns the updated provider configuration</response>
        /// <response code="404">If the provider configuration is not found</response>
        [HttpPut("providers/{id}")]
        [ProducesResponseType(typeof(AudioProviderConfigDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateProvider(int id, [FromBody] UpdateAudioProviderConfigDto dto)
        {
            var provider = await _providerService.UpdateAsync(id, dto);
            if (provider == null)
                return NotFound();

            return Ok(provider);
        }

        /// <summary>
        /// Deletes an audio provider configuration.
        /// </summary>
        /// <param name="id">The provider configuration ID</param>
        /// <response code="204">If the provider configuration was deleted</response>
        /// <response code="404">If the provider configuration is not found</response>
        [HttpDelete("providers/{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteProvider(int id)
        {
            var deleted = await _providerService.DeleteAsync(id);
            if (!deleted)
                return NotFound();

            return NoContent();
        }

        /// <summary>
        /// Tests audio provider connectivity.
        /// </summary>
        /// <param name="id">The provider configuration ID</param>
        /// <param name="operationType">The operation type to test</param>
        /// <response code="200">Returns the test results</response>
        /// <response code="404">If the provider configuration is not found</response>
        [HttpPost("providers/{id}/test")]
        [ProducesResponseType(typeof(AudioProviderTestResult), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> TestProvider(int id, [FromQuery] string operationType = "transcription")
        {
            try
            {
                var result = await _providerService.TestProviderAsync(id, operationType);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        #endregion

        #region Cost Configuration Endpoints

        /// <summary>
        /// Gets all audio cost configurations.
        /// </summary>
        /// <response code="200">Returns the list of audio cost configurations</response>
        [HttpGet("costs")]
        [ProducesResponseType(typeof(List<AudioCostDto>), 200)]
        public async Task<IActionResult> GetCosts()
        {
            var costs = await _costService.GetAllAsync();
            return Ok(costs);
        }

        /// <summary>
        /// Gets a specific audio cost configuration.
        /// </summary>
        /// <param name="id">The cost configuration ID</param>
        /// <response code="200">Returns the audio cost configuration</response>
        /// <response code="404">If the cost configuration is not found</response>
        [HttpGet("costs/{id}")]
        [ProducesResponseType(typeof(AudioCostDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetCost(int id)
        {
            var cost = await _costService.GetByIdAsync(id);
            if (cost == null)
                return NotFound();

            return Ok(cost);
        }

        /// <summary>
        /// Gets audio costs by provider.
        /// </summary>
        /// <param name="provider">The provider name</param>
        /// <response code="200">Returns the list of costs for the provider</response>
        [HttpGet("costs/by-provider/{provider}")]
        [ProducesResponseType(typeof(List<AudioCostDto>), 200)]
        public async Task<IActionResult> GetCostsByProvider(string provider)
        {
            var costs = await _costService.GetByProviderAsync(provider);
            return Ok(costs);
        }

        /// <summary>
        /// Gets the current cost for a specific operation.
        /// </summary>
        /// <param name="provider">The provider name</param>
        /// <param name="operationType">The operation type</param>
        /// <param name="model">The model name (optional)</param>
        /// <response code="200">Returns the current cost</response>
        /// <response code="404">If no cost is found</response>
        [HttpGet("costs/current")]
        [ProducesResponseType(typeof(AudioCostDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetCurrentCost(
            [FromQuery] string provider,
            [FromQuery] string operationType,
            [FromQuery] string? model = null)
        {
            var cost = await _costService.GetCurrentCostAsync(provider, operationType, model);
            if (cost == null)
                return NotFound();

            return Ok(cost);
        }

        /// <summary>
        /// Creates a new audio cost configuration.
        /// </summary>
        /// <param name="dto">The cost configuration to create</param>
        /// <response code="201">Returns the created cost configuration</response>
        /// <response code="400">If the configuration is invalid</response>
        [HttpPost("costs")]
        [ProducesResponseType(typeof(AudioCostDto), 201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> CreateCost([FromBody] CreateAudioCostDto dto)
        {
            try
            {
                var cost = await _costService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetCost), new { id = cost.Id }, cost);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Updates an audio cost configuration.
        /// </summary>
        /// <param name="id">The cost configuration ID</param>
        /// <param name="dto">The updated configuration</param>
        /// <response code="200">Returns the updated cost configuration</response>
        /// <response code="404">If the cost configuration is not found</response>
        [HttpPut("costs/{id}")]
        [ProducesResponseType(typeof(AudioCostDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateCost(int id, [FromBody] UpdateAudioCostDto dto)
        {
            var cost = await _costService.UpdateAsync(id, dto);
            if (cost == null)
                return NotFound();

            return Ok(cost);
        }

        /// <summary>
        /// Deletes an audio cost configuration.
        /// </summary>
        /// <param name="id">The cost configuration ID</param>
        /// <response code="204">If the cost configuration was deleted</response>
        /// <response code="404">If the cost configuration is not found</response>
        [HttpDelete("costs/{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteCost(int id)
        {
            var deleted = await _costService.DeleteAsync(id);
            if (!deleted)
                return NotFound();

            return NoContent();
        }

        #endregion

        #region Usage Analytics Endpoints

        /// <summary>
        /// Gets audio usage logs with pagination and filtering.
        /// </summary>
        /// <param name="query">Query parameters for filtering and pagination</param>
        /// <response code="200">Returns paginated usage logs</response>
        [HttpGet("usage")]
        [ProducesResponseType(typeof(PagedResult<AudioUsageDto>), 200)]
        public async Task<IActionResult> GetUsageLogs([FromQuery] AudioUsageQueryDto query)
        {
            var logs = await _usageService.GetUsageLogsAsync(query);
            return Ok(logs);
        }

        /// <summary>
        /// Gets audio usage summary statistics.
        /// </summary>
        /// <param name="startDate">Start date for the summary</param>
        /// <param name="endDate">End date for the summary</param>
        /// <param name="virtualKey">Filter by virtual key (optional)</param>
        /// <param name="provider">Filter by provider (optional)</param>
        /// <response code="200">Returns usage summary</response>
        [HttpGet("usage/summary")]
        [ProducesResponseType(typeof(AudioUsageSummaryDto), 200)]
        public async Task<IActionResult> GetUsageSummary(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] string? virtualKey = null,
            [FromQuery] string? provider = null)
        {
            var summary = await _usageService.GetUsageSummaryAsync(startDate, endDate, virtualKey, provider);
            return Ok(summary);
        }

        /// <summary>
        /// Gets audio usage by virtual key.
        /// </summary>
        /// <param name="virtualKey">The virtual key</param>
        /// <param name="startDate">Start date (optional)</param>
        /// <param name="endDate">End date (optional)</param>
        /// <response code="200">Returns usage data for the key</response>
        [HttpGet("usage/by-key/{virtualKey}")]
        [ProducesResponseType(typeof(Interfaces.AudioKeyUsageDto), 200)]
        public async Task<IActionResult> GetUsageByKey(
            string virtualKey,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var usage = await _usageService.GetUsageByKeyAsync(virtualKey, startDate, endDate);
            return Ok(usage);
        }

        /// <summary>
        /// Gets audio usage by provider.
        /// </summary>
        /// <param name="provider">The provider name</param>
        /// <param name="startDate">Start date (optional)</param>
        /// <param name="endDate">End date (optional)</param>
        /// <response code="200">Returns usage data for the provider</response>
        [HttpGet("usage/by-provider/{provider}")]
        [ProducesResponseType(typeof(Interfaces.AudioProviderUsageDto), 200)]
        public async Task<IActionResult> GetUsageByProvider(
            string provider,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var usage = await _usageService.GetUsageByProviderAsync(provider, startDate, endDate);
            return Ok(usage);
        }

        #endregion

        #region Real-time Session Management

        /// <summary>
        /// Gets real-time session metrics.
        /// </summary>
        /// <response code="200">Returns session metrics</response>
        [HttpGet("sessions/metrics")]
        [ProducesResponseType(typeof(RealtimeSessionMetricsDto), 200)]
        public async Task<IActionResult> GetSessionMetrics()
        {
            var metrics = await _usageService.GetRealtimeSessionMetricsAsync();
            return Ok(metrics);
        }

        /// <summary>
        /// Gets active real-time sessions.
        /// </summary>
        /// <response code="200">Returns list of active sessions</response>
        [HttpGet("sessions")]
        [ProducesResponseType(typeof(List<RealtimeSessionDto>), 200)]
        public async Task<IActionResult> GetActiveSessions()
        {
            var sessions = await _usageService.GetActiveSessionsAsync();
            return Ok(sessions);
        }

        /// <summary>
        /// Gets details of a specific real-time session.
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <response code="200">Returns session details</response>
        /// <response code="404">If the session is not found</response>
        [HttpGet("sessions/{sessionId}")]
        [ProducesResponseType(typeof(RealtimeSessionDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetSessionDetails(string sessionId)
        {
            var session = await _usageService.GetSessionDetailsAsync(sessionId);
            if (session == null)
                return NotFound();

            return Ok(session);
        }

        /// <summary>
        /// Terminates an active real-time session.
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <response code="204">If the session was terminated</response>
        /// <response code="404">If the session is not found</response>
        [HttpDelete("sessions/{sessionId}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> TerminateSession(string sessionId)
        {
            var terminated = await _usageService.TerminateSessionAsync(sessionId);
            if (!terminated)
                return NotFound();

            _logger.LogInformation("Terminated real-time session {SessionId}", sessionId);
            return NoContent();
        }

        #endregion
    }
}

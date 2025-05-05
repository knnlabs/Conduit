using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.WebUI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Controllers
{
    /// <summary>
    /// Controller for monitoring repository pattern performance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This controller provides endpoints for monitoring the performance and usage of
    /// the repository pattern implementation. It is used during the migration phase
    /// to track metrics and verify that the implementation is working correctly.
    /// </para>
    /// <para>
    /// The endpoints allow retrieving performance metrics, clearing metrics, and getting
    /// configuration information for the repository pattern.
    /// </para>
    /// </remarks>
    [Route("api/repository-monitoring")]
    [ApiController]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class RepositoryMonitoringController : ControllerBase
    {
        private readonly RepositoryPatternConfigurationService _configService;
        private readonly ILogger<RepositoryMonitoringController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RepositoryMonitoringController"/> class.
        /// </summary>
        /// <param name="configService">The repository pattern configuration service.</param>
        /// <param name="logger">The logger.</param>
        public RepositoryMonitoringController(
            RepositoryPatternConfigurationService configService,
            ILogger<RepositoryMonitoringController> logger)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the current repository pattern configuration.
        /// </summary>
        /// <returns>The configuration information.</returns>
        /// <response code="200">Returns the configuration information.</response>
        [HttpGet("config")]
        [ProducesResponseType(typeof(ConfigurationInfo), 200)]
        public IActionResult GetConfiguration()
        {
            var config = new ConfigurationInfo
            {
                IsEnabled = _configService.IsEnabled,
                DetailedLoggingEnabled = _configService.DetailedLoggingEnabled,
                TrackPerformanceMetrics = _configService.TrackPerformanceMetrics,
                ParallelVerificationEnabled = _configService.ParallelVerificationEnabled
            };
            
            return Ok(config);
        }

        /// <summary>
        /// Gets the performance metrics for repository operations.
        /// </summary>
        /// <returns>The performance metrics.</returns>
        /// <response code="200">Returns the performance metrics.</response>
        /// <response code="400">If performance tracking is disabled.</response>
        [HttpGet("metrics")]
        [ProducesResponseType(typeof(Dictionary<string, OperationMetrics>), 200)]
        [ProducesResponseType(400)]
        public IActionResult GetMetrics()
        {
            if (!_configService.TrackPerformanceMetrics)
            {
                return BadRequest("Performance metrics tracking is disabled.");
            }
            
            var metrics = _configService.GetPerformanceMetrics();
            return Ok(metrics);
        }

        /// <summary>
        /// Clears all performance metrics.
        /// </summary>
        /// <returns>A status message.</returns>
        /// <response code="200">If the metrics were cleared successfully.</response>
        /// <response code="400">If performance tracking is disabled.</response>
        [HttpPost("metrics/clear")]
        [ProducesResponseType(typeof(StatusResponse), 200)]
        [ProducesResponseType(400)]
        public IActionResult ClearMetrics()
        {
            if (!_configService.TrackPerformanceMetrics)
            {
                return BadRequest("Performance metrics tracking is disabled.");
            }
            
            _configService.ClearMetrics();
            return Ok(new StatusResponse { Success = true, Message = "Metrics cleared successfully." });
        }
    }

    /// <summary>
    /// Represents the repository pattern configuration information.
    /// </summary>
    public class ConfigurationInfo
    {
        /// <summary>
        /// Gets or sets whether the repository pattern is enabled.
        /// </summary>
        public bool IsEnabled { get; set; }
        
        /// <summary>
        /// Gets or sets whether detailed logging is enabled.
        /// </summary>
        public bool DetailedLoggingEnabled { get; set; }
        
        /// <summary>
        /// Gets or sets whether performance metrics are being tracked.
        /// </summary>
        public bool TrackPerformanceMetrics { get; set; }
        
        /// <summary>
        /// Gets or sets whether parallel verification is enabled.
        /// </summary>
        public bool ParallelVerificationEnabled { get; set; }
    }

    /// <summary>
    /// Represents a status response.
    /// </summary>
    public class StatusResponse
    {
        /// <summary>
        /// Gets or sets whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Gets or sets a message describing the result.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
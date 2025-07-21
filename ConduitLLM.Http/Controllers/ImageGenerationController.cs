using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Controller for image generation optimization endpoints.
    /// </summary>
    [Route("api/v1/images")]
    [ApiController]
    [Authorize]
    public class ImageGenerationController : ControllerBase
    {
        private readonly IImageGenerationMetricsService _metricsService;
        private readonly IModelProviderMappingService _modelMappingService;
        private readonly ILogger<ImageGenerationController> _logger;
        
        public ImageGenerationController(
            IImageGenerationMetricsService metricsService,
            IModelProviderMappingService modelMappingService,
            ILogger<ImageGenerationController> logger)
        {
            _metricsService = metricsService;
            _modelMappingService = modelMappingService;
            _logger = logger;
        }
        
        /// <summary>
        /// Gets the optimal provider for image generation based on current performance metrics.
        /// </summary>
        /// <param name="imageCount">Number of images to generate (default 1).</param>
        /// <param name="maxWaitTime">Maximum acceptable wait time in seconds (optional).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The optimal provider and model for the request.</returns>
        [HttpGet("optimal-provider")]
        [ProducesResponseType(typeof(OptimalProviderResponse), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetOptimalProvider(
            [FromQuery] int imageCount = 1,
            [FromQuery] double? maxWaitTime = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Get all model mappings
                var allMappings = await _modelMappingService.GetAllMappingsAsync();
                var imageProviders = allMappings
                    .Where(m => m.SupportsImageGeneration)
                    .ToList();
                
                if (!imageProviders.Any())
                {
                    return NotFound(new { error = "No image generation providers available" });
                }
                
                var availableProviders = imageProviders
                    .Select(p => (p.ProviderName, p.ProviderModelId))
                    .ToList();
                
                // Get optimal provider based on metrics
                var optimal = await _metricsService.SelectOptimalProviderAsync(
                    availableProviders,
                    imageCount,
                    maxWaitTime,
                    cancellationToken);
                
                if (!optimal.HasValue)
                {
                    return NotFound(new { error = "No provider meets the specified criteria" });
                }
                
                var (provider, model) = optimal.Value;
                var stats = await _metricsService.GetProviderStatsAsync(
                    provider, 
                    model, 
                    60, 
                    cancellationToken);
                
                return Ok(new OptimalProviderResponse
                {
                    Provider = provider,
                    Model = model,
                    EstimatedWaitTimeSeconds = stats?.EstimatedWaitTimeSeconds ?? 0,
                    AverageGenerationTimeMs = stats?.AvgGenerationTimeMs ?? 0,
                    SuccessRate = stats?.SuccessRate ?? 1.0,
                    HealthScore = stats?.HealthScore ?? 1.0,
                    CurrentQueueDepth = stats?.CurrentQueueDepth ?? 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting optimal provider for image generation");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
        
        /// <summary>
        /// Gets performance statistics for all image generation providers.
        /// </summary>
        /// <param name="windowMinutes">Time window in minutes for statistics (default 60).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of provider performance statistics.</returns>
        [HttpGet("provider-stats")]
        [ProducesResponseType(typeof(IEnumerable<ImageGenerationProviderStats>), 200)]
        public async Task<IActionResult> GetProviderStats(
            [FromQuery] int windowMinutes = 60,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var stats = await _metricsService.GetAllProviderStatsAsync(
                    windowMinutes, 
                    cancellationToken);
                
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider statistics");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
        
        /// <summary>
        /// Gets performance statistics for a specific provider.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <param name="model">Model name.</param>
        /// <param name="windowMinutes">Time window in minutes for statistics (default 60).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Provider performance statistics.</returns>
        [HttpGet("provider-stats/{provider}/{model}")]
        [ProducesResponseType(typeof(ImageGenerationProviderStats), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetProviderStats(
            string provider,
            string model,
            [FromQuery] int windowMinutes = 60,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var stats = await _metricsService.GetProviderStatsAsync(
                    provider, 
                    model, 
                    windowMinutes, 
                    cancellationToken);
                
                if (stats == null)
                {
                    return NotFound(new { error = $"No statistics found for {provider}/{model}" });
                }
                
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider statistics for {Provider}/{Model}", 
                    provider, model);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
    
    /// <summary>
    /// Response for optimal provider selection.
    /// </summary>
    public class OptimalProviderResponse
    {
        /// <summary>
        /// Selected provider name.
        /// </summary>
        public string Provider { get; set; } = string.Empty;
        
        /// <summary>
        /// Selected model name.
        /// </summary>
        public string Model { get; set; } = string.Empty;
        
        /// <summary>
        /// Estimated wait time in seconds.
        /// </summary>
        public double EstimatedWaitTimeSeconds { get; set; }
        
        /// <summary>
        /// Average generation time in milliseconds.
        /// </summary>
        public double AverageGenerationTimeMs { get; set; }
        
        /// <summary>
        /// Success rate (0.0 to 1.0).
        /// </summary>
        public double SuccessRate { get; set; }
        
        /// <summary>
        /// Provider health score (0.0 to 1.0).
        /// </summary>
        public double HealthScore { get; set; }
        
        /// <summary>
        /// Current queue depth.
        /// </summary>
        public int CurrentQueueDepth { get; set; }
    }
}
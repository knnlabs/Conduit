using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Core.Models.Routing;
using ConduitLLM.WebUI.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Controllers
{
    /// <summary>
    /// Controller for managing router configurations and deployments
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class RouterController : ControllerBase
    {
        private readonly IRouterService _routerService;
        private readonly ILogger<RouterController> _logger;

        /// <summary>
        /// Creates a new instance of the RouterController
        /// </summary>
        public RouterController(IRouterService routerService, ILogger<RouterController> logger)
        {
            _routerService = routerService ?? throw new ArgumentNullException(nameof(routerService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the current router configuration
        /// </summary>
        [HttpGet("config")]
        public async Task<ActionResult<RouterConfig>> GetRouterConfig()
        {
            try
            {
                var config = await _routerService.GetRouterConfigAsync();
                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving router configuration");
                return StatusCode(500, "Error retrieving router configuration");
            }
        }

        /// <summary>
        /// Updates the router configuration
        /// </summary>
        [HttpPut("config")]
        [Authorize(Policy = "MasterKeyPolicy")]
        public async Task<ActionResult> UpdateRouterConfig([FromBody] RouterConfig config)
        {
            try
            {
                if (config == null)
                {
                    return BadRequest("Router configuration cannot be null");
                }

                bool success = await _routerService.UpdateRouterConfigAsync(config);
                if (success)
                {
                    return Ok("Router configuration updated successfully");
                }
                else
                {
                    return StatusCode(500, "Failed to update router configuration");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating router configuration");
                return StatusCode(500, "Error updating router configuration");
            }
        }

        /// <summary>
        /// Gets all model deployments
        /// </summary>
        [HttpGet("deployments")]
        public async Task<ActionResult<List<ModelDeployment>>> GetModelDeployments()
        {
            try
            {
                var deployments = await _routerService.GetModelDeploymentsAsync();
                return Ok(deployments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving model deployments");
                return StatusCode(500, "Error retrieving model deployments");
            }
        }

        /// <summary>
        /// Gets a specific model deployment
        /// </summary>
        [HttpGet("deployments/{deploymentName}")]
        public async Task<ActionResult<ModelDeployment>> GetModelDeployment(string deploymentName)
        {
            try
            {
                var deployment = await _routerService.GetModelDeploymentAsync(deploymentName);
                if (deployment == null)
                {
                    return NotFound($"Deployment '{deploymentName}' not found");
                }
                return Ok(deployment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving model deployment {DeploymentName}", deploymentName);
                return StatusCode(500, $"Error retrieving model deployment '{deploymentName}'");
            }
        }

        /// <summary>
        /// Creates or updates a model deployment
        /// </summary>
        [HttpPost("deployments")]
        [Authorize(Policy = "MasterKeyPolicy")]
        public async Task<ActionResult> CreateOrUpdateModelDeployment([FromBody] ModelDeployment deployment)
        {
            try
            {
                if (deployment == null)
                {
                    return BadRequest("Model deployment cannot be null");
                }

                if (string.IsNullOrWhiteSpace(deployment.DeploymentName))
                {
                    return BadRequest("Deployment name cannot be empty");
                }

                if (string.IsNullOrWhiteSpace(deployment.ModelAlias))
                {
                    return BadRequest("Model alias cannot be empty");
                }

                bool success = await _routerService.SaveModelDeploymentAsync(deployment);
                if (success)
                {
                    return Ok("Model deployment saved successfully");
                }
                else
                {
                    return StatusCode(500, "Failed to save model deployment");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving model deployment");
                return StatusCode(500, "Error saving model deployment");
            }
        }

        /// <summary>
        /// Deletes a model deployment
        /// </summary>
        [HttpDelete("deployments/{deploymentName}")]
        [Authorize(Policy = "MasterKeyPolicy")]
        public async Task<ActionResult> DeleteModelDeployment(string deploymentName)
        {
            try
            {
                bool success = await _routerService.DeleteModelDeploymentAsync(deploymentName);
                if (success)
                {
                    return Ok($"Deployment '{deploymentName}' deleted successfully");
                }
                else
                {
                    return NotFound($"Deployment '{deploymentName}' not found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model deployment {DeploymentName}", deploymentName);
                return StatusCode(500, $"Error deleting model deployment '{deploymentName}'");
            }
        }

        /// <summary>
        /// Gets all fallback configurations
        /// </summary>
        [HttpGet("fallbacks")]
        public async Task<ActionResult<Dictionary<string, List<string>>>> GetFallbackConfigurations()
        {
            try
            {
                var fallbacks = await _routerService.GetFallbackConfigurationsAsync();
                return Ok(fallbacks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving fallback configurations");
                return StatusCode(500, "Error retrieving fallback configurations");
            }
        }

        /// <summary>
        /// Sets a fallback configuration
        /// </summary>
        [HttpPost("fallbacks/{primaryModel}")]
        [Authorize(Policy = "MasterKeyPolicy")]
        public async Task<ActionResult> SetFallbackConfiguration(string primaryModel, [FromBody] List<string> fallbackModels)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(primaryModel))
                {
                    return BadRequest("Primary model cannot be empty");
                }

                if (fallbackModels == null || fallbackModels.Count == 0)
                {
                    return BadRequest("Fallback models cannot be empty");
                }

                bool success = await _routerService.SetFallbackConfigurationAsync(primaryModel, fallbackModels);
                if (success)
                {
                    return Ok($"Fallback configuration for model '{primaryModel}' saved successfully");
                }
                else
                {
                    return StatusCode(500, $"Failed to save fallback configuration for model '{primaryModel}'");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting fallback configuration for model {PrimaryModel}", primaryModel);
                return StatusCode(500, $"Error setting fallback configuration for model '{primaryModel}'");
            }
        }

        /// <summary>
        /// Removes a fallback configuration
        /// </summary>
        [HttpDelete("fallbacks/{primaryModel}")]
        [Authorize(Policy = "MasterKeyPolicy")]
        public async Task<ActionResult> RemoveFallbackConfiguration(string primaryModel)
        {
            try
            {
                bool success = await _routerService.RemoveFallbackConfigurationAsync(primaryModel);
                if (success)
                {
                    return Ok($"Fallback configuration for model '{primaryModel}' removed successfully");
                }
                else
                {
                    return NotFound($"Fallback configuration for model '{primaryModel}' not found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing fallback configuration for model {PrimaryModel}", primaryModel);
                return StatusCode(500, $"Error removing fallback configuration for model '{primaryModel}'");
            }
        }
    }
}

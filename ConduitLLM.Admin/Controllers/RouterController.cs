using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Core.Models.Routing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers;

/// <summary>
/// Controller for managing router configurations and deployments
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "MasterKeyPolicy")]
public class RouterController : ControllerBase
{
    private readonly IAdminRouterService _routerService;
    private readonly ILogger<RouterController> _logger;
    
    /// <summary>
    /// Initializes a new instance of the RouterController
    /// </summary>
    /// <param name="routerService">The router service</param>
    /// <param name="logger">The logger</param>
    public RouterController(
        IAdminRouterService routerService,
        ILogger<RouterController> logger)
    {
        _routerService = routerService ?? throw new ArgumentNullException(nameof(routerService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Gets the current router configuration
    /// </summary>
    /// <returns>The router configuration</returns>
    [HttpGet("config")]
    [ProducesResponseType(typeof(RouterConfig), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRouterConfig()
    {
        try
        {
            var config = await _routerService.GetRouterConfigAsync();
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving router configuration");
            return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving router configuration");
        }
    }
    
    /// <summary>
    /// Updates the router configuration
    /// </summary>
    /// <param name="config">The new router configuration</param>
    /// <returns>Success response</returns>
    [HttpPut("config")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateRouterConfig([FromBody] RouterConfig config)
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
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to update router configuration");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating router configuration");
            return StatusCode(StatusCodes.Status500InternalServerError, "Error updating router configuration");
        }
    }
    
    /// <summary>
    /// Gets all model deployments
    /// </summary>
    /// <returns>List of all model deployments</returns>
    [HttpGet("deployments")]
    [ProducesResponseType(typeof(List<ModelDeployment>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetModelDeployments()
    {
        try
        {
            var deployments = await _routerService.GetModelDeploymentsAsync();
            return Ok(deployments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving model deployments");
            return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving model deployments");
        }
    }
    
    /// <summary>
    /// Gets a specific model deployment
    /// </summary>
    /// <param name="deploymentName">The name of the deployment</param>
    /// <returns>The model deployment</returns>
    [HttpGet("deployments/{deploymentName}")]
    [ProducesResponseType(typeof(ModelDeployment), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetModelDeployment(string deploymentName)
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
            return StatusCode(StatusCodes.Status500InternalServerError, $"Error retrieving model deployment '{deploymentName}'");
        }
    }
    
    /// <summary>
    /// Creates or updates a model deployment
    /// </summary>
    /// <param name="deployment">The deployment to save</param>
    /// <returns>Success response</returns>
    [HttpPost("deployments")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateOrUpdateModelDeployment([FromBody] ModelDeployment deployment)
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
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to save model deployment");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving model deployment");
            return StatusCode(StatusCodes.Status500InternalServerError, "Error saving model deployment");
        }
    }
    
    /// <summary>
    /// Deletes a model deployment
    /// </summary>
    /// <param name="deploymentName">The name of the deployment to delete</param>
    /// <returns>Success response</returns>
    [HttpDelete("deployments/{deploymentName}")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteModelDeployment(string deploymentName)
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
            return StatusCode(StatusCodes.Status500InternalServerError, $"Error deleting model deployment '{deploymentName}'");
        }
    }
    
    /// <summary>
    /// Gets all fallback configurations
    /// </summary>
    /// <returns>Dictionary mapping primary models to their fallback models</returns>
    [HttpGet("fallbacks")]
    [ProducesResponseType(typeof(Dictionary<string, List<string>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetFallbackConfigurations()
    {
        try
        {
            var fallbacks = await _routerService.GetFallbackConfigurationsAsync();
            return Ok(fallbacks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fallback configurations");
            return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving fallback configurations");
        }
    }
    
    /// <summary>
    /// Sets a fallback configuration
    /// </summary>
    /// <param name="primaryModel">The primary model</param>
    /// <param name="fallbackModels">The fallback models</param>
    /// <returns>Success response</returns>
    [HttpPost("fallbacks/{primaryModel}")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SetFallbackConfiguration(string primaryModel, [FromBody] List<string> fallbackModels)
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
                return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to save fallback configuration for model '{primaryModel}'");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting fallback configuration for model {PrimaryModel}", primaryModel);
            return StatusCode(StatusCodes.Status500InternalServerError, $"Error setting fallback configuration for model '{primaryModel}'");
        }
    }
    
    /// <summary>
    /// Removes a fallback configuration
    /// </summary>
    /// <param name="primaryModel">The primary model</param>
    /// <returns>Success response</returns>
    [HttpDelete("fallbacks/{primaryModel}")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemoveFallbackConfiguration(string primaryModel)
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
            return StatusCode(StatusCodes.Status500InternalServerError, $"Error removing fallback configuration for model '{primaryModel}'");
        }
    }
}
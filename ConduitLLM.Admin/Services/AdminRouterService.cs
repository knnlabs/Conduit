using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Core.Models.Routing;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Admin.Services;

/// <summary>
/// Service for managing router configuration through the Admin API
/// </summary>
public class AdminRouterService : IAdminRouterService
{
    private readonly IRouterConfigRepository _routerConfigRepository;
    private readonly IModelDeploymentRepository _modelDeploymentRepository;
    private readonly IFallbackConfigurationRepository _fallbackConfigRepository;
    private readonly ILogger<AdminRouterService> _logger;

    /// <summary>
    /// Initializes a new instance of the AdminRouterService class
    /// </summary>
    /// <param name="routerConfigRepository">The router configuration repository</param>
    /// <param name="modelDeploymentRepository">The model deployment repository</param>
    /// <param name="fallbackConfigRepository">The fallback configuration repository</param>
    /// <param name="logger">The logger</param>
    public AdminRouterService(
        IRouterConfigRepository routerConfigRepository,
        IModelDeploymentRepository modelDeploymentRepository,
        IFallbackConfigurationRepository fallbackConfigRepository,
        ILogger<AdminRouterService> logger)
    {
        _routerConfigRepository = routerConfigRepository ?? throw new ArgumentNullException(nameof(routerConfigRepository));
        _modelDeploymentRepository = modelDeploymentRepository ?? throw new ArgumentNullException(nameof(modelDeploymentRepository));
        _fallbackConfigRepository = fallbackConfigRepository ?? throw new ArgumentNullException(nameof(fallbackConfigRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<RouterConfig> GetRouterConfigAsync()
    {
        _logger.LogInformation("Getting router configuration");
        // For now, we'll just return an empty config since the implementation is incomplete
        return Task.FromResult(new RouterConfig());
    }

    /// <inheritdoc />
    public Task<bool> UpdateRouterConfigAsync(RouterConfig config)
    {
        try
        {
            _logger.LogInformation("Updating router configuration");

            if (config == null)
            {
                _logger.LogWarning("Router configuration is null");
                return Task.FromResult(false);
            }

            // Implementation would normally call _routerConfigRepository.SaveConfigAsync(config)
            // but we'll leave this as a stub for now
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating router configuration");
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public Task<List<ModelDeployment>> GetModelDeploymentsAsync()
    {
        _logger.LogInformation("Getting all model deployments");
        // Return empty list for now
        return Task.FromResult(new List<ModelDeployment>());
    }

    /// <inheritdoc />
    public Task<ModelDeployment?> GetModelDeploymentAsync(string deploymentName)
    {
_logger.LogInformation("Getting model deployment: {DeploymentName}", deploymentName.Replace(Environment.NewLine, ""));
        // Return null for now
        return Task.FromResult<ModelDeployment?>(null);
    }

    /// <inheritdoc />
    public Task<bool> SaveModelDeploymentAsync(ModelDeployment deployment)
    {
        try
        {
_logger.LogInformation("Saving model deployment: {DeploymentName}", deployment.DeploymentName.Replace(Environment.NewLine, ""));

            if (deployment == null || string.IsNullOrWhiteSpace(deployment.DeploymentName))
            {
                _logger.LogWarning("Invalid model deployment");
                return Task.FromResult(false);
            }

            // Implementation would normally call _modelDeploymentRepository.SaveAsync(deployment)
            // but we'll leave this as a stub for now
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
_logger.LogError(ex, "Error saving model deployment: {DeploymentName}".Replace(Environment.NewLine, ""), deployment?.DeploymentName?.Replace(Environment.NewLine, "") ?? "");
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public Task<bool> DeleteModelDeploymentAsync(string deploymentName)
    {
        try
        {
_logger.LogInformation("Deleting model deployment: {DeploymentName}", deploymentName.Replace(Environment.NewLine, ""));

            // This would normally check if the deployment exists and then delete it
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
_logger.LogError(ex, "Error deleting model deployment: {DeploymentName}".Replace(Environment.NewLine, ""), deploymentName.Replace(Environment.NewLine, ""));
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, List<string>>> GetFallbackConfigurationsAsync()
    {
        _logger.LogInformation("Getting all fallback configurations");

        var fallbackConfigs = await _fallbackConfigRepository.GetAllAsync();
        var result = new Dictionary<string, List<string>>();

        foreach (var config in fallbackConfigs)
        {
            // Convert entity to model
            var fallbackModelIds = await _fallbackConfigRepository.GetMappingsAsync(config.Id);
            var modelIds = fallbackModelIds.Select(m => m.ModelDeploymentId.ToString()).ToList();
            result[config.PrimaryModelDeploymentId.ToString()] = modelIds;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<bool> SetFallbackConfigurationAsync(string primaryModel, List<string> fallbackModels)
    {
        try
        {
_logger.LogInformation("Setting fallback configuration for model: {PrimaryModel}", primaryModel.Replace(Environment.NewLine, ""));

            if (string.IsNullOrWhiteSpace(primaryModel) || fallbackModels == null || fallbackModels.Count == 0)
            {
                _logger.LogWarning("Invalid fallback configuration");
                return false;
            }

            // Create the fallback configuration model
            var fallbackConfig = new FallbackConfiguration
            {
                PrimaryModelDeploymentId = primaryModel,
                FallbackModelDeploymentIds = fallbackModels
            };

            // Save using extension method
            await _fallbackConfigRepository.SaveAsync(fallbackConfig);

            return true;
        }
        catch (Exception ex)
        {
_logger.LogError(ex, "Error setting fallback configuration for model: {PrimaryModel}".Replace(Environment.NewLine, ""), primaryModel.Replace(Environment.NewLine, ""));
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveFallbackConfigurationAsync(string primaryModel)
    {
        try
        {
_logger.LogInformation("Removing fallback configuration for model: {PrimaryModel}", primaryModel.Replace(Environment.NewLine, ""));

            // Find the configuration for this primary model
            var allConfigs = await _fallbackConfigRepository.GetAllAsync();
            var config = allConfigs.FirstOrDefault(c => c.PrimaryModelDeploymentId.ToString() == primaryModel);

            if (config != null)
            {
                await _fallbackConfigRepository.DeleteAsync(config.Id);
            }

            return true;
        }
        catch (Exception ex)
        {
_logger.LogError(ex, "Error removing fallback configuration for model: {PrimaryModel}".Replace(Environment.NewLine, ""), primaryModel.Replace(Environment.NewLine, ""));
            return false;
        }
    }
}

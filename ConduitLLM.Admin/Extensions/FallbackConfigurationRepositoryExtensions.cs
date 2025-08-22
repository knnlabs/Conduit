using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models.Routing;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Admin.Extensions
{
    /// <summary>
    /// Extension methods for the fallback configuration repository
    /// </summary>
    public static class FallbackConfigurationRepositoryExtensions
    {
        /// <summary>
        /// Converts a FallbackConfigurationEntity to a FallbackConfiguration model
        /// </summary>
        /// <param name="entity">The entity to convert</param>
        /// <param name="fallbackMappings">The fallback mappings for this configuration</param>
        /// <returns>The converted model</returns>
        public static FallbackConfiguration ToModel(
            this FallbackConfigurationEntity entity,
            IEnumerable<FallbackModelMappingEntity> fallbackMappings)
        {
            var model = new FallbackConfiguration
            {
                Id = entity.Id,
                PrimaryModelDeploymentId = entity.PrimaryModelDeploymentId.ToString(),
                FallbackModelDeploymentIds = fallbackMappings
                    .OrderBy(m => m.Order)
                    .Select(m => m.ModelDeploymentId.ToString())
                    .ToList()
            };

            return model;
        }

        /// <summary>
        /// Saves a FallbackConfiguration model to the repository
        /// </summary>
        /// <param name="repository">The repository</param>
        /// <param name="config">The configuration to save</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task SaveAsync(
            this IFallbackConfigurationRepository repository,
            FallbackConfiguration config)
        {
            // Parse the GUID from the string
            if (!Guid.TryParse(config.PrimaryModelDeploymentId, out var primaryModelGuid))
            {
                throw new ArgumentException($"Invalid primary model ID: {config.PrimaryModelDeploymentId}");
            }

            // Check if a configuration for this primary model already exists
            var allConfigs = await repository.GetAllAsync();
            var existingConfig = allConfigs.FirstOrDefault(c => c.PrimaryModelDeploymentId == primaryModelGuid);

            if (existingConfig == null)
            {
                // Create new configuration
                var entity = new FallbackConfigurationEntity
                {
                    PrimaryModelDeploymentId = primaryModelGuid,
                    IsActive = true,
                    Name = $"Fallback for {config.PrimaryModelDeploymentId}"
                };

                await repository.CreateAsync(entity);
            }
            else
            {
                // Update existing configuration
                await repository.UpdateAsync(existingConfig);
            }

            // Note: This is a simplified implementation - in a real application,
            // you would also need to handle the fallback mappings
        }
    }
}

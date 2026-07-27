using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for managing virtual keys through the Admin API - Discovery functionality
    /// </summary>
    public partial class AdminVirtualKeyService
    {
        /// <inheritdoc />
        public async Task<DiscoveryModelsResponse?> PreviewDiscoveryAsync(int id, string? capability = null)
        {
            _logger.LogInformation("Previewing discovery for virtual key {KeyId} with capability filter: {Capability}", 
                id, capability ?? "none");

            // Get the virtual key
            var virtualKey = await _virtualKeyRepository.GetByIdAsync(id);
            if (virtualKey == null)
            {
                _logger.LogWarning("Virtual key with ID {KeyId} not found", id);
                return null;
            }

            using var context = await _dbContextFactory.CreateDbContextAsync();

            var models = await DiscoveryModelProjector.ProjectAsync(
                context,
                capability,
                _discoveryOptions.ExposePricing,
                _logger);

            return new DiscoveryModelsResponse(models, models.Count);
        }

    }
}

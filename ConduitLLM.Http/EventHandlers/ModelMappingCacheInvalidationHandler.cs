using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Events;
using ConduitLLM.Http.Interfaces;
using ConduitLLM.Http.Services;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Handles ModelMappingChanged events to refresh in-memory settings
    /// Critical for maintaining runtime configuration consistency
    /// </summary>
    public class ModelMappingCacheInvalidationHandler : IConsumer<ModelMappingChanged>
    {
        private readonly ISettingsRefreshService _settingsRefreshService;
        private readonly ILogger<ModelMappingCacheInvalidationHandler> _logger;

        public ModelMappingCacheInvalidationHandler(
            ISettingsRefreshService settingsRefreshService,
            ILogger<ModelMappingCacheInvalidationHandler> logger)
        {
            _settingsRefreshService = settingsRefreshService ?? throw new ArgumentNullException(nameof(settingsRefreshService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles ModelMappingChanged events by refreshing model mappings from the database
        /// </summary>
        public async Task Consume(ConsumeContext<ModelMappingChanged> context)
        {
            var @event = context.Message;
            
            try
            {
                _logger.LogInformation(
                    "Processing ModelMappingChanged event: {ModelAlias} ({ChangeType})",
                    @event.ModelAlias,
                    @event.ChangeType);

                // Refresh all model mappings to ensure consistency
                await _settingsRefreshService.RefreshModelMappingsAsync();
                
                _logger.LogInformation(
                    "Successfully refreshed model mappings after {ChangeType} of {ModelAlias}",
                    @event.ChangeType,
                    @event.ModelAlias);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to refresh model mappings after {ChangeType} of {ModelAlias}", 
                    @event.ChangeType,
                    @event.ModelAlias);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }
    }
}
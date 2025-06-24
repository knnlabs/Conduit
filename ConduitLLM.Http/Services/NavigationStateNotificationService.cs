using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Http.Hubs;
using System;
using System.Threading.Tasks;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service for sending real-time navigation state updates through SignalR
    /// </summary>
    public interface INavigationStateNotificationService
    {
        /// <summary>
        /// Notifies all connected clients of a model mapping change
        /// </summary>
        Task NotifyModelMappingChangedAsync(int mappingId, string modelAlias, string changeType);
        
        /// <summary>
        /// Notifies all connected clients of a provider health change
        /// </summary>
        Task NotifyProviderHealthChangedAsync(string providerName, bool isHealthy, string status);
        
        /// <summary>
        /// Notifies all connected clients of model capabilities discovery
        /// </summary>
        Task NotifyModelCapabilitiesDiscoveredAsync(string providerName, int modelCount, int embeddingCount = 0, int visionCount = 0, int imageGenCount = 0, int videoGenCount = 0);
        
        /// <summary>
        /// Notifies specific model subscribers of availability change
        /// </summary>
        Task NotifyModelAvailabilityChangedAsync(string modelId, bool isAvailable);
    }

    /// <summary>
    /// Implementation of navigation state notification service using SignalR
    /// </summary>
    public class NavigationStateNotificationService : INavigationStateNotificationService
    {
        private readonly IHubContext<NavigationStateHub> _hubContext;
        private readonly ILogger<NavigationStateNotificationService> _logger;

        /// <summary>
        /// Initializes a new instance of the NavigationStateNotificationService
        /// </summary>
        /// <param name="hubContext">SignalR hub context for NavigationStateHub</param>
        /// <param name="logger">Logger instance</param>
        public NavigationStateNotificationService(
            IHubContext<NavigationStateHub> hubContext,
            ILogger<NavigationStateNotificationService> logger)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task NotifyModelMappingChangedAsync(int mappingId, string modelAlias, string changeType)
        {
            try
            {
                var notification = new
                {
                    type = "ModelMappingChanged",
                    data = new
                    {
                        mappingId,
                        modelAlias,
                        changeType,
                        timestamp = DateTime.UtcNow
                    }
                };

                await _hubContext.Clients.Group("navigation-updates").SendAsync("NavigationStateUpdate", notification);
                
                // Also notify model-specific subscribers
                await _hubContext.Clients.Group($"model-{modelAlias}").SendAsync("ModelUpdate", notification);
                
                _logger.LogDebug("Sent model mapping change notification for {ModelAlias} ({ChangeType})", modelAlias, changeType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send model mapping change notification for {ModelAlias}", modelAlias);
            }
        }

        /// <inheritdoc />
        public async Task NotifyProviderHealthChangedAsync(string providerName, bool isHealthy, string status)
        {
            try
            {
                var notification = new
                {
                    type = "ProviderHealthChanged",
                    data = new
                    {
                        providerName,
                        isHealthy,
                        status,
                        timestamp = DateTime.UtcNow
                    }
                };

                await _hubContext.Clients.Group("navigation-updates").SendAsync("NavigationStateUpdate", notification);
                
                _logger.LogDebug("Sent provider health change notification for {ProviderName} (Healthy: {IsHealthy})", providerName, isHealthy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send provider health change notification for {ProviderName}", providerName);
            }
        }

        /// <inheritdoc />
        public async Task NotifyModelCapabilitiesDiscoveredAsync(string providerName, int modelCount, int embeddingCount = 0, int visionCount = 0, int imageGenCount = 0, int videoGenCount = 0)
        {
            try
            {
                var notification = new
                {
                    type = "ModelCapabilitiesDiscovered",
                    data = new
                    {
                        providerName,
                        modelCount,
                        embeddingCount,
                        visionCount,
                        imageGenCount,
                        videoGenCount,
                        timestamp = DateTime.UtcNow
                    }
                };

                await _hubContext.Clients.Group("navigation-updates").SendAsync("NavigationStateUpdate", notification);
                
                _logger.LogDebug("Sent model capabilities discovered notification for {ProviderName} ({ModelCount} models, {EmbeddingCount} embeddings, {VisionCount} vision, {ImageGenCount} image gen, {VideoGenCount} video gen)", 
                    providerName, modelCount, embeddingCount, visionCount, imageGenCount, videoGenCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send model capabilities discovered notification for {ProviderName}", providerName);
            }
        }

        /// <inheritdoc />
        public async Task NotifyModelAvailabilityChangedAsync(string modelId, bool isAvailable)
        {
            try
            {
                var notification = new
                {
                    type = "ModelAvailabilityChanged",
                    data = new
                    {
                        modelId,
                        isAvailable,
                        timestamp = DateTime.UtcNow
                    }
                };

                await _hubContext.Clients.Group($"model-{modelId}").SendAsync("ModelUpdate", notification);
                await _hubContext.Clients.Group("navigation-updates").SendAsync("NavigationStateUpdate", notification);
                
                _logger.LogDebug("Sent model availability change notification for {ModelId} (Available: {IsAvailable})", modelId, isAvailable);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send model availability change notification for {ModelId}", modelId);
            }
        }
    }
}
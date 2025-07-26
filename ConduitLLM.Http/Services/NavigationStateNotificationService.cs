using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Http.Hubs;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Configuration;
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
        private readonly IHubContext<SystemNotificationHub> _hubContext;
        private readonly ILogger<NavigationStateNotificationService> _logger;

        /// <summary>
        /// Initializes a new instance of the NavigationStateNotificationService
        /// </summary>
        /// <param name="hubContext">SignalR hub context for SystemNotificationHub</param>
        /// <param name="logger">Logger instance</param>
        public NavigationStateNotificationService(
            IHubContext<SystemNotificationHub> hubContext,
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
                var notification = new ModelMappingNotification
                {
                    MappingId = mappingId,
                    ModelAlias = modelAlias,
                    ChangeType = changeType,
                    Priority = NotificationPriority.Medium
                };
                
                await _hubContext.Clients.All.SendAsync("OnModelMappingChanged", notification);
                
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
                // Convert bool isHealthy to HealthStatus enum
                var healthStatus = isHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy;
                if (status.Contains("degraded", StringComparison.OrdinalIgnoreCase))
                {
                    healthStatus = HealthStatus.Degraded;
                }
                
                // Parse provider name to ProviderType enum
                if (!Enum.TryParse<ProviderType>(providerName, true, out var providerType))
                {
                    _logger.LogWarning("Unknown provider type: {ProviderName}", providerName);
                    return;
                }
                
                var notification = new ProviderHealthNotification
                {
                    ProviderType = providerType,
                    Status = healthStatus.ToString(),
                    Priority = healthStatus == HealthStatus.Unhealthy ? NotificationPriority.High : NotificationPriority.Medium
                };
                
                await _hubContext.Clients.All.SendAsync("OnProviderHealthChanged", notification);
                
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
                // Parse provider name to ProviderType enum
                if (!Enum.TryParse<ProviderType>(providerName, true, out var providerType))
                {
                    _logger.LogWarning("Unknown provider type: {ProviderName}", providerName);
                    return;
                }
                
                var notification = new ModelCapabilitiesNotification
                {
                    ProviderType = providerType,
                    ModelCount = modelCount,
                    EmbeddingCount = embeddingCount,
                    VisionCount = visionCount,
                    ImageGenCount = imageGenCount,
                    VideoGenCount = videoGenCount,
                    Priority = NotificationPriority.Low
                };
                
                await _hubContext.Clients.All.SendAsync("OnModelCapabilitiesDiscovered", notification);
                
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
                var notification = new ModelAvailabilityNotification
                {
                    ModelId = modelId,
                    IsAvailable = isAvailable,
                    Priority = isAvailable ? NotificationPriority.Low : NotificationPriority.Medium
                };
                
                await _hubContext.Clients.All.SendAsync("OnModelAvailabilityChanged", notification);
                
                _logger.LogDebug("Sent model availability change notification for {ModelId} (Available: {IsAvailable})", modelId, isAvailable);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send model availability change notification for {ModelId}", modelId);
            }
        }
    }
}
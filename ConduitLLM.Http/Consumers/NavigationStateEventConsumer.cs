using MassTransit;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Events;
using ConduitLLM.Http.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ConduitLLM.Http.Consumers
{
    /// <summary>
    /// Consumes ModelMappingChanged events and pushes real-time updates through SignalR
    /// </summary>
    public class ModelMappingChangedNotificationConsumer : IConsumer<ModelMappingChanged>
    {
        private readonly INavigationStateNotificationService _notificationService;
        private readonly ILogger<ModelMappingChangedNotificationConsumer> _logger;

        /// <summary>
        /// Initializes a new instance of the ModelMappingChangedNotificationConsumer
        /// </summary>
        /// <param name="notificationService">Navigation state notification service</param>
        /// <param name="logger">Logger instance</param>
        public ModelMappingChangedNotificationConsumer(
            INavigationStateNotificationService notificationService,
            ILogger<ModelMappingChangedNotificationConsumer> logger)
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles ModelMappingChanged events by pushing updates through SignalR
        /// </summary>
        /// <param name="context">Message context containing the event</param>
        public async Task Consume(ConsumeContext<ModelMappingChanged> context)
        {
            var @event = context.Message;
            
            try
            {
                await _notificationService.NotifyModelMappingChangedAsync(
                    @event.MappingId,
                    @event.ModelAlias,
                    @event.ChangeType);
                
                _logger.LogInformation(
                    "Pushed real-time update for model mapping change: {ModelAlias} ({ChangeType})",
                    @event.ModelAlias,
                    @event.ChangeType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to push real-time update for model mapping change: {ModelAlias}", 
                    @event.ModelAlias);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }
    }

    /// <summary>
    /// Consumes ProviderHealthChanged events and pushes real-time updates through SignalR
    /// </summary>
    public class ProviderHealthChangedNotificationConsumer : IConsumer<ProviderHealthChanged>
    {
        private readonly INavigationStateNotificationService _notificationService;
        private readonly ILogger<ProviderHealthChangedNotificationConsumer> _logger;

        /// <summary>
        /// Initializes a new instance of the ProviderHealthChangedNotificationConsumer
        /// </summary>
        /// <param name="notificationService">Navigation state notification service</param>
        /// <param name="logger">Logger instance</param>
        public ProviderHealthChangedNotificationConsumer(
            INavigationStateNotificationService notificationService,
            ILogger<ProviderHealthChangedNotificationConsumer> logger)
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles ProviderHealthChanged events by pushing updates through SignalR
        /// </summary>
        /// <param name="context">Message context containing the event</param>
        public async Task Consume(ConsumeContext<ProviderHealthChanged> context)
        {
            var @event = context.Message;
            
            try
            {
                await _notificationService.NotifyProviderHealthChangedAsync(
                    @event.ProviderType.ToString(),
                    @event.IsHealthy,
                    @event.Status);
                
                _logger.LogInformation(
                    "Pushed real-time update for provider health change: {ProviderName} (Healthy: {IsHealthy})",
                    @event.ProviderType.ToString(),
                    @event.IsHealthy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to push real-time update for provider health change: {ProviderName}", 
                    @event.ProviderType.ToString());
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }
    }

    /// <summary>
    /// Consumes ModelCapabilitiesDiscovered events and pushes real-time updates through SignalR
    /// </summary>
    public class ModelCapabilitiesDiscoveredNotificationConsumer : IConsumer<ModelCapabilitiesDiscovered>
    {
        private readonly INavigationStateNotificationService _notificationService;
        private readonly ILogger<ModelCapabilitiesDiscoveredNotificationConsumer> _logger;

        /// <summary>
        /// Initializes a new instance of the ModelCapabilitiesDiscoveredNotificationConsumer
        /// </summary>
        /// <param name="notificationService">Navigation state notification service</param>
        /// <param name="logger">Logger instance</param>
        public ModelCapabilitiesDiscoveredNotificationConsumer(
            INavigationStateNotificationService notificationService,
            ILogger<ModelCapabilitiesDiscoveredNotificationConsumer> logger)
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles ModelCapabilitiesDiscovered events by pushing updates through SignalR
        /// </summary>
        /// <param name="context">Message context containing the event</param>
        public async Task Consume(ConsumeContext<ModelCapabilitiesDiscovered> context)
        {
            var @event = context.Message;
            
            try
            {
                // Calculate capability-specific counts from the model capabilities
                var embeddingCount = @event.ModelCapabilities.Values.Count(c => c.SupportsEmbeddings);
                var visionCount = @event.ModelCapabilities.Values.Count(c => c.SupportsVision);
                var imageGenCount = @event.ModelCapabilities.Values.Count(c => c.SupportsImageGeneration);
                var videoGenCount = @event.ModelCapabilities.Values.Count(c => c.SupportsVideoGeneration);

                await _notificationService.NotifyModelCapabilitiesDiscoveredAsync(
                    @event.ProviderType.ToString(),
                    @event.ModelCapabilities.Count,
                    embeddingCount,
                    visionCount,
                    imageGenCount,
                    videoGenCount);
                
                _logger.LogInformation(
                    "Pushed real-time update for model capabilities discovered: {ProviderName} ({ModelCount} models, {EmbeddingCount} embeddings, {VisionCount} vision, {ImageGenCount} image gen, {VideoGenCount} video gen)",
                    @event.ProviderType.ToString(),
                    @event.ModelCapabilities.Count,
                    embeddingCount,
                    visionCount,
                    imageGenCount,
                    videoGenCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to push real-time update for model capabilities discovered: {ProviderName}", 
                    @event.ProviderType.ToString());
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }
    }
}
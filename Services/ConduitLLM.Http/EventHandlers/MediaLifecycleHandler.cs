using MassTransit;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Handles MediaGenerationCompleted events to track generated media for lifecycle management.
    /// CRITICAL: This handler writes to MediaRecords table via IMediaLifecycleService.
    /// </summary>
    public class MediaLifecycleHandler : IConsumer<MediaGenerationCompleted>
    {
        private readonly IMediaLifecycleService _mediaLifecycleService;
        private readonly ILogger<MediaLifecycleHandler> _logger;

        public MediaLifecycleHandler(
            IMediaLifecycleService mediaLifecycleService,
            ILogger<MediaLifecycleHandler> logger)
        {
            _mediaLifecycleService = mediaLifecycleService ?? throw new ArgumentNullException(nameof(mediaLifecycleService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles MediaGenerationCompleted events by recording media metadata for lifecycle tracking
        /// </summary>
        public async Task Consume(ConsumeContext<MediaGenerationCompleted> context)
        {
            var @event = context.Message;
            
            try
            {
                _logger.LogInformation(
                    "Processing MediaGenerationCompleted event: {MediaType} for VirtualKey {VirtualKeyId} at {MediaUrl}",
                    @event.MediaType,
                    @event.VirtualKeyId,
                    @event.MediaUrl);

                // Create metadata for the media lifecycle service
                var metadata = new MediaLifecycleMetadata
                {
                    ContentType = @event.ContentType,
                    SizeBytes = @event.FileSizeBytes,
                    Provider = @event.GeneratedByModel,
                    Prompt = @event.GenerationPrompt,
                    StorageUrl = @event.MediaUrl,
                    ExpiresAt = @event.ExpiresAt
                };

                // Track the media using the service (writes to MediaRecords table)
                var mediaRecord = await _mediaLifecycleService.TrackMediaAsync(
                    @event.VirtualKeyId,
                    @event.StorageKey,
                    @event.MediaType.ToString(),
                    metadata);
                
                _logger.LogInformation(
                    "Successfully recorded {MediaType} lifecycle entry for VirtualKey {VirtualKeyId}: {StorageKey} ({FileSize} bytes)",
                    @event.MediaType,
                    @event.VirtualKeyId,
                    @event.StorageKey,
                    @event.FileSizeBytes);

                // TODO: Future enhancement - implement automatic cleanup based on expiration
                if (@event.ExpiresAt.HasValue)
                {
                    _logger.LogInformation(
                        "Media {StorageKey} is set to expire at {ExpiresAt}",
                        @event.StorageKey,
                        @event.ExpiresAt.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to record media lifecycle for {MediaType} at {StorageKey}", 
                    @event.MediaType,
                    @event.StorageKey);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }
    }
}
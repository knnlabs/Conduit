using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Events;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Handles MediaGenerationCompleted events to track generated media for lifecycle management
    /// Critical for future implementation of media cleanup when virtual keys are deleted
    /// </summary>
    public class MediaLifecycleHandler : IConsumer<MediaGenerationCompleted>
    {
        private readonly IMediaLifecycleRepository _mediaLifecycleRepository;
        private readonly ILogger<MediaLifecycleHandler> _logger;

        public MediaLifecycleHandler(
            IMediaLifecycleRepository mediaLifecycleRepository,
            ILogger<MediaLifecycleHandler> logger)
        {
            _mediaLifecycleRepository = mediaLifecycleRepository ?? throw new ArgumentNullException(nameof(mediaLifecycleRepository));
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

                // Create media lifecycle record
                var mediaRecord = new MediaLifecycleRecord
                {
                    VirtualKeyId = @event.VirtualKeyId,
                    MediaType = @event.MediaType.ToString(),
                    MediaUrl = @event.MediaUrl,
                    StorageKey = @event.StorageKey,
                    FileSizeBytes = @event.FileSizeBytes,
                    ContentType = @event.ContentType,
                    GeneratedByModel = @event.GeneratedByModel,
                    GenerationPrompt = @event.GenerationPrompt,
                    GeneratedAt = @event.GeneratedAt,
                    ExpiresAt = @event.ExpiresAt,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(@event.Metadata),
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                await _mediaLifecycleRepository.AddAsync(mediaRecord);
                
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
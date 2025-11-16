using MassTransit;
using ConduitLLM.Core.Events;

using ConduitLLM.Http.Interfaces;
namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Handles VideoGenerationStarted events to notify clients via SignalR
    /// Provides real-time status updates when video generation begins
    /// </summary>
    public class VideoGenerationStartedHandler : IConsumer<VideoGenerationStarted>
    {
        private readonly IVideoGenerationNotificationService _notificationService;
        private readonly ILogger<VideoGenerationStartedHandler> _logger;

        public VideoGenerationStartedHandler(
            IVideoGenerationNotificationService notificationService,
            ILogger<VideoGenerationStartedHandler> logger)
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles VideoGenerationStarted events by pushing real-time notifications
        /// </summary>
        public async Task Consume(ConsumeContext<VideoGenerationStarted> context)
        {
            var @event = context.Message;
            
            try
            {
                _logger.LogInformation(
                    "Processing VideoGenerationStarted event: RequestId {RequestId} on provider {Provider}",
                    @event.RequestId,
                    @event.Provider);

                // Send real-time notification
                await _notificationService.NotifyVideoGenerationStartedAsync(
                    @event.RequestId,
                    @event.Provider,
                    @event.StartedAt,
                    @event.EstimatedSeconds);
                
                _logger.LogInformation(
                    "Successfully notified clients of video generation start for request {RequestId}",
                    @event.RequestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to send video generation started notification for request {RequestId}", 
                    @event.RequestId);
                // Don't re-throw - notification failures shouldn't break the workflow
            }
        }
    }
}
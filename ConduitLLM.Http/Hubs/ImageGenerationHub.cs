using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// SignalR hub for real-time image generation status updates
    /// </summary>
    public class ImageGenerationHub : Hub
    {
        private readonly ILogger<ImageGenerationHub> _logger;

        public ImageGenerationHub(ILogger<ImageGenerationHub> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogDebug("Client connected to ImageGenerationHub: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogDebug("Client disconnected from ImageGenerationHub: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Subscribe to updates for a specific image generation task
        /// </summary>
        public async Task SubscribeToTask(string taskId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"image-{taskId}");
            _logger.LogDebug("Client {ConnectionId} subscribed to image task {TaskId}", 
                Context.ConnectionId, taskId);
        }

        /// <summary>
        /// Unsubscribe from updates for a specific image generation task
        /// </summary>
        public async Task UnsubscribeFromTask(string taskId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"image-{taskId}");
            _logger.LogDebug("Client {ConnectionId} unsubscribed from image task {TaskId}", 
                Context.ConnectionId, taskId);
        }
    }
}
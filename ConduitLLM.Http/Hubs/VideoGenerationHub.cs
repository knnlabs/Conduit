using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// SignalR hub for real-time video generation status updates
    /// </summary>
    public class VideoGenerationHub : Hub
    {
        private readonly ILogger<VideoGenerationHub> _logger;

        public VideoGenerationHub(ILogger<VideoGenerationHub> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogDebug("Client connected to VideoGenerationHub: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogDebug("Client disconnected from VideoGenerationHub: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Subscribe to updates for a specific video generation request
        /// </summary>
        public async Task SubscribeToRequest(string requestId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"video-{requestId}");
            _logger.LogDebug("Client {ConnectionId} subscribed to video request {RequestId}", 
                Context.ConnectionId, requestId);
        }

        /// <summary>
        /// Unsubscribe from updates for a specific video generation request
        /// </summary>
        public async Task UnsubscribeFromRequest(string requestId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"video-{requestId}");
            _logger.LogDebug("Client {ConnectionId} unsubscribed from video request {RequestId}", 
                Context.ConnectionId, requestId);
        }
    }
}
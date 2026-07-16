using ConduitLLM.Core.Constants;

namespace ConduitLLM.Gateway.Hubs
{
    /// <summary>
    /// SignalR hub for real-time image generation status updates.
    /// </summary>
    public class ImageGenerationHub : TaskSubscriptionHub
    {
        public ImageGenerationHub(
            ILogger<ImageGenerationHub> logger,
            IServiceProvider serviceProvider)
            : base(logger, serviceProvider)
        {
        }

        protected override string GetHubName() => "ImageGeneration";
        protected override string GetTaskGroupName(string taskId) => SignalRConstants.Groups.ImageTask(taskId);
    }
}

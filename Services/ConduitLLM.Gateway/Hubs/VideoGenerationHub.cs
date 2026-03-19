using ConduitLLM.Core.Constants;

namespace ConduitLLM.Gateway.Hubs
{
    /// <summary>
    /// SignalR hub for real-time video generation status updates.
    /// </summary>
    public class VideoGenerationHub : TaskSubscriptionHub
    {
        public VideoGenerationHub(
            ILogger<VideoGenerationHub> logger,
            IServiceProvider serviceProvider)
            : base(logger, serviceProvider)
        {
        }

        protected override string GetHubName() => "VideoGeneration";
        protected override string GetTaskGroupName(string taskId) => SignalRConstants.Groups.VideoTask(taskId);
    }
}

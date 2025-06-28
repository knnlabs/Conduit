namespace ConduitLLM.WebUI.Constants
{
    /// <summary>
    /// SignalR constants for WebUI - should match Core.Constants.SignalRConstants
    /// This is a duplicate to avoid WebUI referencing Core directly
    /// </summary>
    public static class SignalRConstants
    {
        /// <summary>
        /// SignalR Hub endpoints
        /// </summary>
        public static class Hubs
        {
            public const string NavigationState = "/hubs/navigation-state";
            public const string ImageGeneration = "/hubs/image-generation";
            public const string VideoGeneration = "/hubs/video-generation";
            public const string SystemNotification = "/hubs/notifications";
            public const string Task = "/hubs/tasks";
        }

        /// <summary>
        /// Hub method names that clients can invoke
        /// </summary>
        public static class HubMethods
        {
            // Common subscription methods
            public const string SubscribeToTask = "SubscribeToTask";
            public const string UnsubscribeFromTask = "UnsubscribeFromTask";
            
            // Navigation state methods
            public const string Subscribe = "Subscribe";
            public const string Unsubscribe = "Unsubscribe";
        }

        /// <summary>
        /// Client method names that hubs can invoke
        /// </summary>
        public static class ClientMethods
        {
            // Task progress events
            public const string TaskProgress = "TaskProgress";
            public const string TaskCompleted = "TaskCompleted";
            public const string TaskFailed = "TaskFailed";
            public const string TaskStarted = "TaskStarted";
            
            // Legacy event names for backward compatibility
            public const string VideoGenerationStarted = "VideoGenerationStarted";
            public const string VideoGenerationProgress = "VideoGenerationProgress";
            public const string VideoGenerationCompleted = "VideoGenerationCompleted";
            public const string VideoGenerationFailed = "VideoGenerationFailed";
            
            public const string ImageGenerationStarted = "ImageGenerationStarted";
            public const string ImageGenerationProgress = "ImageGenerationProgress";
            public const string ImageGenerationCompleted = "ImageGenerationCompleted";
            public const string ImageGenerationFailed = "ImageGenerationFailed";
            public const string ImageGenerationCancelled = "ImageGenerationCancelled";
            
            // Navigation state events
            public const string NavigationStateUpdated = "NavigationStateUpdated";
            
            // System notification events
            public const string NotificationReceived = "NotificationReceived";
            public const string SystemAlert = "SystemAlert";
        }
    }
}
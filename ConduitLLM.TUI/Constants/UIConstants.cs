namespace ConduitLLM.TUI.Constants;

/// <summary>
/// Centralized UI string constants for the TUI application.
/// </summary>
public static class UIConstants
{
    /// <summary>
    /// Connection status constants.
    /// </summary>
    public static class ConnectionStatus
    {
        public const string Connected = "Connected";
        public const string Disconnected = "Disconnected";
        public const string Connecting = "Connecting";
        public const string Failed = "Connection Failed";
        public const string Ready = "Ready";
    }

    /// <summary>
    /// Window and frame titles.
    /// </summary>
    public static class Titles
    {
        public const string MainWindow = "Conduit TUI";
        public const string ProviderCredentials = "Provider Credentials";
        public const string ModelMappings = "Model Mappings";
        public const string VirtualKeys = "Virtual Keys";
        public const string ChatHistory = "Chat History";
        public const string Settings = "Settings";
        public const string About = "About Conduit TUI";
        public const string KeyboardShortcuts = "Keyboard Shortcuts";
        public const string SystemHealth = "System Health";
        public const string Configuration = "Configuration";
        public const string ImageGeneration = "Image Generation";
        public const string VideoGeneration = "Video Generation";
        public const string Logs = "Logs (Ctrl+L to toggle)";
    }

    /// <summary>
    /// Common button labels.
    /// </summary>
    public static class ButtonLabels
    {
        public const string Add = "Add";
        public const string Edit = "Edit";
        public const string Delete = "Delete";
        public const string Save = "Save";
        public const string Cancel = "Cancel";
        public const string OK = "OK";
        public const string Refresh = "Refresh";
        public const string Send = "Send [Ctrl+Enter]";
        public const string Clear = "Clear";
        public const string GenerateVideo = "Generate Video";
        public const string GenerateImage = "Generate Image";
        public const string CheckStatus = "Check Status";
        public const string DiscoverModels = "Discover Models";
        public const string TestConnection = "Test Connection";
        public const string ResetToDefaults = "Reset to Defaults";
    }

    /// <summary>
    /// Status messages.
    /// </summary>
    public static class StatusMessages
    {
        public const string Loading = "Loading...";
        public const string LoadingProviders = "Loading providers...";
        public const string LoadingConfiguration = "Loading configuration...";
        public const string ConfigurationLoaded = "Configuration loaded";
        public const string ConfigurationSaved = "Configuration saved";
        public const string SaveNotImplemented = "Configuration save not yet implemented";
        public const string Success = "Success";
        public const string Failed = "Failed";
        public const string Error = "Error";
        public const string TestingConnection = "Testing connection...";
        public const string TestCompleted = "Test completed successfully";
        public const string TestFailed = "Test failed";
    }

    /// <summary>
    /// SignalR hub paths and method names.
    /// </summary>
    public static class SignalR
    {
        public static class Hubs
        {
            public const string Notifications = "/hubs/notifications";
            public const string VideoGeneration = "/hubs/video-generation";
            public const string ImageGeneration = "/hubs/image-generation";
        }

        public static class Methods
        {
            public const string OnModelMappingChanged = "OnModelMappingChanged";
            public const string OnProviderHealthChanged = "OnProviderHealthChanged";
            public const string OnModelCapabilitiesDiscovered = "OnModelCapabilitiesDiscovered";
            public const string VideoGenerationProgress = "VideoGenerationProgress";
            public const string VideoGenerationCompleted = "VideoGenerationCompleted";
            public const string VideoGenerationFailed = "VideoGenerationFailed";
            public const string ImageGenerationProgress = "ImageGenerationProgress";
            public const string ImageGenerationCompleted = "ImageGenerationCompleted";
            public const string ImageGenerationFailed = "ImageGenerationFailed";
            public const string JoinTaskGroup = "JoinTaskGroup";
            public const string LeaveTaskGroup = "LeaveTaskGroup";
            public const string SettingsUpdated = "SettingsUpdated";
        }
    }

    /// <summary>
    /// Configuration keys and headers.
    /// </summary>
    public static class Configuration
    {
        public const string WebUIVirtualKey = "WebUI_VirtualKey";
        public const string ApiKeyHeader = "X-API-Key";
        public const string DefaultCoreApiUrl = "http://localhost:5000";
        public const string DefaultAdminApiUrl = "http://localhost:5002";
    }

    /// <summary>
    /// Application information.
    /// </summary>
    public static class AppInfo
    {
        public const string Version = "v1.0.0";
        public const string FullTitle = "Conduit TUI v1.0.0";
        public const string Description = "Terminal User Interface for Conduit LLM";
        public const string Copyright = "© 2025 KNN Labs, Inc.";
    }

    /// <summary>
    /// Log panel status.
    /// </summary>
    public static class LogPanelStatus
    {
        public const string Visible = "visible";
        public const string Hidden = "hidden";
    }

    /// <summary>
    /// Common error message templates.
    /// </summary>
    public static class ErrorMessages
    {
        public const string LoadFailed = "Failed to load {0}";
        public const string SaveFailed = "Failed to save {0}";
        public const string ConnectionFailed = "Failed to connect to {0}";
        public const string OperationFailed = "Failed to {0}";
        public const string ErrorFormat = "Error: {0}";
    }

    /// <summary>
    /// Provider status values.
    /// </summary>
    public static class ProviderStatus
    {
        public const string Enabled = "Enabled";
        public const string Disabled = "Disabled";
    }
}
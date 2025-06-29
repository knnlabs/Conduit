namespace ConduitLLM.AdminClient.Constants;

/// <summary>
/// Centralized API endpoint constants for admin operations.
/// </summary>
public static class ApiEndpoints
{
    /// <summary>
    /// Virtual key management endpoints.
    /// </summary>
    public static class VirtualKeys
    {
        /// <summary>
        /// Base virtual keys endpoint.
        /// </summary>
        public const string Base = "/api/virtualkeys";
    }

    /// <summary>
    /// Provider management endpoints.
    /// </summary>
    public static class Providers
    {
        /// <summary>
        /// Base providers endpoint.
        /// </summary>
        public const string Base = "/api/providers";

        /// <summary>
        /// Provider credentials endpoint.
        /// </summary>
        public const string Credentials = "/api/provider-credentials";

        /// <summary>
        /// Provider health endpoint.
        /// </summary>
        public const string Health = "/api/provider-health";

        /// <summary>
        /// Provider models endpoint.
        /// </summary>
        public const string Models = "/api/provider-models";
    }

    /// <summary>
    /// Model management endpoints.
    /// </summary>
    public static class Models
    {
        /// <summary>
        /// Model costs endpoint.
        /// </summary>
        public const string Costs = "/api/model-costs";

        /// <summary>
        /// Model mappings endpoint.
        /// </summary>
        public const string Mappings = "/api/model-mappings";
    }

    /// <summary>
    /// System and configuration endpoints.
    /// </summary>
    public static class System
    {
        /// <summary>
        /// System settings endpoint.
        /// </summary>
        public const string Settings = "/api/settings";

        /// <summary>
        /// Database backup endpoint.
        /// </summary>
        public const string DatabaseBackup = "/api/database-backup";

        /// <summary>
        /// System information endpoint.
        /// </summary>
        public const string Info = "/api/system";

        /// <summary>
        /// Discovery endpoint.
        /// </summary>
        public const string Discovery = "/api/discovery";

        /// <summary>
        /// HTTP client configuration endpoint.
        /// </summary>
        public const string HttpClientConfiguration = "/api/http-client-configuration";

        /// <summary>
        /// Cache configuration endpoint.
        /// </summary>
        public const string CacheConfiguration = "/api/cache-configuration";
    }

    /// <summary>
    /// Analytics and monitoring endpoints.
    /// </summary>
    public static class Analytics
    {
        /// <summary>
        /// Base analytics endpoint.
        /// </summary>
        public const string Base = "/api/analytics";
    }

    /// <summary>
    /// Audio configuration endpoints.
    /// </summary>
    public static class Audio
    {
        /// <summary>
        /// Audio configuration endpoint.
        /// </summary>
        public const string Configuration = "/api/audio-configuration";
    }

    /// <summary>
    /// IP filtering endpoints.
    /// </summary>
    public static class IpFilter
    {
        /// <summary>
        /// IP filter endpoint.
        /// </summary>
        public const string Base = "/api/ip-filter";
    }

    /// <summary>
    /// Notification endpoints.
    /// </summary>
    public static class Notifications
    {
        /// <summary>
        /// Base notifications endpoint.
        /// </summary>
        public const string Base = "/api/notifications";
    }
}
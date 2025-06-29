namespace ConduitLLM.CoreClient.Constants;

/// <summary>
/// Centralized API endpoint constants for type-safe endpoint management.
/// </summary>
public static class ApiEndpoints
{
    /// <summary>
    /// Root-level API endpoints (non-versioned).
    /// </summary>
    public static class Root
    {
        /// <summary>
        /// Metrics endpoint.
        /// </summary>
        public const string Metrics = "/metrics";
    }

    /// <summary>
    /// Version 1 API endpoints.
    /// </summary>
    public static class V1
    {
        /// <summary>
        /// Chat completion endpoints.
        /// </summary>
        public static class Chat
        {
            /// <summary>
            /// Chat completions endpoint.
            /// </summary>
            public const string Completions = "/v1/chat/completions";
        }

        /// <summary>
        /// Image generation and manipulation endpoints.
        /// </summary>
        public static class Images
        {
            /// <summary>
            /// Image generations endpoint.
            /// </summary>
            public const string Generations = "/v1/images/generations";

            /// <summary>
            /// Async image generations endpoint.
            /// </summary>
            public const string AsyncGenerations = "/v1/images/generations/async";

            /// <summary>
            /// Image edits endpoint.
            /// </summary>
            public const string Edits = "/v1/images/edits";

            /// <summary>
            /// Image variations endpoint.
            /// </summary>
            public const string Variations = "/v1/images/variations";
        }

        /// <summary>
        /// Video generation endpoints.
        /// </summary>
        public static class Videos
        {
            /// <summary>
            /// Async video generations endpoint.
            /// Note: Only async video generation is supported.
            /// </summary>
            public const string AsyncGenerations = "/v1/videos/generations/async";
        }

        /// <summary>
        /// Task management endpoints.
        /// </summary>
        public static class Tasks
        {
            /// <summary>
            /// Base tasks endpoint.
            /// </summary>
            public const string Base = "/v1/tasks";
        }

        /// <summary>
        /// Embeddings endpoint.
        /// </summary>
        public static class Embeddings
        {
            /// <summary>
            /// Text embeddings endpoint.
            /// </summary>
            public const string Base = "/v1/embeddings";
        }

        /// <summary>
        /// Audio processing endpoints.
        /// </summary>
        public static class Audio
        {
            /// <summary>
            /// Audio transcriptions endpoint.
            /// </summary>
            public const string Transcriptions = "/v1/audio/transcriptions";

            /// <summary>
            /// Audio translations endpoint.
            /// </summary>
            public const string Translations = "/v1/audio/translations";

            /// <summary>
            /// Audio speech endpoint.
            /// </summary>
            public const string Speech = "/v1/audio/speech";
        }

        /// <summary>
        /// Model information endpoints.
        /// </summary>
        public static class Models
        {
            /// <summary>
            /// Models list endpoint.
            /// </summary>
            public const string Base = "/v1/models";
        }

        /// <summary>
        /// Batch operations endpoints.
        /// Note: Core API uses specific batch endpoints, not a generic /v1/batch endpoint.
        /// </summary>
        public static class Batch
        {
            /// <summary>
            /// Batch spend updates endpoint.
            /// </summary>
            public const string SpendUpdates = "/v1/batch/spend-updates";
            
            /// <summary>
            /// Batch virtual keys endpoint.
            /// </summary>
            public const string VirtualKeys = "/v1/batch/virtual-keys";
        }

        /// <summary>
        /// Metrics endpoints.
        /// </summary>
        public static class Metrics
        {
            /// <summary>
            /// Base metrics endpoint.
            /// </summary>
            public const string Base = "/v1/metrics";
        }

        // Note: Health endpoint is at root level (/health), not under /v1
        // The SDK's HealthService handles this correctly
    }
}
using ConduitLLM.Providers.Common.Models;

namespace ConduitLLM.Providers.Providers.Gemini
{
    /// <summary>
    /// Revised client for interacting with the Google Gemini API using the new client hierarchy.
    /// Provides standardized handling of API requests and responses with enhanced error handling.
    /// </summary>
    /// <remarks>
    /// This is the main partial class declaration for GeminiClient.
    /// Implementation is split across multiple partial files for better organization:
    /// - GeminiClient.Main.cs: Constructor and configuration
    /// - GeminiClient.Chat.cs: Chat completion functionality
    /// - GeminiClient.Streaming.cs: Streaming functionality
    /// - GeminiClient.Models.cs: Model listing
    /// - GeminiClient.Mapping.cs: Request/response mapping
    /// - GeminiClient.Utilities.cs: Utility methods and capabilities
    /// - GeminiClient.Authentication.cs: Authentication verification
    /// </remarks>
    public partial class GeminiClient : CustomProviderClient
    {
        // Implementation is in partial classes - see remarks above for organization
    }
}
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;

using Microsoft.Extensions.Logging;
// Use aliases to avoid ambiguities

namespace ConduitLLM.Providers.OpenAICompatible
{
    /// <summary>
    /// Base class for LLM clients that implement OpenAI-compatible APIs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This abstract class serves as a foundation for providers that implement APIs compatible 
    /// with the OpenAI API format, such as OpenAI, Azure OpenAI, Mistral AI, Groq, and others.
    /// </para>
    /// <para>
    /// It provides a standardized implementation of the <see cref="ILLMClient"/> interface 
    /// with customizable methods that derived classes can override to accommodate provider-specific
    /// behaviors while maintaining a consistent interface.
    /// </para>
    /// <para>
    /// Key features:
    /// - Standard implementations for chat completions, embeddings, and image generation
    /// - Consistent error handling and response mapping
    /// - Support for streaming responses
    /// - Extensibility through virtual methods
    /// - Comprehensive logging and diagnostics
    /// </para>
    /// </remarks>
    public abstract partial class OpenAICompatibleClient : BaseLLMClient
    {
        /// <summary>
        /// Gets the base URL for API requests.
        /// </summary>
        protected readonly string BaseUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenAICompatibleClient"/> class.
        /// </summary>
        /// <param name="provider">The provider entity containing configuration.</param>
        /// <param name="primaryKeyCredential">The primary key credential to use for requests.</param>
        /// <param name="providerModelId">The provider's model identifier.</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory.</param>
        /// <param name="providerName">The name of this LLM provider.</param>
        /// <param name="baseUrl">The base URL for API requests.</param>
        protected OpenAICompatibleClient(
            Provider provider,
            ProviderKeyCredential primaryKeyCredential,
            string providerModelId,
            ILogger logger,
            IHttpClientFactory? httpClientFactory = null,
            string? providerName = null,
            string? baseUrl = null,
            ProviderDefaultModels? defaultModels = null)
            : base(provider, primaryKeyCredential, providerModelId, logger, httpClientFactory, providerName, defaultModels)
        {
            BaseUrl = baseUrl ?? "https://api.openai.com/v1";
        }
    }
}
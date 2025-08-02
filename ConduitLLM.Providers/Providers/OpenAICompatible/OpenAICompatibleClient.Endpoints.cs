namespace ConduitLLM.Providers.Providers.OpenAICompatible
{
    /// <summary>
    /// OpenAICompatibleClient partial class containing endpoint URL methods.
    /// </summary>
    public abstract partial class OpenAICompatibleClient
    {
        /// <summary>
        /// Gets the chat completion endpoint for the provider.
        /// </summary>
        /// <returns>The full URL for the chat completions endpoint.</returns>
        /// <remarks>
        /// Derived classes can override this method to provide custom endpoints.
        /// </remarks>
        protected virtual string GetChatCompletionEndpoint()
        {
            return $"{BaseUrl}/chat/completions";
        }

        /// <summary>
        /// Gets the models endpoint for the provider.
        /// </summary>
        /// <returns>The full URL for the models endpoint.</returns>
        /// <remarks>
        /// Derived classes can override this method to provide custom endpoints.
        /// </remarks>
        protected virtual string GetModelsEndpoint()
        {
            return $"{BaseUrl}/models";
        }

        /// <summary>
        /// Gets the embedding endpoint for the provider.
        /// </summary>
        /// <returns>The full URL for the embeddings endpoint.</returns>
        /// <remarks>
        /// Derived classes can override this method to provide custom endpoints.
        /// </remarks>
        protected virtual string GetEmbeddingEndpoint()
        {
            return $"{BaseUrl}/embeddings";
        }

        /// <summary>
        /// Gets the image generation endpoint for the provider.
        /// </summary>
        /// <returns>The full URL for the image generations endpoint.</returns>
        /// <remarks>
        /// Derived classes can override this method to provide custom endpoints.
        /// </remarks>
        protected virtual string GetImageGenerationEndpoint()
        {
            return $"{BaseUrl}/images/generations";
        }
    }
}
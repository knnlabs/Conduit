using ConduitLLM.Providers.Helpers;

namespace ConduitLLM.Providers.OpenAI
{
    /// <summary>
    /// OpenAIClient partial class containing endpoint URL methods.
    /// </summary>
    public partial class OpenAIClient
    {
        /// <summary>
        /// Gets the chat completion endpoint for the provider.
        /// </summary>
        /// <returns>The full URL for the chat completions endpoint.</returns>
        /// <remarks>
        /// For Azure OpenAI, constructs a deployment-specific endpoint with API version.
        /// For standard OpenAI, returns the default chat completions endpoint.
        /// </remarks>
        protected override string GetChatCompletionEndpoint()
        {
            if (_isAzure)
            {
                var url = UrlBuilder.Combine(BaseUrl, "openai", "deployments", ProviderModelId, "chat/completions");
                return UrlBuilder.AppendQueryString(url, ("api-version", Constants.AzureApiVersion));
            }
            return UrlBuilder.Combine(BaseUrl, Constants.Endpoints.ChatCompletions);
        }

        /// <summary>
        /// Gets the embedding endpoint for the provider.
        /// </summary>
        /// <returns>The full URL for the embeddings endpoint.</returns>
        /// <remarks>
        /// For Azure OpenAI, constructs a deployment-specific endpoint with API version.
        /// For standard OpenAI, returns the default embeddings endpoint.
        /// </remarks>
        protected override string GetEmbeddingEndpoint()
        {
            if (_isAzure)
            {
                var url = UrlBuilder.Combine(BaseUrl, "openai", "deployments", ProviderModelId, "embeddings");
                return UrlBuilder.AppendQueryString(url, ("api-version", Constants.AzureApiVersion));
            }
            return UrlBuilder.Combine(BaseUrl, Constants.Endpoints.Embeddings);
        }

        /// <summary>
        /// Gets the image generation endpoint for the provider.
        /// </summary>
        /// <returns>The full URL for the image generations endpoint.</returns>
        /// <remarks>
        /// For Azure OpenAI, constructs a deployment-specific endpoint with API version.
        /// For standard OpenAI, returns the default image generations endpoint.
        /// </remarks>
        protected override string GetImageGenerationEndpoint()
        {
            if (_isAzure)
            {
                var url = UrlBuilder.Combine(BaseUrl, "openai", "deployments", ProviderModelId, "images/generations");
                return UrlBuilder.AppendQueryString(url, ("api-version", Constants.AzureApiVersion));
            }
            return UrlBuilder.Combine(BaseUrl, Constants.Endpoints.ImageGenerations);
        }

        /// <summary>
        /// Gets the models endpoint for the provider.
        /// </summary>
        /// <returns>The full URL for the models endpoint.</returns>
        /// <remarks>
        /// For Azure OpenAI, returns the deployments listing endpoint with API version.
        /// For standard OpenAI, returns the default models endpoint.
        /// </remarks>
        /// <exception cref="NotSupportedException">Azure OpenAI does not support listing models in the same format as OpenAI.</exception>
        protected override string GetModelsEndpoint()
        {
            if (_isAzure)
            {
                // Azure uses a different endpoint structure for listing deployments
                var url = UrlBuilder.Combine(BaseUrl, "openai", "deployments");
                return UrlBuilder.AppendQueryString(url, ("api-version", Constants.AzureApiVersion));
            }
            return UrlBuilder.Combine(BaseUrl, Constants.Endpoints.Models);
        }
    }
}
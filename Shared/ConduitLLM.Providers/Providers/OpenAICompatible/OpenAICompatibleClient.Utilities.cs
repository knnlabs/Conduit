namespace ConduitLLM.Providers.OpenAICompatible
{
    /// <summary>
    /// OpenAICompatibleClient partial class containing utility and helper methods.
    /// </summary>
    public abstract partial class OpenAICompatibleClient
    {
        /// <summary>
        /// Translates an unsuccessful HTTP response into a provider-specific exception.
        /// Return <see langword="null"/> to use the shared communication exception.
        /// </summary>
        protected virtual Exception? TranslateHttpError(
            HttpResponseMessage response,
            string responseContent) => null;

        /// <summary>
        /// Configure the HTTP client with provider-specific settings.
        /// </summary>
        /// <param name="client">The HTTP client to configure.</param>
        /// <param name="apiKey">The API key to use for authentication.</param>
        /// <remarks>
        /// This method adds standard headers and authentication to the HTTP client.
        /// Derived classes can override this method to provide provider-specific configuration.
        /// </remarks>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            base.ConfigureHttpClient(client, apiKey);

            // Set the base address if not already set
            if (client.BaseAddress == null && !string.IsNullOrEmpty(BaseUrl))
            {
                client.BaseAddress = new Uri(BaseUrl);
            }
        }

        // ExtractEnhancedErrorMessage is inherited from BaseLLMClient
    }
}

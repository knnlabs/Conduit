namespace ConduitLLM.Providers.Groq
{
    /// <summary>
    /// GroqClient partial class containing authentication methods.
    /// Uses the base class implementation which verifies against /models endpoint with Bearer auth.
    /// </summary>
    public partial class GroqClient
    {
        /// <summary>
        /// Gets the health check URL for Groq (uses /models endpoint via base class).
        /// </summary>
        public override string GetHealthCheckUrl(string? baseUrl = null)
        {
            var effectiveBaseUrl = !string.IsNullOrWhiteSpace(baseUrl)
                ? baseUrl.TrimEnd('/')
                : (!string.IsNullOrWhiteSpace(Provider.BaseUrl)
                    ? Provider.BaseUrl.TrimEnd('/')
                    : Constants.Urls.DefaultBaseUrl.TrimEnd('/'));

            return $"{effectiveBaseUrl}/models";
        }
    }
}

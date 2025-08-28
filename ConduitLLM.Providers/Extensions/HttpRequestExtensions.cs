using System.Net.Http;

namespace ConduitLLM.Providers.Extensions
{
    /// <summary>
    /// Extension methods for HTTP requests to support provider key tracking.
    /// </summary>
    public static class HttpRequestExtensions
    {
        private const string KeyCredentialIdKey = "KeyCredentialId";
        private const string ProviderIdKey = "ProviderId";
        
        /// <summary>
        /// Sets the key credential context on an HTTP request for error tracking.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="keyCredentialId">The ID of the key credential being used.</param>
        /// <param name="providerId">The ID of the provider.</param>
        public static void SetKeyCredentialContext(
            this HttpRequestMessage request, 
            int keyCredentialId,
            int providerId)
        {
            request.Options.Set(new HttpRequestOptionsKey<int>(KeyCredentialIdKey), keyCredentialId);
            request.Options.Set(new HttpRequestOptionsKey<int>(ProviderIdKey), providerId);
        }
        
        /// <summary>
        /// Gets the key credential context from an HTTP request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>A tuple containing the key ID and provider ID if set, otherwise null values.</returns>
        public static (int? keyId, int? providerId) GetKeyCredentialContext(
            this HttpRequestMessage request)
        {
            request.Options.TryGetValue(new HttpRequestOptionsKey<int>(KeyCredentialIdKey), out var keyId);
            request.Options.TryGetValue(new HttpRequestOptionsKey<int>(ProviderIdKey), out var providerId);
            
            return (keyId == 0 ? null : keyId, providerId == 0 ? null : providerId);
        }
        
        /// <summary>
        /// Tries to get the key credential ID from an HTTP request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="keyCredentialId">The key credential ID if found.</param>
        /// <returns>True if the key credential ID was found, false otherwise.</returns>
        public static bool TryGetKeyCredentialId(
            this HttpRequestMessage request,
            out int keyCredentialId)
        {
            if (request.Options.TryGetValue(new HttpRequestOptionsKey<int>(KeyCredentialIdKey), out keyCredentialId))
            {
                return keyCredentialId > 0;
            }
            
            keyCredentialId = 0;
            return false;
        }
    }
}
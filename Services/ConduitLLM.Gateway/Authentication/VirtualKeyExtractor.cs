using ConduitLLM.Core.Utilities;

namespace ConduitLLM.Gateway.Authentication
{
    /// <summary>
    /// The single policy for extracting a virtual key credential from an HTTP request.
    /// </summary>
    internal static class VirtualKeyExtractor
    {
        /// <summary>
        /// Extracts the virtual key from the request, or null when none is present.
        /// Query-string credentials (<c>access_token</c>, <c>api_key</c>) are accepted only on
        /// SignalR hub paths: browsers cannot set headers on WebSocket handshakes, which is the
        /// sole legitimate use. Query strings land in access logs and referrers, so header
        /// credentials are required everywhere else.
        /// </summary>
        public static string? Extract(HttpContext? context)
        {
            if (context == null)
            {
                return null;
            }

            if (context.Request.Path.StartsWithSegments("/hubs"))
            {
                // access_token is SignalR's standard query parameter; api_key is a legacy alias.
                if (context.Request.Query.TryGetValue("access_token", out var accessToken))
                {
                    return accessToken.ToString();
                }

                if (context.Request.Query.TryGetValue("api_key", out var apiKey))
                {
                    return apiKey.ToString();
                }
            }

            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                var token = SpanHelper.ExtractBearerToken(authHeader);
                if (!string.IsNullOrEmpty(token))
                {
                    return token;
                }
            }

            var apiKeyHeader = context.Request.Headers["X-API-Key"].FirstOrDefault();
            if (!string.IsNullOrEmpty(apiKeyHeader))
            {
                return apiKeyHeader.Trim();
            }

            return null;
        }
    }
}

using System.Text;
namespace ConduitLLM.Core.Utilities
{
    /// <summary>
    /// Provides standardized URL construction methods for all providers.
    /// </summary>
    public static class UrlBuilder
    {
        /// <summary>
        /// Combines a base URL with a path, ensuring proper formatting.
        /// </summary>
        /// <param name="baseUrl">The base URL (e.g., "https://api.example.com" or "https://api.example.com/")</param>
        /// <param name="path">The path to append (e.g., "/v1/models" or "v1/models")</param>
        /// <returns>A properly formatted URL with no duplicate slashes</returns>
        /// <exception cref="ArgumentException">Thrown when baseUrl is null or whitespace</exception>
        public static string Combine(string baseUrl, string path)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("Base URL cannot be null or whitespace", nameof(baseUrl));
            }

            // Handle null or empty path
            if (string.IsNullOrWhiteSpace(path))
            {
                return baseUrl.TrimEnd('/');
            }

            // Use SpanHelper for zero-allocation URL combining
            return SpanHelper.CombineUrl(baseUrl, path);
        }

        /// <summary>
        /// Combines multiple URL segments into a single URL.
        /// </summary>
        /// <param name="segments">The URL segments to combine</param>
        /// <returns>A properly formatted URL</returns>
        /// <exception cref="ArgumentException">Thrown when no segments are provided or first segment is invalid</exception>
        public static string Combine(params string[] segments)
        {
            if (segments == null || segments.Length == 0)
            {
                throw new ArgumentException("At least one URL segment must be provided", nameof(segments));
            }

            // Start with the first segment (base URL)
            var result = segments[0];
            
            // Combine remaining segments
            for (int i = 1; i < segments.Length; i++)
            {
                result = Combine(result, segments[i]);
            }

            return result;
        }

        /// <summary>
        /// Appends query parameters to a URL.
        /// </summary>
        /// <param name="url">The base URL</param>
        /// <param name="parameters">The query parameters to append</param>
        /// <returns>URL with query parameters appended</returns>
        public static string AppendQueryString(string url, params (string key, string value)[] parameters)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("URL cannot be null or whitespace", nameof(url));
            }

            if (parameters == null || parameters.Length == 0)
            {
                return url;
            }

            var queryString = new StringBuilder();
            var hasExistingQuery = url.Contains('?');

            foreach (var (key, value) in parameters)
            {
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (queryString.Length == 0)
                {
                    queryString.Append(hasExistingQuery ? '&' : '?');
                }
                else
                {
                    queryString.Append('&');
                }

                queryString.Append(Uri.EscapeDataString(key));
                queryString.Append('=');
                queryString.Append(Uri.EscapeDataString(value));
            }

            return url + queryString.ToString();
        }

        /// <summary>
        /// Validates that a URL is well-formed.
        /// </summary>
        /// <param name="url">The URL to validate</param>
        /// <returns>True if the URL is valid, false otherwise</returns>
        public static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
                   (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
        }
    }
}

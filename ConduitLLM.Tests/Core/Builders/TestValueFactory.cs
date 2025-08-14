using System;
using System.Text;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Tests.Core.Builders
{
    /// <summary>
    /// Factory for creating common test values.
    /// </summary>
    public static class TestValueFactory
    {
        /// <summary>
        /// Creates a test storage key for the given media type.
        /// </summary>
        public static string CreateStorageKey(MediaType mediaType, string hash = "test-hash")
        {
            var typeFolder = mediaType.ToString().ToLower();
            var dateFolder = DateTime.UtcNow.ToString("yyyy/MM/dd");
            var extension = mediaType switch
            {
                MediaType.Image => ".jpg",
                MediaType.Video => ".mp4",
                MediaType.Audio => ".mp3",
                _ => ".bin"
            };
            return $"{typeFolder}/{dateFolder}/{hash}{extension}";
        }

        /// <summary>
        /// Creates a test URL for the given storage key.
        /// </summary>
        public static string CreateUrl(string storageKey, string baseUrl = "https://storage.example.com")
        {
            return $"{baseUrl.TrimEnd('/')}/{storageKey}";
        }

        /// <summary>
        /// Creates a test content hash.
        /// </summary>
        public static string CreateContentHash(string suffix = "")
        {
            return $"sha256-{Guid.NewGuid().ToString("N")[..16]}{suffix}";
        }

        /// <summary>
        /// Creates a test base64 string.
        /// </summary>
        public static string CreateBase64Data(string content = "test data")
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
        }

        /// <summary>
        /// Creates a test file name for the given media type.
        /// </summary>
        public static string CreateFileName(MediaType mediaType, string prefix = "test")
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var extension = mediaType switch
            {
                MediaType.Image => ".jpg",
                MediaType.Video => ".mp4",
                MediaType.Audio => ".mp3",
                _ => ".bin"
            };
            return $"{prefix}_{timestamp}{extension}";
        }
    }
}
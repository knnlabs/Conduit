using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ConduitLLM.Core.Utilities
{
    /// <summary>
    /// Provides utility methods for image processing, validation, and conversion
    /// operations related to vision-capable models.
    /// </summary>
    public static class ImageUtility
    {
        // Constants for image validation
        private const int MaxImageFileSizeBytes = 20 * 1024 * 1024; // 20MB default

        private static readonly Dictionary<string, byte[]> ImageSignatures = new Dictionary<string, byte[]>
        {
            ["image/jpeg"] = new byte[] { 0xFF, 0xD8, 0xFF },
            ["image/png"] = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
            ["image/gif"] = new byte[] { 0x47, 0x49, 0x46, 0x38 },
            ["image/webp"] = Encoding.ASCII.GetBytes("RIFF\0\0\0\0WEBP"),
            ["image/bmp"] = new byte[] { 0x42, 0x4D }
        };

        private static readonly string[] SupportedMimeTypes = new[]
        {
            "image/jpeg", "image/png", "image/gif", "image/webp", "image/bmp"
        };

        /// <summary>
        /// Validates an image file based on size and format.
        /// </summary>
        /// <param name="imageData">The image data as a byte array</param>
        /// <param name="maxSizeBytes">Maximum allowed size in bytes</param>
        /// <param name="errorMessage">Error message if validation fails</param>
        /// <returns>True if the image is valid, false otherwise</returns>
        public static bool ValidateImage(byte[] imageData, out string errorMessage, int maxSizeBytes = MaxImageFileSizeBytes)
        {
            errorMessage = string.Empty;

            // Check if image data is null or empty
            if (imageData == null || imageData.Length == 0)
            {
                errorMessage = "Image data is null or empty";
                return false;
            }

            // Check size limit
            if (imageData.Length > maxSizeBytes)
            {
                errorMessage = $"Image size ({imageData.Length / (1024 * 1024)}MB) exceeds the maximum allowed size ({maxSizeBytes / (1024 * 1024)}MB)";
                return false;
            }

            // Check format signature
            string? detectedMimeType = DetectMimeType(imageData);
            if (detectedMimeType == null)
            {
                errorMessage = "Unknown or unsupported image format";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Detects the MIME type of an image from its binary data by examining file signatures.
        /// </summary>
        /// <param name="imageData">The image data as a byte array</param>
        /// <returns>The detected MIME type, or null if the format is unknown</returns>
        public static string? DetectMimeType(byte[] imageData)
        {
            if (imageData == null || imageData.Length < 2)
                return null;

            foreach (var signature in ImageSignatures)
            {
                if (StartsWithSignature(imageData, signature.Value))
                {
                    return signature.Key;
                }
            }

            return null;
        }

        /// <summary>
        /// Converts image bytes to a base64 data URL with the correct MIME type.
        /// </summary>
        /// <param name="imageData">The image data as a byte array</param>
        /// <param name="mimeType">The MIME type of the image</param>
        /// <returns>A data URL containing the base64-encoded image</returns>
        public static string ToBase64DataUrl(byte[] imageData, string? mimeType = null)
        {
            // Auto-detect MIME type if not provided
            mimeType ??= DetectMimeType(imageData) ?? "application/octet-stream";

            return $"data:{mimeType};base64,{Convert.ToBase64String(imageData)}";
        }

        /// <summary>
        /// Calculates a hash of the image data for caching or comparison purposes.
        /// </summary>
        /// <param name="imageData">The image data as a byte array</param>
        /// <returns>A SHA-256 hash of the image data as a hex string</returns>
        public static string CalculateImageHash(byte[] imageData)
        {
            using var sha256 = SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(imageData);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Validates a URL for security purposes.
        /// </summary>
        /// <param name="url">The URL to validate</param>
        /// <param name="allowedDomains">Optional list of allowed domains</param>
        /// <returns>True if the URL is valid and allowed, false otherwise</returns>
        public static bool ValidateImageUrl(string url, IEnumerable<string>? allowedDomains = null)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            // Data URLs are always valid
            if (url.StartsWith("data:image/"))
                return true;

            // Check if it's a valid URI
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
                return false;

            // Only allow HTTP and HTTPS
            if (uri.Scheme != "http" && uri.Scheme != "https")
                return false;

            // If allowed domains are specified, check against them
            if (allowedDomains != null)
            {
                bool domainAllowed = false;
                foreach (var domain in allowedDomains)
                {
                    if (uri.Host.EndsWith(domain, StringComparison.OrdinalIgnoreCase))
                    {
                        domainAllowed = true;
                        break;
                    }
                }

                if (!domainAllowed)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Extracts image data from a base64 data URL.
        /// </summary>
        /// <param name="dataUrl">The data URL containing the image</param>
        /// <param name="mimeType">Output parameter to receive the detected MIME type</param>
        /// <returns>The image data as a byte array, or null if the URL is not a valid data URL</returns>
        public static byte[]? ExtractImageDataFromDataUrl(string dataUrl, out string? mimeType)
        {
            mimeType = null;

            if (!dataUrl.StartsWith("data:"))
                return null;

            int mimeTypeStart = dataUrl.IndexOf(':') + 1;
            int mimeTypeEnd = dataUrl.IndexOf(';', mimeTypeStart);

            if (mimeTypeEnd < 0)
                return null;

            mimeType = dataUrl.Substring(mimeTypeStart, mimeTypeEnd - mimeTypeStart);

            if (!mimeType.StartsWith("image/"))
                return null;

            if (!dataUrl.Substring(mimeTypeEnd + 1).StartsWith("base64,"))
                return null;

            int dataStart = dataUrl.IndexOf("base64,") + 7;
            string base64Data = dataUrl.Substring(dataStart);

            try
            {
                return Convert.FromBase64String(base64Data);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// <summary>
        /// Downloads an image from a URL asynchronously.
        /// </summary>
        /// <param name="url">The URL of the image to download</param>
        /// <returns>The image data as a byte array</returns>
        public static async Task<byte[]> DownloadImageAsync(string url)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("URL cannot be null or empty", nameof(url));

            if (url.StartsWith("data:"))
            {
                byte[]? imageData = ExtractImageDataFromDataUrl(url, out _);
                if (imageData == null)
                    throw new ArgumentException("Invalid data URL format", nameof(url));

                return imageData;
            }

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30); // Set a reasonable timeout

            try
            {
                return await httpClient.GetByteArrayAsync(url);
            }
            catch (HttpRequestException ex)
            {
                throw new IOException($"Failed to download image from URL: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Determines if two byte arrays have the same contents.
        /// </summary>
        private static bool StartsWithSignature(byte[] data, byte[] signature)
        {
            if (data.Length < signature.Length)
                return false;

            for (int i = 0; i < signature.Length; i++)
            {
                if (signature[i] != 0 && data[i] != signature[i])
                    return false;
            }

            return true;
        }
    }
}

using System.Drawing;
using System.Net.Http;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Calculates token usage for images based on resolution using industry-standard formulas.
    /// Follows OpenAI's vision pricing model for accurate token estimation.
    /// </summary>
    public class ImageTokenCalculator : IImageTokenCalculator
    {
        private readonly ILogger<ImageTokenCalculator> _logger;
        private readonly HttpClient _httpClient;
        
        /// <summary>
        /// Token count for low detail images (fixed value)
        /// </summary>
        public const int LowDetailTokens = 85;
        
        /// <summary>
        /// Base token count for high detail images
        /// </summary>
        public const int HighDetailBaseTokens = 170;
        
        /// <summary>
        /// Token count per 512x512 tile for high detail images
        /// </summary>
        public const int TokensPerTile = 170;
        
        /// <summary>
        /// Maximum dimension for image scaling (OpenAI standard)
        /// </summary>
        public const int MaxDimension = 2048;
        
        /// <summary>
        /// Maximum short side dimension after scaling (OpenAI standard)
        /// </summary>
        public const int MaxShortSide = 768;
        
        /// <summary>
        /// Tile size for calculating tokens (512x512 pixels)
        /// </summary>
        public const int TileSize = 512;

        public ImageTokenCalculator(ILogger<ImageTokenCalculator> logger, HttpClient httpClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// Calculates the token count for an image based on its URL and detail level
        /// </summary>
        /// <param name="imageUrl">The ImageUrl object containing the image URL and detail level</param>
        /// <returns>The estimated token count for the image</returns>
        public async Task<int> CalculateImageTokensAsync(ImageUrl imageUrl)
        {
            if (imageUrl == null)
                throw new ArgumentNullException(nameof(imageUrl));

            var detail = imageUrl.Detail?.ToLower() ?? "high"; // Default to high detail
            
            // Low detail mode always returns fixed token count
            if (detail == "low")
            {
                _logger.LogDebug("Image uses low detail mode, returning {Tokens} tokens", LowDetailTokens);
                return LowDetailTokens;
            }

            try
            {
                // Get image dimensions
                var (width, height) = await GetImageDimensionsAsync(imageUrl);
                
                if (width == 0 || height == 0)
                {
                    _logger.LogWarning("Could not determine image dimensions, using conservative estimate");
                    return EstimateConservativeTokens();
                }

                // Calculate tokens based on high detail formula
                return CalculateHighDetailTokens(width, height);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate image tokens, using conservative estimate");
                return EstimateConservativeTokens();
            }
        }

        /// <summary>
        /// Gets the dimensions of an image from its URL (either base64 or HTTP URL)
        /// </summary>
        private async Task<(int width, int height)> GetImageDimensionsAsync(ImageUrl imageUrl)
        {
            if (imageUrl.IsBase64DataUrl)
            {
                return GetBase64ImageDimensions(imageUrl.Base64Data!);
            }
            else
            {
                return await GetRemoteImageDimensionsAsync(imageUrl.Url);
            }
        }

        /// <summary>
        /// Gets dimensions from a base64 encoded image
        /// </summary>
        private (int width, int height) GetBase64ImageDimensions(string base64Data)
        {
            try
            {
                var imageBytes = Convert.FromBase64String(base64Data);
                return GetImageDimensionsFromBytes(imageBytes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decode base64 image for dimension extraction");
                return (0, 0);
            }
        }

        /// <summary>
        /// Gets dimensions from a remote image URL
        /// </summary>
        private async Task<(int width, int height)> GetRemoteImageDimensionsAsync(string url)
        {
            try
            {
                // First try to get dimensions from headers (if server supports it)
                using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
                using var headResponse = await _httpClient.SendAsync(headRequest);

                if (headResponse.Headers.TryGetValues("X-Image-Width", out var widthValues) &&
                    headResponse.Headers.TryGetValues("X-Image-Height", out var heightValues))
                {
                    if (int.TryParse(widthValues.FirstOrDefault(), out var width) &&
                        int.TryParse(heightValues.FirstOrDefault(), out var height))
                    {
                        return (width, height);
                    }
                }

                // If headers don't contain dimensions, download the image
                // We only need the first few bytes to determine dimensions for most formats
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                using var stream = await response.Content.ReadAsStreamAsync();
                
                // Read enough bytes to get image dimensions (usually in the header)
                var buffer = new byte[4096]; // Should be enough for most image headers
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                
                if (bytesRead > 0)
                {
                    var trimmedBuffer = new byte[bytesRead];
                    Array.Copy(buffer, trimmedBuffer, bytesRead);
                    return GetImageDimensionsFromBytes(trimmedBuffer);
                }

                return (0, 0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get dimensions from remote image URL: {Url}", url);
                return (0, 0);
            }
        }

        /// <summary>
        /// Extracts image dimensions from byte array by reading image headers
        /// </summary>
        private (int width, int height) GetImageDimensionsFromBytes(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length < 8)
                return (0, 0);

            // JPEG
            if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8)
            {
                return GetJpegDimensions(imageBytes);
            }
            // PNG
            else if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && 
                     imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
            {
                return GetPngDimensions(imageBytes);
            }
            // GIF
            else if (imageBytes[0] == 0x47 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46)
            {
                return GetGifDimensions(imageBytes);
            }
            // WebP
            else if (imageBytes.Length >= 12 && 
                     imageBytes[0] == 0x52 && imageBytes[1] == 0x49 && 
                     imageBytes[2] == 0x46 && imageBytes[3] == 0x46 &&
                     imageBytes[8] == 0x57 && imageBytes[9] == 0x45 && 
                     imageBytes[10] == 0x42 && imageBytes[11] == 0x50)
            {
                return GetWebPDimensions(imageBytes);
            }

            return (0, 0);
        }

        /// <summary>
        /// Gets dimensions from JPEG bytes
        /// </summary>
        private (int width, int height) GetJpegDimensions(byte[] bytes)
        {
            try
            {
                int offset = 2; // Skip SOI marker
                while (offset < bytes.Length - 9)
                {
                    if (bytes[offset] != 0xFF) break;
                    
                    byte marker = bytes[offset + 1];
                    offset += 2;
                    
                    // SOF markers (Start of Frame) contain dimensions
                    if ((marker >= 0xC0 && marker <= 0xCF) && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                    {
                        if (offset + 7 < bytes.Length)
                        {
                            int height = (bytes[offset + 3] << 8) | bytes[offset + 4];
                            int width = (bytes[offset + 5] << 8) | bytes[offset + 6];
                            return (width, height);
                        }
                    }
                    
                    if (offset + 2 > bytes.Length) break;
                    int segmentLength = (bytes[offset] << 8) | bytes[offset + 1];
                    offset += segmentLength;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse JPEG dimensions from image bytes");
            }

            return (0, 0);
        }

        /// <summary>
        /// Gets dimensions from PNG bytes
        /// </summary>
        private (int width, int height) GetPngDimensions(byte[] bytes)
        {
            if (bytes.Length < 24) return (0, 0);
            
            try
            {
                // PNG dimensions are in the IHDR chunk which starts at byte 16
                int width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
                int height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
                return (width, height);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse PNG dimensions from image bytes");
            }

            return (0, 0);
        }

        /// <summary>
        /// Gets dimensions from GIF bytes
        /// </summary>
        private (int width, int height) GetGifDimensions(byte[] bytes)
        {
            if (bytes.Length < 10) return (0, 0);
            
            try
            {
                int width = bytes[6] | (bytes[7] << 8);
                int height = bytes[8] | (bytes[9] << 8);
                return (width, height);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse GIF dimensions from image bytes");
            }

            return (0, 0);
        }

        /// <summary>
        /// Gets dimensions from WebP bytes
        /// </summary>
        private (int width, int height) GetWebPDimensions(byte[] bytes)
        {
            if (bytes.Length < 30) return (0, 0);
            
            try
            {
                // Check for VP8 format
                if (bytes[12] == 0x56 && bytes[13] == 0x50 && bytes[14] == 0x38)
                {
                    if (bytes[15] == 0x20) // VP8 (lossy)
                    {
                        int width = ((bytes[26] | (bytes[27] << 8)) & 0x3FFF) + 1;
                        int height = ((bytes[28] | (bytes[29] << 8)) & 0x3FFF) + 1;
                        return (width, height);
                    }
                    else if (bytes[15] == 0x4C) // VP8L (lossless)
                    {
                        if (bytes.Length >= 25)
                        {
                            int bits = bytes[21] | (bytes[22] << 8) | (bytes[23] << 16) | (bytes[24] << 24);
                            int width = ((bits & 0x3FFF) + 1);
                            int height = (((bits >> 14) & 0x3FFF) + 1);
                            return (width, height);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse WebP dimensions from image bytes");
            }

            return (0, 0);
        }

        /// <summary>
        /// Calculates tokens for high detail images using OpenAI's formula
        /// </summary>
        private int CalculateHighDetailTokens(int width, int height)
        {
            // First, scale the image to fit within the maximum dimensions
            var (scaledWidth, scaledHeight) = ScaleImageDimensions(width, height);
            
            // Calculate the number of 512x512 tiles
            int tilesWide = (int)Math.Ceiling((double)scaledWidth / TileSize);
            int tilesHigh = (int)Math.Ceiling((double)scaledHeight / TileSize);
            int totalTiles = tilesWide * tilesHigh;
            
            // Calculate total tokens: base + (tiles * tokens_per_tile)
            int totalTokens = HighDetailBaseTokens + (totalTiles * TokensPerTile);
            
            _logger.LogDebug(
                "Image {Width}x{Height} scaled to {ScaledWidth}x{ScaledHeight}, " +
                "tiles: {TilesWide}x{TilesHigh}={TotalTiles}, tokens: {Tokens}",
                width, height, scaledWidth, scaledHeight, 
                tilesWide, tilesHigh, totalTiles, totalTokens);
            
            return totalTokens;
        }

        /// <summary>
        /// Scales image dimensions according to OpenAI's vision model scaling rules
        /// </summary>
        private (int width, int height) ScaleImageDimensions(int originalWidth, int originalHeight)
        {
            // First, scale down if any dimension exceeds the maximum
            if (originalWidth > MaxDimension || originalHeight > MaxDimension)
            {
                double scale = Math.Min(
                    (double)MaxDimension / originalWidth,
                    (double)MaxDimension / originalHeight
                );
                originalWidth = (int)(originalWidth * scale);
                originalHeight = (int)(originalHeight * scale);
            }
            
            // Then, scale down further if the shortest side is still larger than the limit
            int shortestSide = Math.Min(originalWidth, originalHeight);
            if (shortestSide > MaxShortSide)
            {
                double scale = (double)MaxShortSide / shortestSide;
                originalWidth = (int)(originalWidth * scale);
                originalHeight = (int)(originalHeight * scale);
            }
            
            return (originalWidth, originalHeight);
        }

        /// <summary>
        /// Returns a conservative token estimate when dimensions cannot be determined
        /// </summary>
        private int EstimateConservativeTokens()
        {
            // Assume a 1024x1024 image (common size) for conservative estimation
            // This gives us 4 tiles (2x2) = 170 + (4 * 170) = 850 tokens
            const int conservativeTokens = 850;
            _logger.LogInformation("Using conservative image token estimate: {Tokens} tokens", conservativeTokens);
            return conservativeTokens;
        }
    }
}
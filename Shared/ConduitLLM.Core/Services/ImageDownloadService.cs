using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Utilities;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services;

/// <summary>
/// Service for downloading images from external URLs using IHttpClientFactory for proper connection management.
/// This service prevents socket exhaustion under high load by using pooled HTTP connections.
/// </summary>
public class ImageDownloadService : IImageDownloadService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImageDownloadService> _logger;

    /// <summary>
    /// The name of the named HttpClient used for external image fetching.
    /// </summary>
    public const string HttpClientName = "ExternalImageFetch";

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageDownloadService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory for creating managed HttpClient instances.</param>
    /// <param name="logger">The logger for diagnostic output.</param>
    public ImageDownloadService(IHttpClientFactory httpClientFactory, ILogger<ImageDownloadService> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<byte[]> DownloadImageAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentException("URL cannot be null or empty", nameof(url));
        }

        // Handle data URLs directly
        if (url.StartsWith("data:"))
        {
            byte[]? imageData = ImageUtility.ExtractImageDataFromDataUrl(url, out _);
            if (imageData == null)
            {
                throw new ArgumentException("Invalid data URL format", nameof(url));
            }
            return imageData;
        }

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        try
        {
            _logger.LogDebug("Downloading image from {Url}", url);
            return await httpClient.GetByteArrayAsync(url, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to download image from {Url}", url);
            throw new IOException($"Failed to download image from URL: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<ImageUrl> DownloadAsImageUrlAsync(string url, string? detail = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentException("URL cannot be null or empty", nameof(url));
        }

        // If already a data URL, just return it wrapped
        if (url.StartsWith("data:"))
        {
            return new ImageUrl { Url = url, Detail = detail };
        }

        byte[] imageBytes = await DownloadImageAsync(url, cancellationToken);

        // Detect MIME type from image bytes
        string mimeType = DetectMimeType(imageBytes);

        string dataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(imageBytes)}";

        return new ImageUrl
        {
            Url = dataUrl,
            Detail = detail
        };
    }

    /// <summary>
    /// Detects the MIME type from image bytes by examining magic numbers.
    /// </summary>
    /// <param name="imageBytes">The image data bytes.</param>
    /// <returns>The detected MIME type, or "image/jpeg" as fallback.</returns>
    private static string DetectMimeType(byte[] imageBytes)
    {
        // Use ImageUtility's detection if available, otherwise fall back to inline detection
        string? detectedType = ImageUtility.DetectMimeType(imageBytes);
        if (detectedType != null)
        {
            return detectedType;
        }

        // Fallback detection using magic numbers
        if (imageBytes.Length >= 2)
        {
            if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8)
                return "image/jpeg";

            if (imageBytes.Length >= 8 &&
                imageBytes[0] == 0x89 && imageBytes[1] == 0x50 &&
                imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
                return "image/png";

            if (imageBytes.Length >= 3 &&
                imageBytes[0] == 0x47 && imageBytes[1] == 0x49 &&
                imageBytes[2] == 0x46)
                return "image/gif";

            if (imageBytes[0] == 0x42 && imageBytes[1] == 0x4D)
                return "image/bmp";
        }

        return "image/jpeg"; // Default fallback
    }
}

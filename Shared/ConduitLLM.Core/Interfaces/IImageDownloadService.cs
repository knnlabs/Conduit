using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces;

/// <summary>
/// Service for downloading images from external URLs using properly managed HTTP connections.
/// This service uses IHttpClientFactory to avoid socket exhaustion under high load.
/// </summary>
public interface IImageDownloadService
{
    /// <summary>
    /// Downloads an image from the specified URL.
    /// </summary>
    /// <param name="url">The URL of the image to download.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The image data as a byte array.</returns>
    /// <exception cref="ArgumentException">Thrown when the URL is null or empty.</exception>
    /// <exception cref="IOException">Thrown when the image download fails.</exception>
    Task<byte[]> DownloadImageAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads an image from the specified URL and converts it to an ImageUrl with base64 data URL.
    /// </summary>
    /// <param name="url">The URL of the image to download.</param>
    /// <param name="detail">Optional detail level for vision models (e.g., "low", "high", "auto").</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An ImageUrl object with the image as a base64 data URL.</returns>
    /// <exception cref="ArgumentException">Thrown when the URL is null or empty.</exception>
    /// <exception cref="IOException">Thrown when the image download fails.</exception>
    Task<ImageUrl> DownloadAsImageUrlAsync(string url, string? detail = null, CancellationToken cancellationToken = default);
}

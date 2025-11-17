using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Provides storage and retrieval services for media files including images, audio, and video.
    /// </summary>
    public interface IMediaStorageService
    {
        /// <summary>
        /// Stores media content and returns storage information.
        /// </summary>
        /// <param name="content">The media content stream.</param>
        /// <param name="metadata">Metadata about the media content.</param>
        /// <param name="progress">Optional progress reporter for upload tracking.</param>
        /// <returns>Storage result containing the storage key and URL.</returns>
        Task<MediaStorageResult> StoreAsync(Stream content, MediaMetadata metadata, IProgress<long>? progress = null);

        /// <summary>
        /// Retrieves a media file stream by its storage key.
        /// </summary>
        /// <param name="storageKey">The unique storage key.</param>
        /// <returns>The media content stream or null if not found.</returns>
        Task<Stream?> GetStreamAsync(string storageKey);

        /// <summary>
        /// Gets metadata information about a stored media file.
        /// </summary>
        /// <param name="storageKey">The unique storage key.</param>
        /// <returns>Media information or null if not found.</returns>
        Task<MediaInfo?> GetInfoAsync(string storageKey);

        /// <summary>
        /// Deletes a media file from storage.
        /// </summary>
        /// <param name="storageKey">The unique storage key.</param>
        /// <returns>True if deletion was successful, false otherwise.</returns>
        Task<bool> DeleteAsync(string storageKey);

        /// <summary>
        /// Generates a URL for accessing the media file.
        /// </summary>
        /// <param name="storageKey">The unique storage key.</param>
        /// <param name="expiration">Optional expiration time for the URL.</param>
        /// <returns>The access URL.</returns>
        Task<string> GenerateUrlAsync(string storageKey, TimeSpan? expiration = null);

        /// <summary>
        /// Checks if a media file exists in storage.
        /// </summary>
        /// <param name="storageKey">The unique storage key.</param>
        /// <returns>True if the file exists, false otherwise.</returns>
        Task<bool> ExistsAsync(string storageKey);

        /// <summary>
        /// Stores video content with support for large files and progress tracking.
        /// </summary>
        /// <param name="content">The video content stream.</param>
        /// <param name="metadata">Video metadata including resolution, duration, etc.</param>
        /// <param name="progressCallback">Optional callback for upload progress.</param>
        /// <returns>Storage result containing the storage key and URL.</returns>
        Task<MediaStorageResult> StoreVideoAsync(
            Stream content, 
            VideoMediaMetadata metadata,
            Action<long>? progressCallback = null);

        /// <summary>
        /// Initiates a multipart upload for large video files.
        /// </summary>
        /// <param name="metadata">Video metadata.</param>
        /// <returns>Upload session information.</returns>
        Task<MultipartUploadSession> InitiateMultipartUploadAsync(VideoMediaMetadata metadata);

        /// <summary>
        /// Uploads a part of a multipart video upload.
        /// </summary>
        /// <param name="sessionId">The multipart upload session ID.</param>
        /// <param name="partNumber">The part number (1-based).</param>
        /// <param name="content">The content for this part.</param>
        /// <returns>Part upload result with ETag.</returns>
        Task<PartUploadResult> UploadPartAsync(string sessionId, int partNumber, Stream content);

        /// <summary>
        /// Completes a multipart upload and returns the final storage result.
        /// </summary>
        /// <param name="sessionId">The multipart upload session ID.</param>
        /// <param name="parts">List of completed parts with their ETags.</param>
        /// <returns>Final storage result.</returns>
        Task<MediaStorageResult> CompleteMultipartUploadAsync(string sessionId, List<PartUploadResult> parts);

        /// <summary>
        /// Aborts a multipart upload session.
        /// </summary>
        /// <param name="sessionId">The multipart upload session ID to abort.</param>
        Task AbortMultipartUploadAsync(string sessionId);

        /// <summary>
        /// Gets a stream for video content with support for HTTP range requests.
        /// </summary>
        /// <param name="storageKey">The unique storage key.</param>
        /// <param name="rangeStart">Optional byte range start.</param>
        /// <param name="rangeEnd">Optional byte range end.</param>
        /// <returns>The video stream for the requested range.</returns>
        Task<RangedStream?> GetVideoStreamAsync(string storageKey, long? rangeStart = null, long? rangeEnd = null);

        /// <summary>
        /// Generates a presigned URL for direct video upload to storage.
        /// </summary>
        /// <param name="metadata">Video metadata.</param>
        /// <param name="expiration">How long the presigned URL should be valid.</param>
        /// <returns>Presigned URL information for direct upload.</returns>
        Task<PresignedUploadUrl> GeneratePresignedUploadUrlAsync(VideoMediaMetadata metadata, TimeSpan expiration);
    }
}
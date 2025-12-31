namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for retrieving and downloading generated content files.
    /// </summary>
    public interface IFileRetrievalService
    {
        /// <summary>
        /// Retrieves a file as a stream.
        /// </summary>
        /// <param name="fileIdentifier">The file identifier (storage key or URL).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A file retrieval result containing the stream and metadata.</returns>
        Task<FileRetrievalResult?> RetrieveFileAsync(string fileIdentifier, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads a file to a local path.
        /// </summary>
        /// <param name="fileIdentifier">The file identifier (storage key or URL).</param>
        /// <param name="destinationPath">The local path to save the file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> DownloadFileAsync(string fileIdentifier, string destinationPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a temporary download URL for a file.
        /// </summary>
        /// <param name="fileIdentifier">The file identifier (storage key or URL).</param>
        /// <param name="expiration">How long the URL should be valid.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A temporary download URL.</returns>
        Task<string?> GetDownloadUrlAsync(string fileIdentifier, TimeSpan expiration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves file metadata without downloading the content.
        /// </summary>
        /// <param name="fileIdentifier">The file identifier (storage key or URL).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>File metadata.</returns>
        Task<FileMetadata?> GetFileMetadataAsync(string fileIdentifier, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a file exists and is accessible.
        /// </summary>
        /// <param name="fileIdentifier">The file identifier (storage key or URL).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the file exists and is accessible.</returns>
        Task<bool> FileExistsAsync(string fileIdentifier, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result of a file retrieval operation.
    /// </summary>
    public class FileRetrievalResult : IDisposable
    {
        /// <summary>
        /// The file content stream.
        /// </summary>
        public required Stream ContentStream { get; set; }

        /// <summary>
        /// The file metadata.
        /// </summary>
        public required FileMetadata Metadata { get; set; }

        /// <summary>
        /// Disposes the content stream.
        /// </summary>
        public void Dispose()
        {
            ContentStream?.Dispose();
        }
    }

    /// <summary>
    /// Metadata about a retrieved file.
    /// </summary>
    public class FileMetadata
    {
        /// <summary>
        /// The original file name.
        /// </summary>
        public string? FileName { get; set; }

        /// <summary>
        /// The MIME content type.
        /// </summary>
        public required string ContentType { get; set; }

        /// <summary>
        /// The file size in bytes.
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// When the file was created.
        /// </summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>
        /// When the file was last modified.
        /// </summary>
        public DateTime? ModifiedAt { get; set; }

        /// <summary>
        /// The storage provider (e.g., "s3", "local", "url").
        /// </summary>
        public string? StorageProvider { get; set; }

        /// <summary>
        /// Additional metadata key-value pairs.
        /// </summary>
        public Dictionary<string, string>? AdditionalMetadata { get; set; }

        /// <summary>
        /// ETag for caching.
        /// </summary>
        public string? ETag { get; set; }

        /// <summary>
        /// Whether the file supports range requests.
        /// </summary>
        public bool SupportsRangeRequests { get; set; } = true;
    }
}
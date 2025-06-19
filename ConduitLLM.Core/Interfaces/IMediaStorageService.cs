using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
        /// <returns>Storage result containing the storage key and URL.</returns>
        Task<MediaStorageResult> StoreAsync(Stream content, MediaMetadata metadata);

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
    }
}
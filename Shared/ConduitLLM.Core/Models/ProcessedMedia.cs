using System.Collections.Generic;

namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Represents processed media after generation and storage.
    /// </summary>
    public class ProcessedMedia
    {
        /// <summary>
        /// Gets or sets the primary URL for single media items.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// Gets or sets the storage key for the media.
        /// </summary>
        public string? StorageKey { get; set; }

        /// <summary>
        /// Gets or sets the collection of processed media items (for batch processing).
        /// </summary>
        public IList<ProcessedMediaItem> Items { get; set; } = new List<ProcessedMediaItem>();

        /// <summary>
        /// Gets or sets the total cost for generating this media.
        /// </summary>
        public decimal Cost { get; set; }

        /// <summary>
        /// Gets or sets additional metadata about the processed media.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets the count of processed items.
        /// </summary>
        public int Count => Items?.Count ?? (string.IsNullOrEmpty(Url) ? 0 : 1);
    }

    /// <summary>
    /// Represents a single processed media item in a batch.
    /// </summary>
    public class ProcessedMediaItem
    {
        /// <summary>
        /// Gets or sets the URL of the processed media.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the storage key.
        /// </summary>
        public string? StorageKey { get; set; }

        /// <summary>
        /// Gets or sets the index in the batch.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets item-specific metadata.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}
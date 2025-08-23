namespace ConduitLLM.Core.Models.Audio
{
    /// <summary>
    /// Result of content filtering operation.
    /// </summary>
    public class ContentFilterResult
    {
        /// <summary>
        /// Gets or sets whether the content passed all filters.
        /// </summary>
        public bool IsApproved { get; set; }

        /// <summary>
        /// Gets or sets the filtered/cleaned text.
        /// </summary>
        public string FilteredText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the categories of issues found.
        /// </summary>
        public List<string> ViolationCategories { get; set; } = new();

        /// <summary>
        /// Gets or sets the confidence score (0-1).
        /// </summary>
        public double ConfidenceScore { get; set; }

        /// <summary>
        /// Gets or sets whether content was modified.
        /// </summary>
        public bool WasModified { get; set; }

        /// <summary>
        /// Gets or sets detailed reasons for filtering.
        /// </summary>
        public List<ContentFilterDetail> Details { get; set; } = new();
    }

    /// <summary>
    /// Detailed information about filtered content.
    /// </summary>
    public class ContentFilterDetail
    {
        /// <summary>
        /// Gets or sets the type of content filtered.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the severity level.
        /// </summary>
        public FilterSeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets the original text segment.
        /// </summary>
        public string OriginalText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the replacement text.
        /// </summary>
        public string ReplacementText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the start position in text.
        /// </summary>
        public int StartIndex { get; set; }

        /// <summary>
        /// Gets or sets the end position in text.
        /// </summary>
        public int EndIndex { get; set; }
    }

    /// <summary>
    /// Severity levels for content filtering.
    /// </summary>
    public enum FilterSeverity
    {
        /// <summary>
        /// Low severity - minor issues.
        /// </summary>
        Low,

        /// <summary>
        /// Medium severity - moderate issues.
        /// </summary>
        Medium,

        /// <summary>
        /// High severity - serious issues.
        /// </summary>
        High,

        /// <summary>
        /// Critical severity - must be blocked.
        /// </summary>
        Critical
    }
}

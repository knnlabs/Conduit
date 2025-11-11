namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Represents the result of a bulk import operation
    /// </summary>
    public class BulkImportResult
    {
        /// <summary>
        /// Gets or sets the number of successfully imported items
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Gets or sets the number of items that failed to import
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Gets or sets the list of error messages for failed imports
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();
    }
}
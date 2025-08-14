namespace ConduitLLM.Configuration.DTOs.Cache
{
    /// <summary>
    /// Data transfer object for top cached item information
    /// </summary>
    public class TopCachedItemDto
    {
        /// <summary>
        /// Cache key or pattern
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Number of hits
        /// </summary>
        public long Hits { get; set; }

        /// <summary>
        /// Item size (formatted string)
        /// </summary>
        public string Size { get; set; } = string.Empty;
    }
}
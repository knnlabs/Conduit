namespace ConduitLLM.Configuration.DTOs.Cache
{
    /// <summary>
    /// Data transfer object for memory usage information
    /// </summary>
    public class MemoryUsageDto
    {
        /// <summary>
        /// Current memory usage (formatted string)
        /// </summary>
        public string Current { get; set; } = string.Empty;

        /// <summary>
        /// Peak memory usage (formatted string)
        /// </summary>
        public string Peak { get; set; } = string.Empty;

        /// <summary>
        /// Memory limit (formatted string)
        /// </summary>
        public string Limit { get; set; } = string.Empty;
    }
}
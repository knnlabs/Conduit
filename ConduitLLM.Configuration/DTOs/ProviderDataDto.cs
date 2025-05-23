namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Simple data transfer object for provider information
    /// </summary>
    public class ProviderDataDto
    {
        /// <summary>
        /// Provider ID
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Provider name
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;
    }
}
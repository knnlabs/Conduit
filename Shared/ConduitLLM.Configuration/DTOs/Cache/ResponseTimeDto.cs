namespace ConduitLLM.Configuration.DTOs.Cache
{
    /// <summary>
    /// Data transfer object for response time metrics
    /// </summary>
    public class ResponseTimeDto
    {
        /// <summary>
        /// Average response time with cache hit (ms)
        /// </summary>
        public int WithCache { get; set; }

        /// <summary>
        /// Average response time without cache (ms)
        /// </summary>
        public int WithoutCache { get; set; }
    }
}
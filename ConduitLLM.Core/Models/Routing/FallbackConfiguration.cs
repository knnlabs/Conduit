namespace ConduitLLM.Core.Models.Routing
{
    /// <summary>
    /// Configuration for model fallback strategies
    /// </summary>
    public class FallbackConfiguration
    {
        /// <summary>
        /// Unique identifier for this fallback configuration
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The ID of the primary model deployment that will fall back to others if it fails
        /// </summary>
        public string PrimaryModelDeploymentId { get; set; } = string.Empty;

        /// <summary>
        /// Ordered list of model deployment IDs to use as fallbacks (in priority order)
        /// </summary>
        public List<string> FallbackModelDeploymentIds { get; set; } = new();
    }
}

namespace ConduitLLM.Core.Configuration
{
    /// <summary>
    /// Options for context window management in LLM requests.
    /// </summary>
    public class ContextManagementOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether automatic context window management is enabled.
        /// When enabled, the system will automatically trim conversation history to fit within model context limits.
        /// </summary>
        public bool EnableAutomaticContextManagement { get; set; } = true;
    }
}

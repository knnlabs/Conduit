using System;

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

        /// <summary>
        /// Gets or sets the default maximum context window size in tokens.
        /// This is used as a fallback when a model-specific limit is not configured.
        /// </summary>
        public int? DefaultMaxContextTokens { get; set; } = 4000;
    }
}

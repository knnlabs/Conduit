using System.Collections.Generic;

namespace ConduitLLM.Configuration.Options
{
    /// <summary>
    /// Configuration options for the router service
    /// </summary>
    public class RouterOptions
    {
        /// <summary>
        /// Section name for configuration
        /// </summary>
        public const string SectionName = "Router";

        /// <summary>
        /// Whether the router is enabled
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// The default routing strategy to use
        /// </summary>
        public string DefaultRoutingStrategy { get; set; } = "Simple";

        /// <summary>
        /// Maximum number of retries for a failed request
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Base delay in milliseconds between retries (for exponential backoff)
        /// </summary>
        public int RetryBaseDelayMs { get; set; } = 500;

        /// <summary>
        /// Maximum delay in milliseconds between retries
        /// </summary>
        public int RetryMaxDelayMs { get; set; } = 10000;

        /// <summary>
        /// List of model deployments available to the router
        /// </summary>
        public List<RouterModelDeployment> ModelDeployments { get; set; } = new();

        /// <summary>
        /// List of fallback configurations in the format "primary_model:fallback_model1,fallback_model2"
        /// </summary>
        public List<string> FallbackRules { get; set; } = new();
    }

    /// <summary>
    /// Configuration for a model deployment in the router
    /// </summary>
    public class RouterModelDeployment
    {
        /// <summary>
        /// Unique name for this model deployment
        /// </summary>
        public string DeploymentName { get; set; } = string.Empty;

        /// <summary>
        /// The model alias this deployment refers to
        /// </summary>
        public string ModelAlias { get; set; } = string.Empty;

        /// <summary>
        /// Maximum requests per minute for this deployment
        /// </summary>
        public int? RPM { get; set; }

        /// <summary>
        /// Maximum tokens per minute for this deployment
        /// </summary>
        public int? TPM { get; set; }

        /// <summary>
        /// Cost per 1000 input tokens
        /// </summary>
        public decimal? InputTokenCostPer1K { get; set; }

        /// <summary>
        /// Cost per 1000 output tokens
        /// </summary>
        public decimal? OutputTokenCostPer1K { get; set; }

        /// <summary>
        /// Priority of this deployment (lower values are higher priority)
        /// </summary>
        public int Priority { get; set; } = 1;
    }
}

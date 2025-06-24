using System;
using System.Collections.Generic;

namespace ConduitLLM.Core.Models.Routing
{
    /// <summary>
    /// Configuration for the LLM Router
    /// </summary>
    public class RouterConfig
    {
        /// <summary>
        /// List of model deployments available to the router
        /// </summary>
        public List<ModelDeployment> ModelDeployments { get; set; } = new();

        /// <summary>
        /// Default routing strategy to use when not explicitly specified
        /// </summary>
        public string DefaultRoutingStrategy { get; set; } = "simple";

        /// <summary>
        /// Dictionary of fallback configurations where keys are model names and values are lists of fallback models
        /// </summary>
        public Dictionary<string, List<string>> Fallbacks { get; set; } = new();

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
        /// Whether fallbacks are enabled
        /// </summary>
        public bool FallbacksEnabled { get; set; } = false;

        /// <summary>
        /// List of fallback configurations between models
        /// </summary>
        public List<FallbackConfiguration> FallbackConfigurations { get; set; } = new();
    }

    /// <summary>
    /// Represents a model deployment that can be used by the router
    /// </summary>
    public class ModelDeployment
    {
        /// <summary>
        /// Unique identifier for this model deployment
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The name of the model (e.g., gpt-4, claude-3-opus)
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// The name of the provider for this model (e.g., OpenAI, Anthropic)
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Unique name for this model deployment - compatibility property
        /// </summary>
        public string DeploymentName
        {
            get => ModelName;
            set => ModelName = value;
        }

        /// <summary>
        /// The model alias this deployment refers to - compatibility property
        /// </summary>
        public string ModelAlias
        {
            get => ProviderName;
            set => ProviderName = value;
        }

        /// <summary>
        /// Weight for random selection strategy (higher values increase selection probability)
        /// </summary>
        public int Weight { get; set; } = 1;

        /// <summary>
        /// Whether health checking is enabled for this deployment
        /// </summary>
        public bool HealthCheckEnabled { get; set; } = true;

        /// <summary>
        /// Whether this deployment is enabled and available for routing
        /// </summary>
        public bool IsEnabled { get; set; } = true;

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

        /// <summary>
        /// Health status of this deployment
        /// </summary>
        public bool IsHealthy { get; set; } = true;

        /// <summary>
        /// Last time this deployment was used
        /// </summary>
        public DateTime LastUsed { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Number of requests made to this deployment
        /// </summary>
        public int RequestCount { get; set; } = 0;

        /// <summary>
        /// Average latency in milliseconds
        /// </summary>
        public double AverageLatencyMs { get; set; } = 0;

        /// <summary>
        /// Whether this deployment supports embedding operations
        /// </summary>
        public bool SupportsEmbeddings { get; set; } = false;
    }

    /// <summary>
    /// Strategy to use when routing requests to models
    /// </summary>
    public enum RoutingStrategy
    {
        /// <summary>
        /// Use the first available model in the list
        /// </summary>
        Simple,

        /// <summary>
        /// Distribute requests evenly across all available models
        /// </summary>
        RoundRobin,

        /// <summary>
        /// Use the model with the lowest cost
        /// </summary>
        LeastCost,

        /// <summary>
        /// Use the model with the lowest latency
        /// </summary>
        LeastLatency,

        /// <summary>
        /// Use the model with the highest priority (lowest priority value)
        /// </summary>
        HighestPriority
    }
}

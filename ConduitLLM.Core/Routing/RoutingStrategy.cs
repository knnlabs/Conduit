using System;

namespace ConduitLLM.Core.Routing
{
    /// <summary>
    /// Defines the available routing strategies for model selection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The routing strategy determines how models are selected for processing requests
    /// when multiple models are available. Each strategy optimizes for different priorities
    /// such as cost, performance, or load balancing.
    /// </para>
    /// </remarks>
    public enum RoutingStrategy
    {
        /// <summary>
        /// Simple strategy that selects the first available healthy model.
        /// This is the default strategy and is suitable for most use cases.
        /// </summary>
        Simple,

        /// <summary>
        /// Selects the model with the lowest token costs.
        /// This strategy optimizes for minimizing costs when multiple models
        /// with different price points are available.
        /// </summary>
        LeastCost,
        
        /// <summary>
        /// Round-robin strategy that distributes requests evenly across all available models.
        /// This strategy is useful for load balancing across multiple models.
        /// </summary>
        RoundRobin,

        /// <summary>
        /// Selects the model with the lowest observed latency.
        /// This strategy optimizes for performance when responsiveness is critical.
        /// </summary>
        LeastLatency,

        /// <summary>
        /// Selects models based on a pre-defined priority order.
        /// Models with lower priority values are selected first.
        /// </summary>
        HighestPriority,
        
        /// <summary>
        /// Selects a random model from the available options.
        /// This strategy provides simple load balancing without tracking model usage.
        /// </summary>
        Random,

        /// <summary>
        /// Selects the model that has been used the least.
        /// This strategy provides load balancing by tracking the usage count of each model.
        /// </summary>
        LeastUsed,

        /// <summary>
        /// Passes through the request to the specified model without selection logic.
        /// This strategy is useful when the client wants to control model selection.
        /// </summary>
        Passthrough
    }
}
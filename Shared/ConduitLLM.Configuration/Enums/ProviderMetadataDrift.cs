namespace ConduitLLM.Configuration.Enums
{
    /// <summary>
    /// The kind of drift detected between a provider's published model metadata and Conduit's
    /// configured values for a model mapping.
    /// </summary>
    public enum DriftType
    {
        /// <summary>Provider pricing differs from the configured ModelCost.</summary>
        Pricing,

        /// <summary>The mapping has no ModelCost configured but the provider publishes pricing.</summary>
        MissingCost,

        /// <summary>Provider context length / max output differs from the configured limits.</summary>
        ContextWindow,

        /// <summary>Provider capabilities (vision, tools, etc.) differ from the configured flags.</summary>
        Capabilities,

        /// <summary>The model is no longer present in the provider's catalog.</summary>
        ModelRemoved,

        /// <summary>The provider has published a deprecation/expiration date for the model.</summary>
        ModelDeprecated
    }

    /// <summary>
    /// The review status of a detected drift item.
    /// </summary>
    public enum DriftStatus
    {
        /// <summary>Detected and awaiting an admin decision.</summary>
        Pending,

        /// <summary>An admin applied the proposed change.</summary>
        Applied,

        /// <summary>An admin dismissed the item.</summary>
        Dismissed,

        /// <summary>The drift resolved on its own (values now match) before an admin acted.</summary>
        AutoResolved
    }
}

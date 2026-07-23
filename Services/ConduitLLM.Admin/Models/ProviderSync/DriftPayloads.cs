namespace ConduitLLM.Admin.Models.ProviderSync
{
    /// <summary>
    /// Pricing snapshot (per-million USD, cache read/write nullable) used for Pricing and MissingCost
    /// drift. Current = Conduit's ModelCost; Proposed = OpenRouter's published pricing.
    /// </summary>
    public class PricingDriftPayload
    {
        public decimal? InputPerMillion { get; set; }
        public decimal? OutputPerMillion { get; set; }
        public decimal? CachedInputPerMillion { get; set; }
        public decimal? CachedWritePerMillion { get; set; }
    }

    /// <summary>Context-window snapshot for ContextWindow drift.</summary>
    public class ContextWindowDriftPayload
    {
        public int? MaxInputTokens { get; set; }
        public int? MaxOutputTokens { get; set; }
    }

    /// <summary>Capability snapshot for Capabilities drift.</summary>
    public class CapabilitiesDriftPayload
    {
        public IReadOnlyList<string> InputModalities { get; set; } = [];
        public IReadOnlyList<string> OutputModalities { get; set; } = [];
        public bool SupportsVision { get; set; }
        public bool SupportsFunctionCalling { get; set; }
        public bool SupportsImageGeneration { get; set; }
        public bool SupportsVideoGeneration { get; set; }
    }
}

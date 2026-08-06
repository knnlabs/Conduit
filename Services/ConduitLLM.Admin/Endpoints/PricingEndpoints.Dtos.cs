using System.Text.Json;
using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.Admin.Endpoints
{
    /// <summary>
    /// Request to validate pricing configuration
    /// </summary>
    public class PricingValidationRequest
    {
        /// <summary>
        /// The structured pricing configuration to validate.
        /// </summary>
        public Dictionary<string, JsonElement> PricingConfiguration { get; set; } = new();
    }

    /// <summary>
    /// Response from pricing validation
    /// </summary>
    public class PricingValidationResponse
    {
        /// <summary>
        /// Whether the configuration is valid
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Validation errors if any
        /// </summary>
        public string[] Errors { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Validation warnings if any
        /// </summary>
        public string[] Warnings { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Request to simulate pricing calculation
    /// </summary>
    public class PricingSimulationRequest
    {
        /// <summary>
        /// The structured pricing configuration.
        /// </summary>
        public Dictionary<string, JsonElement> PricingConfiguration { get; set; } = new();

        /// <summary>
        /// Parameters for the simulation
        /// </summary>
        public Dictionary<string, object>? Parameters { get; set; }

        /// <summary>
        /// Video duration in seconds (for per_second pricing)
        /// </summary>
        public double? VideoDurationSeconds { get; set; }

        /// <summary>
        /// Video resolution (e.g., "1080p")
        /// </summary>
        public string? VideoResolution { get; set; }

        /// <summary>
        /// Image count (for per_unit pricing)
        /// </summary>
        public int? ImageCount { get; set; }

        /// <summary>
        /// Image resolution (e.g., "1024x1024")
        /// </summary>
        public string? ImageResolution { get; set; }

        /// <summary>
        /// Image quality (e.g., "hd", "standard")
        /// </summary>
        public string? ImageQuality { get; set; }
    }

    /// <summary>
    /// Response from pricing simulation
    /// </summary>
    public class PricingSimulationResponse
    {
        /// <summary>
        /// The calculated cost
        /// </summary>
        public decimal CalculatedCost { get; set; }

        /// <summary>
        /// The rate that was applied
        /// </summary>
        public decimal AppliedRate { get; set; }

        /// <summary>
        /// The quantity used in calculation
        /// </summary>
        public decimal Quantity { get; set; }

        /// <summary>
        /// Information about the matched rule, if any
        /// </summary>
        public MatchedRuleInfo? MatchedRule { get; set; }

        /// <summary>
        /// Whether the default rate was used
        /// </summary>
        public bool UsedDefaultRate { get; set; }

        /// <summary>
        /// Warning message if any
        /// </summary>
        public string? WarningMessage { get; set; }
    }

    /// <summary>
    /// Information about a matched pricing rule
    /// </summary>
    public class MatchedRuleInfo
    {
        /// <summary>
        /// Rule description
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Rule priority
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Rule rate
        /// </summary>
        public decimal Rate { get; set; }

        /// <summary>
        /// Summary of conditions
        /// </summary>
        public string[]? ConditionsSummary { get; set; }
    }

    /// <summary>
    /// Information about a pricing type
    /// </summary>
    public class PricingTypeInfo
    {
        /// <summary>
        /// The pricing type identifier
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Description of the pricing type
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Example calculation
        /// </summary>
        public string Example { get; set; } = string.Empty;
    }

    /// <summary>
    /// Information about a condition operator
    /// </summary>
    public class OperatorInfo
    {
        /// <summary>
        /// The operator identifier
        /// </summary>
        public string Operator { get; set; } = string.Empty;

        /// <summary>
        /// Description of the operator
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Example usage
        /// </summary>
        public string Example { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to query pricing audit events
    /// </summary>
    public class PricingAuditQueryRequest
    {
        /// <summary>
        /// Start date
        /// </summary>
        public DateTime From { get; set; }

        /// <summary>
        /// End date
        /// </summary>
        public DateTime To { get; set; }

        /// <summary>
        /// Optional virtual key ID filter
        /// </summary>
        public int? VirtualKeyId { get; set; }

        /// <summary>
        /// Optional model ID filter
        /// </summary>
        public string? ModelId { get; set; }

        /// <summary>
        /// Optional pricing type filter
        /// </summary>
        public string? PricingType { get; set; }

        /// <summary>
        /// Page number (1-based)
        /// </summary>
        public int PageNumber { get; set; } = 1;

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; } = Pagination.DefaultPageSize;
    }

    /// <summary>
    /// Pricing audit event DTO
    /// </summary>
    public class PricingAuditEventDto
    {
        /// <summary>Unique identifier for the audit event.</summary>
        public long Id { get; set; }
        /// <summary>When the pricing event occurred.</summary>
        public DateTime Timestamp { get; set; }
        /// <summary>The virtual key ID associated with this event.</summary>
        public int VirtualKeyId { get; set; }
        /// <summary>The model identifier used for pricing.</summary>
        public string ModelId { get; set; } = string.Empty;
        /// <summary>The model cost configuration ID that was applied.</summary>
        public int ModelCostId { get; set; }
        /// <summary>The type of pricing applied (e.g., token, image, audio).</summary>
        public string PricingType { get; set; } = string.Empty;
        /// <summary>Input parameters used for pricing calculation.</summary>
        public Dictionary<string, JsonElement> InputParameters { get; set; } = new();
        /// <summary>The pricing rule that matched, if any.</summary>
        public string? MatchedRule { get; set; }
        /// <summary>Whether the default rate was used instead of a specific rule.</summary>
        public bool UsedDefaultRate { get; set; }
        /// <summary>The rate that was applied for pricing.</summary>
        public decimal AppliedRate { get; set; }
        /// <summary>The quantity (tokens, images, seconds, etc.) being priced.</summary>
        public decimal Quantity { get; set; }
        /// <summary>The final calculated cost.</summary>
        public decimal CalculatedCost { get; set; }
        /// <summary>The request ID for correlation, if available.</summary>
        public string? RequestId { get; set; }
    }
}

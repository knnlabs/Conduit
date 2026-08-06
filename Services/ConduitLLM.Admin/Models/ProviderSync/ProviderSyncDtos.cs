using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConduitLLM.Admin.Models.ProviderSync
{
    /// <summary>A drift item surfaced to the admin review UI.</summary>
    public class DriftItemDto
    {
        [JsonRequired]
        public int Id { get; set; }
        [JsonRequired]
        public int ModelProviderMappingId { get; set; }
        [JsonRequired]
        public string ModelAlias { get; set; } = string.Empty;
        [JsonRequired]
        public int ProviderId { get; set; }
        [JsonRequired]
        public string ProviderName { get; set; } = string.Empty;
        [JsonRequired]
        public string OpenRouterModelId { get; set; } = string.Empty;
        [JsonRequired]
        public string DriftType { get; set; } = string.Empty;
        [JsonRequired]
        public string Status { get; set; } = string.Empty;

        /// <summary>Conduit's current values at detection time (shape depends on DriftType).</summary>
        [JsonRequired]
        public Dictionary<string, JsonElement> CurrentValues { get; set; } = new();

        /// <summary>The provider's proposed values (shape depends on DriftType).</summary>
        [JsonRequired]
        public Dictionary<string, JsonElement> ProposedValues { get; set; } = new();

        [JsonRequired]
        public DateTime FirstDetectedAt { get; set; }
        [JsonRequired]
        public DateTime LastDetectedAt { get; set; }
        [JsonRequired]
        public DateTime? ResolvedAt { get; set; }
        [JsonRequired]
        public string? ResolvedBy { get; set; }
    }

    /// <summary>A summary of a sync run for the run-history panel.</summary>
    public class ProviderSyncRunDto
    {
        [JsonRequired]
        public int Id { get; set; }
        [JsonRequired]
        public string ProviderType { get; set; } = string.Empty;
        [JsonRequired]
        public DateTime StartedAt { get; set; }
        [JsonRequired]
        public DateTime? CompletedAt { get; set; }
        [JsonRequired]
        public string Status { get; set; } = string.Empty;
        [JsonRequired]
        public string TriggeredBy { get; set; } = string.Empty;
        [JsonRequired]
        public int ModelsFetched { get; set; }
        [JsonRequired]
        public int MappingsChecked { get; set; }
        [JsonRequired]
        public int ItemsCreated { get; set; }
        [JsonRequired]
        public int ItemsUpdated { get; set; }
        [JsonRequired]
        public int ItemsAutoResolved { get; set; }
        [JsonRequired]
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Result of applying or dismissing a single drift item.</summary>
    public class DriftActionResultDto
    {
        [JsonRequired]
        public int Id { get; set; }
        [JsonRequired]
        public bool Success { get; set; }

        /// <summary>True when the item was rejected because Conduit's current values changed since detection.</summary>
        [JsonRequired]
        public bool Stale { get; set; }

        [JsonRequired]
        public string? Error { get; set; }
    }

    /// <summary>Request body for bulk apply/dismiss.</summary>
    public class BulkDriftActionRequest
    {
        [JsonRequired]
        public List<int> Ids { get; set; } = new();
    }

    /// <summary>Partial-success response for bulk apply/dismiss.</summary>
    public class BulkDriftActionResponse
    {
        [JsonRequired]
        public int SucceededCount { get; set; }
        [JsonRequired]
        public int FailedCount { get; set; }
        [JsonRequired]
        public List<DriftActionResultDto> Results { get; set; } = new();
    }
}

namespace ConduitLLM.Admin.Models.ProviderSync
{
    /// <summary>A drift item surfaced to the admin review UI.</summary>
    public class DriftItemDto
    {
        public int Id { get; set; }
        public int ModelProviderMappingId { get; set; }
        public string ModelAlias { get; set; } = string.Empty;
        public int ProviderId { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public string OpenRouterModelId { get; set; } = string.Empty;
        public string DriftType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        /// <summary>Conduit's current values at detection time (JSON; shape depends on DriftType).</summary>
        public string CurrentValuesJson { get; set; } = "{}";

        /// <summary>The provider's proposed values (JSON; shape depends on DriftType).</summary>
        public string ProposedValuesJson { get; set; } = "{}";

        public DateTime FirstDetectedAt { get; set; }
        public DateTime LastDetectedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedBy { get; set; }
    }

    /// <summary>A summary of a sync run for the run-history panel.</summary>
    public class ProviderSyncRunDto
    {
        public int Id { get; set; }
        public string ProviderType { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string TriggeredBy { get; set; } = string.Empty;
        public int ModelsFetched { get; set; }
        public int MappingsChecked { get; set; }
        public int ItemsCreated { get; set; }
        public int ItemsUpdated { get; set; }
        public int ItemsAutoResolved { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Result of applying or dismissing a single drift item.</summary>
    public class DriftActionResultDto
    {
        public int Id { get; set; }
        public bool Success { get; set; }

        /// <summary>True when the item was rejected because Conduit's current values changed since detection.</summary>
        public bool Stale { get; set; }

        public string? Error { get; set; }
    }

    /// <summary>Request body for bulk apply/dismiss.</summary>
    public class BulkDriftActionRequest
    {
        public List<int> Ids { get; set; } = new();
    }

    /// <summary>Partial-success response for bulk apply/dismiss.</summary>
    public class BulkDriftActionResponse
    {
        public int SucceededCount { get; set; }
        public int FailedCount { get; set; }
        public List<DriftActionResultDto> Results { get; set; } = new();
    }
}

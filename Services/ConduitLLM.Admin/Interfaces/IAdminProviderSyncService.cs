using ConduitLLM.Admin.Models.ProviderSync;

namespace ConduitLLM.Admin.Interfaces
{
    /// <summary>
    /// Admin-facing service for reviewing and applying OpenRouter metadata drift items.
    /// </summary>
    public interface IAdminProviderSyncService
    {
        /// <summary>Lists drift items, optionally filtered by status/type/provider.</summary>
        Task<List<DriftItemDto>> GetDriftItemsAsync(string? status, string? driftType, int? providerId, int page, int pageSize);

        /// <summary>Gets a single drift item.</summary>
        Task<DriftItemDto?> GetDriftItemAsync(int id);

        /// <summary>Applies a drift item's proposed change (routing through the event-publishing services).</summary>
        Task<DriftActionResultDto> ApplyAsync(int id, string actor);

        /// <summary>Dismisses a drift item without applying it.</summary>
        Task<DriftActionResultDto> DismissAsync(int id, string actor);

        /// <summary>Applies multiple drift items, returning per-item results.</summary>
        Task<BulkDriftActionResponse> ApplyBulkAsync(IReadOnlyList<int> ids, string actor);

        /// <summary>Dismisses multiple drift items, returning per-item results.</summary>
        Task<BulkDriftActionResponse> DismissBulkAsync(IReadOnlyList<int> ids, string actor);

        /// <summary>Lists recent sync runs.</summary>
        Task<List<ProviderSyncRunDto>> GetSyncRunsAsync(int page, int pageSize);
    }
}

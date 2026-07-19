using ConduitLLM.Admin.Models.ProviderSync;

namespace ConduitLLM.Admin.Interfaces
{
    /// <summary>
    /// Fetches OpenRouter's model catalog and detects drift against Conduit's configured ModelCost /
    /// Model rows for every OpenRouter-mapped model, persisting drift items for admin review.
    /// </summary>
    public interface IOpenRouterDriftDetectionService
    {
        /// <summary>
        /// Runs one sync cycle: fetch the catalog, compare against enabled OpenRouter mappings, and
        /// idempotently upsert drift items. Returns a summary of the run.
        /// </summary>
        /// <param name="triggeredBy">"Schedule" or "Manual".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<ProviderSyncRunDto> RunSyncAsync(string triggeredBy, CancellationToken cancellationToken = default);
    }
}

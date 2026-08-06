using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Repository interface for managing virtual key spend history.
    /// Extends IRepositoryBase for standard CRUD operations and adds domain-specific methods.
    /// </summary>
    public interface IVirtualKeySpendHistoryRepository : IRepositoryBase<VirtualKeySpendHistory, int>
    {
        /// <summary>
        /// Gets all spend history records for a specific virtual key
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of spend history records ordered by timestamp descending</returns>
        Task<List<VirtualKeySpendHistory>> GetByVirtualKeyIdAsync(int virtualKeyId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets spend history records within a date range
        /// </summary>
        /// <param name="startDate">The start date</param>
        /// <param name="endDate">The end date</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of spend history records with VirtualKey navigation property included</returns>
        Task<List<VirtualKeySpendHistory>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets spend history records for a virtual key within a date range
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        /// <param name="startDate">The start date</param>
        /// <param name="endDate">The end date</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of spend history records ordered by timestamp descending</returns>
        Task<List<VirtualKeySpendHistory>> GetByVirtualKeyAndDateRangeAsync(
            int virtualKeyId,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the total amount spent for a virtual key
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The total amount spent</returns>
        Task<decimal> GetTotalSpendAsync(int virtualKeyId, CancellationToken cancellationToken = default);
    }
}

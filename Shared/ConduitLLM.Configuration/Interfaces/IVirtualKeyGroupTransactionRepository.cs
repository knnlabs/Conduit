using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces;

/// <summary>
/// Repository interface for managing virtual key group transactions.
/// Extends IRepositoryBase for standard CRUD operations and adds domain-specific methods.
/// </summary>
public interface IVirtualKeyGroupTransactionRepository : IRepositoryBase<VirtualKeyGroupTransaction, long>
{
    /// <summary>
    /// Gets all transactions for a specific virtual key group
    /// </summary>
    /// <param name="groupId">The virtual key group ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of transactions ordered by CreatedAt descending</returns>
    Task<List<VirtualKeyGroupTransaction>> GetByGroupIdAsync(int groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets transactions within a date range
    /// </summary>
    /// <param name="startDate">The start date</param>
    /// <param name="endDate">The end date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of transactions with VirtualKeyGroup navigation property included</returns>
    Task<List<VirtualKeyGroupTransaction>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets transactions for a virtual key group within a date range
    /// </summary>
    /// <param name="groupId">The virtual key group ID</param>
    /// <param name="startDate">The start date</param>
    /// <param name="endDate">The end date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of transactions ordered by CreatedAt descending</returns>
    Task<List<VirtualKeyGroupTransaction>> GetByGroupIdAndDateRangeAsync(
        int groupId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total credits added for a virtual key group
    /// </summary>
    /// <param name="groupId">The virtual key group ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The total amount of credits added</returns>
    Task<decimal> GetTotalCreditsAsync(int groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total debits for a virtual key group
    /// </summary>
    /// <param name="groupId">The virtual key group ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The total amount of debits</returns>
    Task<decimal> GetTotalDebitsAsync(int groupId, CancellationToken cancellationToken = default);
}

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Enums;

namespace ConduitLLM.Configuration.Interfaces;

/// <summary>
/// Repository interface for managing virtual key groups.
/// Extends IRepositoryBase for standard CRUD operations and adds domain-specific methods.
/// </summary>
public interface IVirtualKeyGroupRepository : IRepositoryBase<VirtualKeyGroup, int>
{
    /// <summary>
    /// Gets a virtual key group by ID with its associated keys
    /// </summary>
    /// <param name="id">The group ID</param>
    /// <returns>The virtual key group with keys or null if not found</returns>
    Task<VirtualKeyGroup?> GetByIdWithKeysAsync(int id);

    /// <summary>
    /// Gets a virtual key group by a virtual key ID
    /// </summary>
    /// <param name="virtualKeyId">The virtual key ID</param>
    /// <returns>The virtual key group or null if not found</returns>
    Task<VirtualKeyGroup?> GetByKeyIdAsync(int virtualKeyId);

    /// <summary>
    /// Adjusts the balance of a virtual key group
    /// </summary>
    /// <param name="groupId">The group ID</param>
    /// <param name="amount">The amount to adjust (positive for credit, negative for debit)</param>
    /// <returns>The new balance after adjustment</returns>
    Task<decimal> AdjustBalanceAsync(int groupId, decimal amount);

    /// <summary>
    /// Adjusts the balance of a virtual key group with transaction details
    /// </summary>
    /// <param name="groupId">The group ID</param>
    /// <param name="amount">The amount to adjust (positive for credit, negative for debit)</param>
    /// <param name="description">Description of the transaction</param>
    /// <param name="initiatedBy">User who initiated the transaction</param>
    /// <returns>The new balance after adjustment</returns>
    Task<decimal> AdjustBalanceAsync(int groupId, decimal amount, string? description, string? initiatedBy);

    /// <summary>
    /// Adjusts the balance of a virtual key group with full transaction details including reference type
    /// </summary>
    /// <param name="groupId">The group ID</param>
    /// <param name="amount">The amount to adjust (positive for credit, negative for debit)</param>
    /// <param name="description">Description of the transaction</param>
    /// <param name="initiatedBy">User who initiated the transaction</param>
    /// <param name="referenceType">The type of reference that triggered this transaction</param>
    /// <param name="referenceId">Optional reference ID (e.g., virtual key ID)</param>
    /// <returns>The new balance after adjustment</returns>
    Task<decimal> AdjustBalanceAsync(int groupId, decimal amount, string? description, string? initiatedBy, ReferenceType referenceType, string? referenceId = null);

    /// <summary>
    /// Gets groups with low balance (below threshold)
    /// </summary>
    /// <param name="threshold">The balance threshold</param>
    /// <returns>List of groups with balance below threshold</returns>
    /// <remarks>This method is obsolete. Use GetLowBalanceGroupsPaginatedAsync instead for better performance.</remarks>
    [Obsolete("Use GetLowBalanceGroupsPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
    Task<List<VirtualKeyGroup>> GetLowBalanceGroupsAsync(decimal threshold);

    /// <summary>
    /// Gets groups with low balance (below threshold) with pagination
    /// </summary>
    /// <param name="threshold">The balance threshold</param>
    /// <param name="pageNumber">The page number (1-based)</param>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A tuple with the list of groups and the total count</returns>
    Task<(List<VirtualKeyGroup> Items, int TotalCount)> GetLowBalanceGroupsPaginatedAsync(
        decimal threshold,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}

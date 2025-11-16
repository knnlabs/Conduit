using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces;

/// <summary>
/// Repository interface for managing virtual key groups
/// </summary>
public interface IVirtualKeyGroupRepository
{
    /// <summary>
    /// Gets a virtual key group by ID
    /// </summary>
    /// <param name="id">The group ID</param>
    /// <returns>The virtual key group or null if not found</returns>
    Task<VirtualKeyGroup?> GetByIdAsync(int id);

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
    /// Gets all virtual key groups
    /// </summary>
    /// <returns>List of all virtual key groups</returns>
    Task<List<VirtualKeyGroup>> GetAllAsync();

    /// <summary>
    /// Creates a new virtual key group
    /// </summary>
    /// <param name="group">The group to create</param>
    /// <returns>The ID of the created group</returns>
    Task<int> CreateAsync(VirtualKeyGroup group);

    /// <summary>
    /// Updates an existing virtual key group
    /// </summary>
    /// <param name="group">The group to update</param>
    /// <returns>True if updated successfully</returns>
    Task<bool> UpdateAsync(VirtualKeyGroup group);

    /// <summary>
    /// Deletes a virtual key group
    /// </summary>
    /// <param name="id">The group ID to delete</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteAsync(int id);

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
    /// Gets groups with low balance (below threshold)
    /// </summary>
    /// <param name="threshold">The balance threshold</param>
    /// <returns>List of groups with balance below threshold</returns>
    Task<List<VirtualKeyGroup>> GetLowBalanceGroupsAsync(decimal threshold);
}
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository interface for managing virtual key spend history
    /// </summary>
    public interface IVirtualKeySpendHistoryRepository
    {
        /// <summary>
        /// Gets a spend history record by ID
        /// </summary>
        /// <param name="id">The spend history record ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The spend history entity or null if not found</returns>
        Task<VirtualKeySpendHistory?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets all spend history records for a specific virtual key
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of spend history records</returns>
        Task<List<VirtualKeySpendHistory>> GetByVirtualKeyIdAsync(int virtualKeyId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets spend history records within a date range
        /// </summary>
        /// <param name="startDate">The start date</param>
        /// <param name="endDate">The end date</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of spend history records</returns>
        Task<List<VirtualKeySpendHistory>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets spend history records for a virtual key within a date range
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        /// <param name="startDate">The start date</param>
        /// <param name="endDate">The end date</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of spend history records</returns>
        Task<List<VirtualKeySpendHistory>> GetByVirtualKeyAndDateRangeAsync(
            int virtualKeyId, 
            DateTime startDate, 
            DateTime endDate, 
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Creates a new spend history record
        /// </summary>
        /// <param name="spendHistory">The spend history to create</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The ID of the created spend history record</returns>
        Task<int> CreateAsync(VirtualKeySpendHistory spendHistory, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Updates a spend history record
        /// </summary>
        /// <param name="spendHistory">The spend history to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        Task<bool> UpdateAsync(VirtualKeySpendHistory spendHistory, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Deletes a spend history record
        /// </summary>
        /// <param name="id">The ID of the spend history record to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the deletion was successful, false otherwise</returns>
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets a summary of spending for a virtual key
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The total amount spent</returns>
        Task<decimal> GetTotalSpendAsync(int virtualKeyId, CancellationToken cancellationToken = default);
    }
}
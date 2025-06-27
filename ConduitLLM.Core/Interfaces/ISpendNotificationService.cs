using System;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.SignalR;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for managing spend notifications and detecting unusual spending patterns.
    /// </summary>
    public interface ISpendNotificationService
    {
        /// <summary>
        /// Notifies that spend has been updated for a virtual key (legacy method for compatibility).
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        /// <param name="spendAmount">The amount spent</param>
        /// <param name="model">The model used</param>
        /// <param name="provider">The provider used</param>
        Task NotifySpendUpdatedAsync(int virtualKeyId, decimal spendAmount, string model, string provider);

        /// <summary>
        /// Notifies about a spend update with budget information.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        /// <param name="amount">The amount spent in this transaction</param>
        /// <param name="totalSpend">The total spend after this transaction</param>
        /// <param name="budget">The budget limit (if set)</param>
        /// <param name="model">The model used</param>
        /// <param name="provider">The provider used</param>
        Task NotifySpendUpdateAsync(int virtualKeyId, decimal amount, decimal totalSpend, decimal? budget, string model, string provider);

        /// <summary>
        /// Sends a spend summary for a period.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        /// <param name="summary">The spend summary notification</param>
        Task SendSpendSummaryAsync(int virtualKeyId, SpendSummaryNotification summary);

        /// <summary>
        /// Records spend data for pattern analysis.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        /// <param name="amount">The amount spent</param>
        void RecordSpend(int virtualKeyId, decimal amount);

        /// <summary>
        /// Checks for unusual spending patterns.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        Task CheckUnusualSpendingAsync(int virtualKeyId);
    }
}
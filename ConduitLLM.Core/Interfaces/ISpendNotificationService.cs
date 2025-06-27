using System;
using System.Threading.Tasks;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for notifying about virtual key spend updates
    /// </summary>
    public interface ISpendNotificationService
    {
        /// <summary>
        /// Notifies that spend has been updated for a virtual key
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        /// <param name="spendAmount">The amount spent</param>
        /// <param name="model">The model used</param>
        /// <param name="provider">The provider used</param>
        Task NotifySpendUpdatedAsync(int virtualKeyId, decimal spendAmount, string model, string provider);
    }
}
using System;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.SignalR;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// Interface for the SpendNotificationHub that provides real-time spend tracking updates.
    /// </summary>
    public interface ISpendNotificationHub
    {
        /// <summary>
        /// Sends a spend update notification to connected clients.
        /// </summary>
        /// <param name="notification">The spend update notification.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SpendUpdate(SpendUpdateNotification notification);

        /// <summary>
        /// Sends a budget alert notification when thresholds are reached.
        /// </summary>
        /// <param name="alert">The budget alert notification.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task BudgetAlert(BudgetAlertNotification alert);

        /// <summary>
        /// Sends a spend summary notification.
        /// </summary>
        /// <param name="summary">The spend summary notification.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SpendSummary(SpendSummaryNotification summary);

        /// <summary>
        /// Sends a notification when unusual spending patterns are detected.
        /// </summary>
        /// <param name="notification">The unusual spending notification.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UnusualSpendingDetected(UnusualSpendingNotification notification);
    }
}
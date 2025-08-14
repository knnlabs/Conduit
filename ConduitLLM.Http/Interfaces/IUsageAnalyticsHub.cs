using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.SignalR;

namespace ConduitLLM.Http.Interfaces
{
    /// <summary>
    /// Interface for the UsageAnalyticsHub that provides real-time usage analytics and monitoring.
    /// </summary>
    public interface IUsageAnalyticsHub
    {
        /// <summary>
        /// Sends real-time usage metrics to subscribed clients.
        /// </summary>
        /// <param name="metrics">The usage metrics notification.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UsageMetrics(UsageMetricsNotification metrics);

        /// <summary>
        /// Sends real-time cost analytics to subscribed clients.
        /// </summary>
        /// <param name="analytics">The cost analytics notification.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CostAnalytics(CostAnalyticsNotification analytics);

        /// <summary>
        /// Sends model performance metrics to subscribed clients.
        /// </summary>
        /// <param name="metrics">The performance metrics notification.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PerformanceMetrics(PerformanceMetricsNotification metrics);

        /// <summary>
        /// Sends error analytics to subscribed clients.
        /// </summary>
        /// <param name="analytics">The error analytics notification.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ErrorAnalytics(ErrorAnalyticsNotification analytics);

        /// <summary>
        /// Sends global usage metrics to admin subscribers.
        /// </summary>
        /// <param name="metrics">The global usage metrics object.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task GlobalUsageMetrics(object metrics);

        /// <summary>
        /// Sends global cost analytics to admin subscribers.
        /// </summary>
        /// <param name="analytics">The global cost analytics object.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task GlobalCostAnalytics(object analytics);

        /// <summary>
        /// Sends global performance metrics to admin subscribers.
        /// </summary>
        /// <param name="metrics">The global performance metrics object.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task GlobalPerformanceMetrics(object metrics);

        /// <summary>
        /// Sends global error analytics to admin subscribers.
        /// </summary>
        /// <param name="analytics">The global error analytics object.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task GlobalErrorAnalytics(object analytics);

        /// <summary>
        /// Sends an analytics summary to the requesting client.
        /// </summary>
        /// <param name="summary">The analytics summary notification.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AnalyticsSummary(AnalyticsSummaryNotification summary);

        /// <summary>
        /// Notifies about successful subscription to analytics.
        /// </summary>
        /// <param name="analyticsType">The subscribed analytics type.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SubscribedToAnalytics(string analyticsType);

        /// <summary>
        /// Notifies about successful unsubscription from analytics.
        /// </summary>
        /// <param name="analyticsType">The unsubscribed analytics type.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UnsubscribedFromAnalytics(string analyticsType);

        /// <summary>
        /// Notifies about successful subscription to global analytics.
        /// </summary>
        /// <param name="analyticsType">The subscribed analytics type.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SubscribedToGlobalAnalytics(string analyticsType);

        /// <summary>
        /// Sends error messages to the client.
        /// </summary>
        /// <param name="error">The error object containing message details.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Error(object error);
    }
}
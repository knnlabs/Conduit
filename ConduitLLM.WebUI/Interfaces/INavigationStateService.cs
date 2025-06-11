using ConduitLLM.WebUI.Models;

namespace ConduitLLM.WebUI.Interfaces
{
    /// <summary>
    /// Service for managing navigation item states based on system prerequisites.
    /// </summary>
    public interface INavigationStateService
    {
        /// <summary>
        /// Gets the state of a specific navigation item.
        /// </summary>
        /// <param name="route">The route of the navigation item.</param>
        /// <returns>The state of the navigation item.</returns>
        Task<NavigationItemState> GetNavigationItemStateAsync(string route);

        /// <summary>
        /// Gets the states of all navigation items.
        /// </summary>
        /// <returns>A dictionary mapping routes to their states.</returns>
        Task<Dictionary<string, NavigationItemState>> GetAllNavigationStatesAsync();

        /// <summary>
        /// Forces a refresh of all navigation states.
        /// </summary>
        Task RefreshStatesAsync();

        /// <summary>
        /// Event raised when any navigation state changes.
        /// </summary>
        event EventHandler<NavigationStateChangedEventArgs>? NavigationStateChanged;
    }
}

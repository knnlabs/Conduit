namespace ConduitLLM.WebUI.Models
{
    /// <summary>
    /// Represents the state of a navigation item including availability and prerequisite information.
    /// </summary>
    public class NavigationItemState
    {
        /// <summary>
        /// Gets or sets whether the navigation item is enabled based on prerequisites being met.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the tooltip message explaining why the item is disabled.
        /// </summary>
        public string? TooltipMessage { get; set; }

        /// <summary>
        /// Gets or sets an optional URL to the configuration page where prerequisites can be configured.
        /// </summary>
        public string? RequiredConfigurationUrl { get; set; }

        /// <summary>
        /// Gets or sets whether to show a visual indicator (e.g., warning icon) on the navigation item.
        /// </summary>
        public bool ShowIndicator { get; set; }
    }

    /// <summary>
    /// Event arguments for navigation state change events.
    /// </summary>
    public class NavigationStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the route that changed state.
        /// </summary>
        public string Route { get; }

        /// <summary>
        /// Gets the new state of the navigation item.
        /// </summary>
        public NavigationItemState NewState { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NavigationStateChangedEventArgs"/> class.
        /// </summary>
        /// <param name="route">The route that changed state.</param>
        /// <param name="newState">The new state of the navigation item.</param>
        public NavigationStateChangedEventArgs(string route, NavigationItemState newState)
        {
            Route = route;
            NewState = newState;
        }
    }
}

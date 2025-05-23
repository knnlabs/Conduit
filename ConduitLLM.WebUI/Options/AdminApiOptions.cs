namespace ConduitLLM.WebUI.Options
{
    /// <summary>
    /// Configuration options for the Admin API client.
    /// </summary>
    public class AdminApiOptions
    {
        /// <summary>
        /// Gets or sets the base URL for the Admin API.
        /// Default is http://localhost:5000 for local development.
        /// </summary>
        public string BaseUrl { get; set; } = "http://localhost:5000";

        /// <summary>
        /// Gets or sets the master key for authenticating with the Admin API.
        /// </summary>
        public string MasterKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timeout in seconds for API requests.
        /// Default is 30 seconds.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets a value indicating whether to use the Admin API client.
        /// If false, direct repository access will be used instead.
        /// Default is true, which means the Admin API will be used.
        /// </summary>
        public bool UseAdminApi { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the Admin API client is enabled.
        /// This is an alias for UseAdminApi for backward compatibility.
        /// </summary>
        public bool Enabled
        {
            get => UseAdminApi;
            set => UseAdminApi = value;
        }
    }
}
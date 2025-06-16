using System.Threading.Tasks;

using ConduitLLM.Providers.Configuration;

namespace ConduitLLM.WebUI.Interfaces
{
    /// <summary>
    /// Service for managing HTTP retry configuration settings
    /// </summary>
    public interface IHttpRetryConfigurationService
    {
        /// <summary>
        /// Updates the HTTP retry configuration settings
        /// </summary>
        /// <param name="retryOptions">The retry options to save</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task UpdateRetryConfigurationAsync(RetryOptions retryOptions);

        /// <summary>
        /// Loads HTTP retry settings from the database into the application options
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        Task LoadSettingsFromDatabaseAsync();

        /// <summary>
        /// Gets the current retry configuration
        /// </summary>
        /// <returns>The current retry configuration</returns>
        RetryOptions GetRetryConfiguration();
    }
}

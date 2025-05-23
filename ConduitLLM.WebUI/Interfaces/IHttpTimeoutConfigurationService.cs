using ConduitLLM.Providers.Configuration;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Interfaces
{
    /// <summary>
    /// Service for managing HTTP timeout configuration settings
    /// </summary>
    public interface IHttpTimeoutConfigurationService
    {
        /// <summary>
        /// Updates the HTTP timeout configuration settings
        /// </summary>
        /// <param name="timeoutOptions">The timeout options to save</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task UpdateTimeoutConfigurationAsync(TimeoutOptions timeoutOptions);
        
        /// <summary>
        /// Loads HTTP timeout settings from the database into the application options
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        Task LoadSettingsFromDatabaseAsync();

        /// <summary>
        /// Gets the current timeout configuration
        /// </summary>
        /// <returns>The current timeout configuration</returns>
        TimeoutOptions GetTimeoutConfiguration();
    }
}
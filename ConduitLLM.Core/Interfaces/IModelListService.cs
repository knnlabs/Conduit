using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for retrieving available models from LLM providers.
    /// </summary>
    [Obsolete("External model discovery is no longer used. The ProviderModelsController now returns models from the local database based on provider type compatibility. This interface will be removed in a future version.")]
    public interface IModelListService
    {
        /// <summary>
        /// Gets a list of available model IDs from a provider.
        /// </summary>
        /// <param name="provider">The provider entity.</param>
        /// <param name="keyCredential">The key credential to use for authentication.</param>
        /// <param name="forceRefresh">Whether to bypass cache and force a refresh.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of available model IDs.</returns>
        [Obsolete("Use the ProviderModelsController to get models from the local database instead.")]
        Task<List<string>> GetModelsForProviderAsync(
            Provider provider,
            ProviderKeyCredential keyCredential,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default);
    }
}
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Providers.Common.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.Groq
{
    /// <summary>
    /// GroqClient partial class containing model discovery methods.
    /// </summary>
    public partial class GroqClient
    {
        /// <summary>
        /// Gets available models from the Groq API or falls back to a predefined list.
        /// </summary>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A list of available models from Groq.</returns>
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Attempt to use the generic OpenAI-compatible /models endpoint
                return await base.GetModelsAsync(apiKey, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to retrieve models from Groq API.");
                throw;
            }
        }
    }
}

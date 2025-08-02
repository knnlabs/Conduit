using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Providers.Common.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.Anthropic
{
    /// <summary>
    /// AnthropicClient partial class containing model listing functionality.
    /// </summary>
    public partial class AnthropicClient
    {
        /// <summary>
        /// Gets available models from the Anthropic API.
        /// </summary>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A list of available models.</returns>
        /// <remarks>
        /// <para>
        /// Anthropic does not provide a models endpoint like other LLM providers. Instead,
        /// this method returns a static list of known Anthropic models. The current list includes:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>Claude 3 models (Opus, Sonnet, Haiku)</description></item>
        ///   <item><description>Claude 2.1 and 2.0</description></item>
        ///   <item><description>Claude Instant 1.2</description></item>
        /// </list>
        /// <para>
        /// This method is implemented as a Task for consistency with the interface, but
        /// it doesn't actually make any API requests.
        /// </para>
        /// </remarks>
        public override Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            Logger.LogWarning("Anthropic does not provide a models listing endpoint");
            throw new NotSupportedException(
                "Anthropic does not provide a models listing endpoint. " +
                "Model availability must be confirmed through Anthropic's documentation. " +
                "Configure specific model IDs directly in your application settings.");
        }
    }
}
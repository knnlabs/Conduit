using System;
using System.Threading;
using System.Threading.Tasks;

using CoreModels = ConduitLLM.Core.Models;

namespace ConduitLLM.Providers.Providers.Anthropic
{
    /// <summary>
    /// AnthropicClient partial class containing image generation functionality.
    /// </summary>
    public partial class AnthropicClient
    {
        /// <summary>
        /// Generates images using the Anthropic API (not currently supported).
        /// </summary>
        /// <param name="request">The image generation request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>This method does not return as it throws a NotSupportedException.</returns>
        /// <remarks>
        /// <para>
        /// As of early 2025, Anthropic does not provide an image generation API.
        /// Although Claude models can analyze images, they cannot generate them.
        /// This method is implemented to fulfill the ILLMClient interface but will
        /// always throw a NotSupportedException.
        /// </para>
        /// <para>
        /// If Anthropic adds image generation support in the future, this method should
        /// be updated to implement the actual API call.
        /// </para>
        /// </remarks>
        /// <exception cref="NotSupportedException">Always thrown as image generation is not supported by Anthropic.</exception>
        public override Task<CoreModels.ImageGenerationResponse> CreateImageAsync(
            CoreModels.ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Image generation is not currently supported by the Anthropic API");
        }
    }
}
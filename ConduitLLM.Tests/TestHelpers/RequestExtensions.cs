using ConduitLLM.Core.Models;
using ConduitLLM.Tests.TestHelpers.Mocks;

namespace ConduitLLM.Tests.TestHelpers
{
    /// <summary>
    /// Extension methods for request objects
    /// </summary>
    public static class RequestExtensions
    {
        /// <summary>
        /// Converts a ChatCompletionRequest to a provider-specific request
        /// </summary>
        public static TRequest ToProviderRequest<TRequest>(this ConduitLLM.Core.Models.ChatCompletionRequest request) where TRequest : new()
        {
            // The implementation details don't matter for tests, this is just a placeholder
            return new TRequest();
        }
    }
}

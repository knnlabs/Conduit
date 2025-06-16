using System.Threading.Tasks;

using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for managing context window sizes in LLM requests to prevent token limit errors.
    /// </summary>
    public interface IContextManager
    {
        /// <summary>
        /// Trims messages in the request if needed to fit within the max token limit.
        /// Returns a potentially new request object with trimmed messages.
        /// </summary>
        /// <param name="request">The original chat completion request</param>
        /// <param name="maxContextTokens">The maximum context window size in tokens</param>
        /// <returns>A new or original request with messages that fit within the token limit</returns>
        Task<ChatCompletionRequest> ManageContextAsync(ChatCompletionRequest request, int? maxContextTokens);
    }
}

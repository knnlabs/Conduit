using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for estimating token counts in LLM messages.
    /// </summary>
    public interface ITokenCounter
    {
        /// <summary>
        /// Estimates tokens for a list of messages, considering model specifics.
        /// </summary>
        /// <param name="modelName">Name of the model to use for token estimation</param>
        /// <param name="messages">List of messages to count tokens for</param>
        /// <returns>Estimated token count</returns>
        Task<int> EstimateTokenCountAsync(string modelName, List<Message> messages);

        /// <summary>
        /// Estimates tokens for a single text string.
        /// </summary>
        /// <param name="modelName">Name of the model to use for token estimation</param>
        /// <param name="text">Text to count tokens for</param>
        /// <returns>Estimated token count</returns>
        Task<int> EstimateTokenCountAsync(string modelName, string text);
    }
}

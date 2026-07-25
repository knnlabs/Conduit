using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for estimating token counts in LLM messages.
    /// </summary>
    /// <remarks>
    /// Estimates carry a <see cref="TokenCountFidelity"/> alongside the number (#1233).
    /// Consumers that turn counts into money or limits — spend reservations, fallback billing,
    /// rate-limit windows — should size any safety buffer by that fidelity rather than applying
    /// one flat figure: an exact count and a chars/4 guess do not deserve the same treatment.
    /// </remarks>
    public interface ITokenCounter
    {
        /// <summary>
        /// Estimates tokens for a list of messages, considering model specifics.
        /// </summary>
        /// <param name="modelName">Name of the model to use for token estimation</param>
        /// <param name="messages">
        /// List of messages to count tokens for. Assistant messages' <see cref="Message.ToolCalls"/>
        /// are counted; providers include them in the prompt on the next turn (#1229).
        /// </param>
        /// <param name="tools">
        /// Tool definitions accompanying the request, or null. Providers inject these schemas into
        /// the prompt, so omitting them under-counts agentic requests — pass the request's tools
        /// whenever they are available (#1229).
        /// </param>
        /// <returns>The estimated token count and its fidelity</returns>
        Task<TokenCount> EstimateTokenCountAsync(string modelName, List<Message> messages, IReadOnlyList<Tool>? tools = null);

        /// <summary>
        /// Estimates tokens for a single text string.
        /// </summary>
        /// <param name="modelName">Name of the model to use for token estimation</param>
        /// <param name="text">Text to count tokens for</param>
        /// <returns>The estimated token count and its fidelity</returns>
        Task<TokenCount> EstimateTokenCountAsync(string modelName, string text);
    }
}

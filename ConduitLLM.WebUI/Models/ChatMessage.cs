using ConduitLLM.Core.Models;

namespace ConduitLLM.WebUI.Models
{
    /// <summary>
    /// Extended message model for the WebUI that includes performance metrics.
    /// </summary>
    public class ChatMessage : Message
    {
        /// <summary>
        /// Performance metrics for this message (if it's an assistant response).
        /// </summary>
        public PerformanceMetrics? PerformanceMetrics { get; set; }

        /// <summary>
        /// Creates a ChatMessage from a regular Message.
        /// </summary>
        public static ChatMessage FromMessage(Message message, PerformanceMetrics? performanceMetrics = null)
        {
            return new ChatMessage
            {
                Role = message.Role,
                Content = message.Content,
                Name = message.Name,
                ToolCalls = message.ToolCalls,
                ToolCallId = message.ToolCallId,
                Timestamp = message.Timestamp,
                PerformanceMetrics = performanceMetrics
            };
        }
    }
}
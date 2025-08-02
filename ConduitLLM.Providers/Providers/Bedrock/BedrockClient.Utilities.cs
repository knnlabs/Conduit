using System;
using System.Collections.Generic;
using System.Text;

using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.Helpers;

namespace ConduitLLM.Providers.Providers.Bedrock
{
    /// <summary>
    /// BedrockClient partial class containing utility and helper methods.
    /// </summary>
    public partial class BedrockClient
    {
        /// <summary>
        /// Maps Bedrock stop reasons to the standardized finish reasons used in the core models.
        /// </summary>
        /// <param name="stopReason">The Bedrock stop reason.</param>
        /// <returns>The standardized finish reason.</returns>
        protected string MapBedrockStopReason(string? stopReason)
        {
            return stopReason?.ToLowerInvariant() switch
            {
                "stop_sequence" => "stop",
                "max_tokens" => "length",
                _ => stopReason ?? "unknown"
            };
        }

        /// <summary>
        /// Builds a simple prompt from messages for non-chat models.
        /// </summary>
        private string BuildPrompt(IEnumerable<ConduitLLM.Core.Models.Message> messages)
        {
            var promptBuilder = new StringBuilder();
            
            foreach (var message in messages)
            {
                var content = ContentHelper.GetContentAsString(message.Content);
                
                if (message.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.AppendLine($"System: {content}");
                }
                else if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.AppendLine($"Human: {content}");
                }
                else if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.AppendLine($"Assistant: {content}");
                }
            }
            
            // Add prompt for assistant response
            promptBuilder.Append("Assistant:");
            
            return promptBuilder.ToString();
        }
        
        /// <summary>
        /// Builds a Llama-specific prompt format.
        /// </summary>
        private string BuildLlamaPrompt(IEnumerable<ConduitLLM.Core.Models.Message> messages)
        {
            var promptBuilder = new StringBuilder();
            
            // Llama format uses special tokens
            promptBuilder.Append("<s>");
            
            foreach (var message in messages)
            {
                var content = ContentHelper.GetContentAsString(message.Content);
                
                if (message.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.Append($"[INST] <<SYS>>\n{content}\n<</SYS>>\n\n");
                }
                else if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.Append($"{content} [/INST]");
                }
                else if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.Append($" {content} </s><s>[INST] ");
                }
            }
            
            return promptBuilder.ToString();
        }
        
        /// <summary>
        /// Builds a Cohere-specific prompt format.
        /// </summary>
        private string BuildCoherePrompt(IEnumerable<ConduitLLM.Core.Models.Message> messages)
        {
            var promptBuilder = new StringBuilder();
            
            foreach (var message in messages)
            {
                var content = ContentHelper.GetContentAsString(message.Content);
                
                if (message.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.AppendLine($"Instructions: {content}");
                    promptBuilder.AppendLine();
                }
                else if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.AppendLine($"User: {content}");
                }
                else if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.AppendLine($"Chatbot: {content}");
                }
            }
            
            // Add prompt for chatbot response
            promptBuilder.Append("Chatbot:");
            
            return promptBuilder.ToString();
        }
        
        /// <summary>
        /// Builds a Mistral-specific prompt format.
        /// </summary>
        private string BuildMistralPrompt(IEnumerable<ConduitLLM.Core.Models.Message> messages)
        {
            var promptBuilder = new StringBuilder();
            
            // Mistral uses a specific instruction format
            promptBuilder.Append("<s>");
            
            foreach (var message in messages)
            {
                var content = ContentHelper.GetContentAsString(message.Content);
                
                if (message.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.Append($"[INST] {content} [/INST]</s>");
                }
                else if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.Append($"[INST] {content} [/INST]");
                }
                else if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    promptBuilder.Append($"{content}</s>");
                }
            }
            
            return promptBuilder.ToString();
        }
        
        /// <summary>
        /// Maps Cohere stop reasons to standardized finish reasons.
        /// </summary>
        private string MapCohereStopReason(string? finishReason)
        {
            return finishReason?.ToUpperInvariant() switch
            {
                "COMPLETE" => "stop",
                "MAX_TOKENS" => "length",
                "ERROR" => "stop",
                "ERROR_TOXIC" => "content_filter",
                _ => "stop"
            };
        }
        
        /// <summary>
        /// Maps Llama stop reasons to standardized finish reasons.
        /// </summary>
        private string MapLlamaStopReason(string? stopReason)
        {
            return stopReason?.ToLowerInvariant() switch
            {
                "length" => "length",
                "max_length" => "length",
                "stop" => "stop",
                "end_of_sequence" => "stop",
                null => "stop",
                _ => "stop"
            };
        }
        
        /// <summary>
        /// Maps Titan completion reasons to standardized finish reasons.
        /// </summary>
        private string MapTitanCompletionReason(string? completionReason)
        {
            return completionReason?.ToUpperInvariant() switch
            {
                "COMPLETE" => "stop",
                "LENGTH" => "length",
                "CONTENT_FILTERED" => "content_filter",
                _ => "stop"
            };
        }
        
        /// <summary>
        /// Maps AI21 finish reasons to standardized finish reasons.
        /// </summary>
        private string MapAI21FinishReason(string? finishReason)
        {
            return finishReason?.ToLowerInvariant() switch
            {
                "endoftext" => "stop",
                "length" => "length",
                "stop" => "stop",
                _ => "stop"
            };
        }
        
        /// <summary>
        /// Maps Mistral stop reasons to standardized finish reasons.
        /// </summary>
        private string MapMistralStopReason(string? stopReason)
        {
            return stopReason?.ToLowerInvariant() switch
            {
                "stop" => "stop",
                "length" => "length",
                "model_length" => "length",
                _ => "stop"
            };
        }
        
        /// <summary>
        /// Estimates token count for a text (rough approximation).
        /// </summary>
        private int EstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            
            // Rough estimate: 1 token per 4 characters
            return (int)Math.Ceiling(text.Length / 4.0);
        }
    }
}
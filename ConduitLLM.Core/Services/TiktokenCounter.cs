using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TiktokenSharp;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Token counter implementation using TiktokenSharp for OpenAI-compatible tokenization.
    /// </summary>
    public class TiktokenCounter : ITokenCounter
    {
        // Cache encodings for performance
        private static readonly Dictionary<string, TikToken> _encodings = new();
        private static readonly object _lock = new();
        private readonly ILogger<TiktokenCounter> _logger;

        public TiktokenCounter(ILogger<TiktokenCounter> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public Task<int> EstimateTokenCountAsync(string modelName, List<Message> messages)
        {
            if (messages == null || !messages.Any())
            {
                return Task.FromResult(0);
            }

            try
            {
                var encoding = GetEncodingForModel(modelName);
                if (encoding == null)
                {
                    // Fallback strategy if we can't get the right encoding
                    _logger.LogWarning("Could not determine encoding for model {ModelName}. Using fallback token estimation method.", modelName);
                    return Task.FromResult(FallbackEstimateTokens(messages));
                }

                int tokenCount = 0;
                foreach (var message in messages)
                {
                    // OpenAI adds tokens per message and per role. 
                    // These numbers are based on OpenAI's tokenization approach
                    tokenCount += 4; // Every message follows <|start|>{role/name}\n{content}<|end|>\n
                    
                    if (message.Role != null)
                    {
                        try
                        {
                            tokenCount += encoding.Encode(message.Role).Count;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error encoding role. Using fallback estimate.");
                            tokenCount += message.Role.Length / 4;
                        }
                    }
                    
                    if (message.Content != null)
                    {
                        try
                        {
                            tokenCount += encoding.Encode(message.Content).Count;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error encoding content. Using fallback estimate.");
                            tokenCount += message.Content.Length / 4;
                        }
                    }
                    
                    // Add handling for 'Name' property if present
                    if (!string.IsNullOrEmpty(message.Name))
                    {
                        try
                        {
                            tokenCount += encoding.Encode(message.Name).Count;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error encoding name. Using fallback estimate.");
                            tokenCount += message.Name.Length / 4;
                        }
                        tokenCount += 1; // Additional overhead for name field
                    }
                }
                
                tokenCount += 3; // Every reply is primed with <|start|>assistant<|message|>

                return Task.FromResult(tokenCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error estimating token count. Using fallback method.");
                return Task.FromResult(FallbackEstimateTokens(messages));
            }
        }

        /// <inheritdoc />
        public Task<int> EstimateTokenCountAsync(string modelName, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Task.FromResult(0);
            }

            try
            {
                var encoding = GetEncodingForModel(modelName);
                if (encoding == null)
                {
                    // Fallback strategy
                    _logger.LogWarning("Could not determine encoding for model {ModelName}. Using fallback token estimation method.", modelName);
                    return Task.FromResult(FallbackEstimateTokens(text));
                }

                try
                {
                    return Task.FromResult(encoding.Encode(text).Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error encoding text. Using fallback estimate.");
                    return Task.FromResult(FallbackEstimateTokens(text));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error estimating token count. Using fallback method.");
                return Task.FromResult(FallbackEstimateTokens(text));
            }
        }

        /// <summary>
        /// Gets the appropriate TikToken encoding for a given model.
        /// </summary>
        private TikToken? GetEncodingForModel(string modelName)
        {
            try
            {
                // Default to the most common encoding
                string encodingName = "cl100k_base"; // Default for newer models (GPT-3.5, GPT-4)
                
                // Lower case the model name for consistent matching
                string lowerModelName = modelName.ToLowerInvariant();
                
                // Map model names/families to their encodings
                if (lowerModelName.Contains("gpt-3.5") || lowerModelName.Contains("gpt-4"))
                {
                    encodingName = "cl100k_base";
                }
                else if (lowerModelName.Contains("davinci") || 
                         lowerModelName.Contains("curie") || 
                         lowerModelName.Contains("babbage") || 
                         lowerModelName.Contains("ada"))
                {
                    encodingName = "p50k_base";
                }
                // Add more mappings as needed for other model families

                lock (_lock)
                {
                    if (!_encodings.TryGetValue(encodingName, out var encoding))
                    {
                        try
                        {
                            encoding = TikToken.EncodingForModel(encodingName);
                            _encodings[encodingName] = encoding;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to get encoding {EncodingName} for model {ModelName}", encodingName, modelName);
                            return null;
                        }
                    }
                    return encoding;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetEncodingForModel");
                return null;
            }
        }

        /// <summary>
        /// Provides a fallback method for estimating tokens when the proper encoder can't be used.
        /// </summary>
        private int FallbackEstimateTokens(List<Message> messages)
        {
            // Very rough estimation based on characters
            int totalCharacters = messages.Sum(m => 
                (m.Content?.Length ?? 0) + 
                (m.Role?.Length ?? 0) + 
                (m.Name?.Length ?? 0));
                
            // A very rough approximation: 4 characters per token on average
            // Plus some overhead for message structure
            return (totalCharacters / 4) + (messages.Count * 5);
        }

        /// <summary>
        /// Provides a fallback method for estimating tokens for a single text string.
        /// </summary>
        private int FallbackEstimateTokens(string text)
        {
            // Rough approximation: average 4 characters per token
            return text.Length / 4;
        }
    }
}

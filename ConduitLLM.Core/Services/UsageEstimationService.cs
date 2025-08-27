using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Service that estimates usage when providers don't return usage data in streaming responses.
    /// Uses conservative estimation to prevent undercharging customers.
    /// </summary>
    public class UsageEstimationService : IUsageEstimationService
    {
        private readonly ITokenCounter _tokenCounter;
        private readonly ILogger<UsageEstimationService> _logger;
        
        /// <summary>
        /// Buffer percentage to add to estimated tokens to ensure we don't undercharge.
        /// Default is 10% overhead for conservative estimation.
        /// </summary>
        private const double ConservativeBufferPercentage = 0.10;

        public UsageEstimationService(
            ITokenCounter tokenCounter,
            ILogger<UsageEstimationService> logger)
        {
            _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<Usage> EstimateUsageFromStreamingResponseAsync(
            string modelId,
            List<Message> inputMessages,
            string streamedContent,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(modelId))
                throw new ArgumentNullException(nameof(modelId));
            if (inputMessages == null || inputMessages.Count == 0)
                throw new ArgumentException("Input messages cannot be null or empty", nameof(inputMessages));
            if (string.IsNullOrEmpty(streamedContent))
                throw new ArgumentException("Streamed content cannot be null or empty", nameof(streamedContent));

            try
            {
                _logger.LogInformation("Estimating usage for model {Model} with {MessageCount} input messages and {OutputLength} characters of output",
                    modelId, inputMessages.Count, streamedContent.Length);

                // Estimate prompt tokens from input messages
                var promptTokens = await _tokenCounter.EstimateTokenCountAsync(modelId, inputMessages);
                
                // Estimate completion tokens from streamed content
                var completionTokens = await _tokenCounter.EstimateTokenCountAsync(modelId, streamedContent);
                
                // Apply conservative buffer to avoid undercharging
                var bufferedPromptTokens = (int)Math.Ceiling(promptTokens * (1 + ConservativeBufferPercentage));
                var bufferedCompletionTokens = (int)Math.Ceiling(completionTokens * (1 + ConservativeBufferPercentage));
                
                var usage = new Usage
                {
                    PromptTokens = bufferedPromptTokens,
                    CompletionTokens = bufferedCompletionTokens,
                    TotalTokens = bufferedPromptTokens + bufferedCompletionTokens
                };

                _logger.LogInformation(
                    "Estimated usage for model {Model}: Prompt={PromptTokens} (raw={RawPrompt}), " +
                    "Completion={CompletionTokens} (raw={RawCompletion}), Total={TotalTokens}, Buffer={Buffer:P0}",
                    modelId, usage.PromptTokens, promptTokens, 
                    usage.CompletionTokens, completionTokens, 
                    usage.TotalTokens, ConservativeBufferPercentage);

                return usage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to estimate usage for model {Model}, falling back to character-based estimation", modelId);
                
                // Fallback to character-based estimation if tokenization fails
                // Using conservative 4 characters per token estimate
                var fallbackPromptTokens = EstimateTokensFromCharacters(GetTotalCharacterCount(inputMessages));
                var fallbackCompletionTokens = EstimateTokensFromCharacters(streamedContent.Length);
                
                // Apply conservative buffer
                var bufferedPromptTokens = (int)Math.Ceiling(fallbackPromptTokens * (1 + ConservativeBufferPercentage));
                var bufferedCompletionTokens = (int)Math.Ceiling(fallbackCompletionTokens * (1 + ConservativeBufferPercentage));
                
                var fallbackUsage = new Usage
                {
                    PromptTokens = bufferedPromptTokens,
                    CompletionTokens = bufferedCompletionTokens,
                    TotalTokens = bufferedPromptTokens + bufferedCompletionTokens
                };

                _logger.LogWarning(
                    "Using character-based fallback estimation for model {Model}: " +
                    "Prompt={PromptTokens}, Completion={CompletionTokens}, Total={TotalTokens}",
                    modelId, fallbackUsage.PromptTokens, fallbackUsage.CompletionTokens, fallbackUsage.TotalTokens);

                return fallbackUsage;
            }
        }

        /// <inheritdoc/>
        public async Task<Usage> EstimateUsageFromTextAsync(
            string modelId,
            string inputText,
            string outputText,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(modelId))
                throw new ArgumentNullException(nameof(modelId));
            if (string.IsNullOrEmpty(inputText))
                throw new ArgumentException("Input text cannot be null or empty", nameof(inputText));
            if (string.IsNullOrEmpty(outputText))
                throw new ArgumentException("Output text cannot be null or empty", nameof(outputText));

            try
            {
                _logger.LogInformation("Estimating usage for model {Model} from text: input={InputLength} chars, output={OutputLength} chars",
                    modelId, inputText.Length, outputText.Length);

                // Estimate tokens for input and output
                var promptTokens = await _tokenCounter.EstimateTokenCountAsync(modelId, inputText);
                var completionTokens = await _tokenCounter.EstimateTokenCountAsync(modelId, outputText);
                
                // Apply conservative buffer
                var bufferedPromptTokens = (int)Math.Ceiling(promptTokens * (1 + ConservativeBufferPercentage));
                var bufferedCompletionTokens = (int)Math.Ceiling(completionTokens * (1 + ConservativeBufferPercentage));
                
                var usage = new Usage
                {
                    PromptTokens = bufferedPromptTokens,
                    CompletionTokens = bufferedCompletionTokens,
                    TotalTokens = bufferedPromptTokens + bufferedCompletionTokens
                };

                _logger.LogInformation(
                    "Estimated text usage for model {Model}: Prompt={PromptTokens}, Completion={CompletionTokens}, Total={TotalTokens}",
                    modelId, usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens);

                return usage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to estimate text usage for model {Model}, using fallback", modelId);
                
                // Fallback to character-based estimation
                var fallbackPromptTokens = EstimateTokensFromCharacters(inputText.Length);
                var fallbackCompletionTokens = EstimateTokensFromCharacters(outputText.Length);
                
                // Apply conservative buffer
                var bufferedPromptTokens = (int)Math.Ceiling(fallbackPromptTokens * (1 + ConservativeBufferPercentage));
                var bufferedCompletionTokens = (int)Math.Ceiling(fallbackCompletionTokens * (1 + ConservativeBufferPercentage));
                
                return new Usage
                {
                    PromptTokens = bufferedPromptTokens,
                    CompletionTokens = bufferedCompletionTokens,
                    TotalTokens = bufferedPromptTokens + bufferedCompletionTokens
                };
            }
        }

        /// <summary>
        /// Estimates token count from character count using conservative 4 characters per token ratio.
        /// This is a fallback when proper tokenization fails.
        /// </summary>
        private int EstimateTokensFromCharacters(int characterCount)
        {
            // Conservative estimate: 4 characters per token (OpenAI average is ~4 chars/token for English)
            // This tends to overestimate slightly, which is good for revenue protection
            const double CharsPerToken = 4.0;
            return Math.Max(1, (int)Math.Ceiling(characterCount / CharsPerToken));
        }

        /// <summary>
        /// Gets the total character count from a list of messages.
        /// </summary>
        private int GetTotalCharacterCount(List<Message> messages)
        {
            int totalChars = 0;
            foreach (var message in messages)
            {
                // Count role characters
                if (!string.IsNullOrEmpty(message.Role))
                    totalChars += message.Role.Length;
                
                // Count content based on type
                if (message.Content is string stringContent)
                {
                    totalChars += stringContent.Length;
                }
                else if (message.Content is System.Collections.IEnumerable contentParts)
                {
                    // Handle multimodal content (list of content parts)
                    foreach (var part in contentParts)
                    {
                        // Try to handle different content part types dynamically
                        var partType = part.GetType();
                        var typeProperty = partType.GetProperty("Type");
                        if (typeProperty != null)
                        {
                            var typeValue = typeProperty.GetValue(part)?.ToString();
                            if (typeValue == "text")
                            {
                                var textProperty = partType.GetProperty("Text");
                                var text = textProperty?.GetValue(part)?.ToString();
                                if (!string.IsNullOrEmpty(text))
                                {
                                    totalChars += text.Length;
                                }
                            }
                            else if (typeValue == "image_url")
                            {
                                // Estimate ~500 chars for image reference (conservative)
                                totalChars += 500;
                            }
                        }
                    }
                }
                
                // Count name if present
                if (!string.IsNullOrEmpty(message.Name))
                    totalChars += message.Name.Length;
            }
            
            return totalChars;
        }
    }
}
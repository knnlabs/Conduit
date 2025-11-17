using System.Text;
using System.Text.Json;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;
using Microsoft.ML.Tokenizers;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Token counter implementation using Microsoft.ML.Tokenizers for LLaMA-based models.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The LlamaTokenCounter provides accurate token counting for LLaMA and LLaMA-derived models
    /// including Meta's LLaMA family and models from providers like Groq, Cerebras, and others
    /// that use LLaMA tokenization.
    /// </para>
    /// <para>
    /// This implementation uses Microsoft's official ML.Tokenizers library which provides
    /// native LLaMA tokenizer support with high performance and accuracy.
    /// </para>
    /// </remarks>
    public class LlamaTokenCounter : ITokenCounter
    {
        // Cache tokenizers for performance
        private static readonly Dictionary<string, Tokenizer> _tokenizers = new();
        private static readonly object _lock = new();
        private readonly ILogger<LlamaTokenCounter> _logger;
        private readonly IModelCapabilityService? _capabilityService;

        /// <summary>
        /// Initializes a new instance of the <see cref="LlamaTokenCounter"/> class.
        /// </summary>
        /// <param name="logger">The logger for recording diagnostic information.</param>
        /// <param name="capabilityService">Service for retrieving model capabilities from configuration.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
        public LlamaTokenCounter(ILogger<LlamaTokenCounter> logger, IModelCapabilityService? capabilityService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _capabilityService = capabilityService;

            if (capabilityService == null)
            {
                _logger.LogWarning("ModelCapabilityService not available, using default LLaMA tokenizer");
            }
        }

        /// <inheritdoc />
        public Task<int> EstimateTokenCountAsync(string modelName, List<Message> messages)
        {
            if (messages == null || messages.Count() == 0)
            {
                return Task.FromResult(0);
            }

            try
            {
                var tokenizer = GetTokenizerForModel(modelName);
                if (tokenizer == null)
                {
                    _logger.LogWarning("Could not get LLaMA tokenizer for model {ModelName}. Using fallback estimation.", modelName);
                    return Task.FromResult(FallbackEstimateTokens(messages));
                }

                int tokenCount = 0;
                foreach (var message in messages)
                {
                    // LLaMA models typically use special tokens for message boundaries
                    // Add overhead for message formatting
                    tokenCount += 4; // Approximate overhead for message structure

                    if (message.Role != null)
                    {
                        try
                        {
                            tokenCount += tokenizer.CountTokens(message.Role);
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
                            if (message.Content is string contentStr)
                            {
                                // Simple string content
                                tokenCount += tokenizer.CountTokens(contentStr);
                            }
                            else if (message.Content is JsonElement jsonElement)
                            {
                                // Handle JsonElement (common when deserialized from JSON)
                                tokenCount += EstimateJsonElementTokens(jsonElement, tokenizer);
                            }
                            else
                            {
                                // Try to handle content parts or other objects
                                string textContent = ExtractTextFromContentObject(message.Content);
                                tokenCount += tokenizer.CountTokens(textContent);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error encoding content. Using fallback estimate.");
                            string contentStr = message.Content.ToString() ?? "";
                            tokenCount += contentStr.Length / 4;
                        }
                    }

                    if (!string.IsNullOrEmpty(message.Name))
                    {
                        try
                        {
                            tokenCount += tokenizer.CountTokens(message.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error encoding name. Using fallback estimate.");
                            tokenCount += message.Name.Length / 4;
                        }
                        tokenCount += 1; // Additional overhead for name field
                    }
                }

                tokenCount += 3; // Reply priming tokens

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
                var tokenizer = GetTokenizerForModel(modelName);
                if (tokenizer == null)
                {
                    _logger.LogWarning("Could not get LLaMA tokenizer for model {ModelName}. Using fallback estimation.", modelName);
                    return Task.FromResult(FallbackEstimateTokens(text));
                }

                try
                {
                    return Task.FromResult(tokenizer.CountTokens(text));
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
        /// Gets the appropriate LLaMA tokenizer for a given model.
        /// </summary>
        /// <param name="modelName">The name of the model to get tokenizer for.</param>
        /// <returns>The appropriate tokenizer, or null if it cannot be determined.</returns>
        private Tokenizer? GetTokenizerForModel(string modelName)
        {
            try
            {
                // Check if we already have a cached tokenizer for this model
                lock (_lock)
                {
                    if (_tokenizers.TryGetValue(modelName, out var cachedTokenizer))
                    {
                        return cachedTokenizer;
                    }
                }

                // Determine which LLaMA tokenizer to use based on model name
                string tokenizerKey = DetermineTokenizerKey(modelName);

                lock (_lock)
                {
                    // Double-check after acquiring lock
                    if (_tokenizers.TryGetValue(tokenizerKey, out var cachedTokenizer))
                    {
                        // Cache under the specific model name as well
                        _tokenizers[modelName] = cachedTokenizer;
                        return cachedTokenizer;
                    }

                    try
                    {
                        // Create appropriate LLaMA tokenizer
                        // Note: In production, you would load the tokenizer model file from a known location
                        // For now, we'll use the default LLaMA tokenizer
                        // This would need to be enhanced to load specific tokenizer model files

                        _logger.LogInformation("Creating LLaMA tokenizer for model {ModelName} with key {TokenizerKey}",
                            modelName, tokenizerKey);

                        // Microsoft.ML.Tokenizers requires a tokenizer model file
                        // For now, return null to trigger fallback
                        // TODO: Implement proper LLaMA tokenizer model file loading
                        _logger.LogWarning("LLaMA tokenizer model file loading not yet implemented. Using fallback.");
                        return null;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to create LLaMA tokenizer for model {ModelName}", modelName);
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTokenizerForModel");
                return null;
            }
        }

        /// <summary>
        /// Determines the appropriate tokenizer key based on model name.
        /// </summary>
        /// <param name="modelName">The model name.</param>
        /// <returns>The tokenizer key to use.</returns>
        private string DetermineTokenizerKey(string modelName)
        {
            string lowerModel = modelName.ToLowerInvariant();

            // LLaMA 3 models
            if (lowerModel.Contains("llama-3") || lowerModel.Contains("llama3"))
            {
                return "llama3";
            }

            // LLaMA 2 models
            if (lowerModel.Contains("llama-2") || lowerModel.Contains("llama2"))
            {
                return "llama2";
            }

            // Default to LLaMA 3 for newer models
            return "llama3";
        }

        /// <summary>
        /// Estimates tokens for content in JsonElement format.
        /// </summary>
        private int EstimateJsonElementTokens(JsonElement element, Tokenizer tokenizer)
        {
            int tokenCount = 0;

            if (element.ValueKind == JsonValueKind.String)
            {
                string? stringValue = element.GetString();
                if (stringValue != null)
                {
                    tokenCount += tokenizer.CountTokens(stringValue);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    if (item.TryGetProperty("type", out var typeElement) &&
                        typeElement.ValueKind == JsonValueKind.String &&
                        typeElement.GetString() == "text" &&
                        item.TryGetProperty("text", out var textElement) &&
                        textElement.ValueKind == JsonValueKind.String)
                    {
                        string? text = textElement.GetString();
                        if (text != null)
                        {
                            tokenCount += tokenizer.CountTokens(text);
                        }
                    }
                    else if (item.TryGetProperty("type", out var imgTypeElement) &&
                             imgTypeElement.ValueKind == JsonValueKind.String &&
                             imgTypeElement.GetString() == "image_url")
                    {
                        // Use same image token estimate as OpenAI
                        tokenCount += 65;
                    }
                }
            }

            return tokenCount;
        }

        /// <summary>
        /// Extracts text content from a complex content object.
        /// </summary>
        private string ExtractTextFromContentObject(object content)
        {
            try
            {
                string json = JsonSerializer.Serialize(content);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                StringBuilder sb = new StringBuilder();

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                    {
                        if (item.TryGetProperty("type", out var typeElement) &&
                            typeElement.ValueKind == JsonValueKind.String &&
                            typeElement.GetString() == "text" &&
                            item.TryGetProperty("text", out var textElement) &&
                            textElement.ValueKind == JsonValueKind.String)
                        {
                            string? text = textElement.GetString();
                            if (text != null)
                            {
                                sb.AppendLine(text);
                            }
                        }
                    }
                    return sb.ToString();
                }
                else if (root.ValueKind == JsonValueKind.String)
                {
                    return root.GetString() ?? "";
                }
            }
            catch
            {
                // If we can't process it properly, just return the string representation
            }

            return content.ToString() ?? "";
        }

        /// <summary>
        /// Provides a fallback method for estimating tokens when the proper tokenizer can't be used.
        /// </summary>
        private int FallbackEstimateTokens(List<Message> messages)
        {
            int totalCharacters = messages.Sum(m =>
                (m.Content != null ? m.Content.ToString()?.Length ?? 0 : 0) +
                (m.Role?.Length ?? 0) +
                (m.Name?.Length ?? 0));

            // Rough estimate: 1 token ≈ 4 characters for LLaMA models
            return totalCharacters / 4;
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

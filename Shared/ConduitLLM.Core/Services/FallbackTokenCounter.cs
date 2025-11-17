using System.Text;
using System.Text.Json;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Fallback token counter implementation for models without specific tokenizer support.
    /// Uses improved heuristics based on known model characteristics.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This fallback counter provides more accurate estimates than simple character-based counting
    /// by applying different ratios based on the model family's known characteristics.
    /// </para>
    /// <para>
    /// While not as accurate as using the actual tokenizer, this approach provides reasonable
    /// estimates for billing and context window management purposes.
    /// </para>
    /// </remarks>
    public class FallbackTokenCounter : ITokenCounter
    {
        private readonly ILogger<FallbackTokenCounter> _logger;
        private readonly IModelCapabilityService? _capabilityService;

        // Character-to-token ratios for different model families
        // Based on empirical observations and documented behavior
        private static readonly Dictionary<TokenizerType, double> CharactersPerToken = new()
        {
            // OpenAI models (cl100k_base, o200k_base)
            { TokenizerType.Cl100KBase, 4.0 },
            { TokenizerType.O200KBase, 4.0 },
            { TokenizerType.P50KBase, 4.0 },
            { TokenizerType.P50KEdit, 4.0 },
            { TokenizerType.R50KBase, 4.0 },

            // Anthropic Claude models - slightly more tokens per character
            { TokenizerType.Claude, 3.8 },
            { TokenizerType.Claude3, 3.8 },

            // Google models
            { TokenizerType.Gemini, 3.5 },
            { TokenizerType.PaLM, 3.5 },

            // Meta LLaMA models - similar to OpenAI
            { TokenizerType.LLaMA, 4.0 },
            { TokenizerType.LLaMA2, 4.0 },
            { TokenizerType.LLaMA3, 4.0 },

            // Mistral models
            { TokenizerType.Mistral, 4.0 },

            // Cohere models
            { TokenizerType.Cohere, 3.8 },

            // Other models
            { TokenizerType.Kimi, 3.5 },
            { TokenizerType.MiniMax, 3.5 },
            { TokenizerType.Groq, 4.0 },
            { TokenizerType.Cerebras, 4.0 },

            // Generic tokenizers
            { TokenizerType.BPE, 4.0 },
            { TokenizerType.SentencePiece, 4.0 },
            { TokenizerType.WordPiece, 3.5 },
            { TokenizerType.Tiktoken, 4.0 },

            // Default fallback
            { TokenizerType.None, 4.0 }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="FallbackTokenCounter"/> class.
        /// </summary>
        /// <param name="logger">The logger for recording diagnostic information.</param>
        /// <param name="capabilityService">Service for retrieving model capabilities from configuration.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
        public FallbackTokenCounter(ILogger<FallbackTokenCounter> logger, IModelCapabilityService? capabilityService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _capabilityService = capabilityService;
        }

        /// <inheritdoc />
        public async Task<int> EstimateTokenCountAsync(string modelName, List<Message> messages)
        {
            if (messages == null || messages.Count() == 0)
            {
                return 0;
            }

            try
            {
                var tokenizerType = await GetTokenizerTypeAsync(modelName);
                var charsPerToken = GetCharactersPerToken(tokenizerType);

                int tokenCount = 0;

                // Add overhead for message structure (similar to OpenAI's approach)
                tokenCount += messages.Count * 4; // Per-message overhead

                foreach (var message in messages)
                {
                    // Count role tokens
                    if (!string.IsNullOrEmpty(message.Role))
                    {
                        tokenCount += EstimateTokensFromText(message.Role, charsPerToken);
                    }

                    // Count content tokens
                    if (message.Content != null)
                    {
                        if (message.Content is string contentStr)
                        {
                            tokenCount += EstimateTokensFromText(contentStr, charsPerToken);
                        }
                        else if (message.Content is JsonElement jsonElement)
                        {
                            tokenCount += EstimateJsonElementTokens(jsonElement, charsPerToken);
                        }
                        else
                        {
                            string textContent = ExtractTextFromContentObject(message.Content);
                            tokenCount += EstimateTokensFromText(textContent, charsPerToken);
                        }
                    }

                    // Count name tokens
                    if (!string.IsNullOrEmpty(message.Name))
                    {
                        tokenCount += EstimateTokensFromText(message.Name, charsPerToken);
                        tokenCount += 1; // Additional overhead
                    }
                }

                // Add reply priming tokens
                tokenCount += 3;

                _logger.LogDebug(
                    "Fallback token estimation for model {ModelName} ({TokenizerType}): {TokenCount} tokens using {CharsPerToken} chars/token ratio",
                    modelName, tokenizerType, tokenCount, charsPerToken);

                return tokenCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in fallback token estimation for model {ModelName}", modelName);
                // Ultra-simple fallback
                return EstimateTokensFromMessages(messages, 4.0);
            }
        }

        /// <inheritdoc />
        public async Task<int> EstimateTokenCountAsync(string modelName, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            try
            {
                var tokenizerType = await GetTokenizerTypeAsync(modelName);
                var charsPerToken = GetCharactersPerToken(tokenizerType);

                int tokenCount = EstimateTokensFromText(text, charsPerToken);

                _logger.LogDebug(
                    "Fallback token estimation for model {ModelName} ({TokenizerType}): {TokenCount} tokens for {CharCount} chars",
                    modelName, tokenizerType, tokenCount, text.Length);

                return tokenCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in fallback token estimation for model {ModelName}", modelName);
                // Ultra-simple fallback
                return text.Length / 4;
            }
        }

        /// <summary>
        /// Gets the tokenizer type for a model from the capability service.
        /// </summary>
        private async Task<TokenizerType> GetTokenizerTypeAsync(string modelName)
        {
            if (_capabilityService == null)
            {
                _logger.LogWarning("ModelCapabilityService not available, using default tokenizer type");
                return TokenizerType.Cl100KBase; // Safe default
            }

            try
            {
                var tokenizerTypeStr = await _capabilityService.GetTokenizerTypeAsync(modelName);
                if (string.IsNullOrEmpty(tokenizerTypeStr))
                {
                    return TokenizerType.Cl100KBase;
                }

                if (Enum.TryParse<TokenizerType>(tokenizerTypeStr, true, out var tokenizerType))
                {
                    return tokenizerType;
                }

                _logger.LogWarning("Unknown tokenizer type '{TokenizerType}' for model {ModelName}, using default",
                    tokenizerTypeStr, modelName);
                return TokenizerType.Cl100KBase;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting tokenizer type for model {ModelName}, using default", modelName);
                return TokenizerType.Cl100KBase;
            }
        }

        /// <summary>
        /// Gets the characters-per-token ratio for a given tokenizer type.
        /// </summary>
        private double GetCharactersPerToken(TokenizerType tokenizerType)
        {
            if (CharactersPerToken.TryGetValue(tokenizerType, out var ratio))
            {
                return ratio;
            }

            _logger.LogDebug("No specific ratio for tokenizer type {TokenizerType}, using default 4.0", tokenizerType);
            return 4.0; // Safe default
        }

        /// <summary>
        /// Estimates token count from text using the given ratio.
        /// </summary>
        private int EstimateTokensFromText(string text, double charsPerToken)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            // Apply conservative rounding (ceiling) to avoid underestimation
            return Math.Max(1, (int)Math.Ceiling(text.Length / charsPerToken));
        }

        /// <summary>
        /// Estimates tokens for content in JsonElement format.
        /// </summary>
        private int EstimateJsonElementTokens(JsonElement element, double charsPerToken)
        {
            int tokenCount = 0;

            if (element.ValueKind == JsonValueKind.String)
            {
                string? stringValue = element.GetString();
                if (stringValue != null)
                {
                    tokenCount += EstimateTokensFromText(stringValue, charsPerToken);
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
                            tokenCount += EstimateTokensFromText(text, charsPerToken);
                        }
                    }
                    else if (item.TryGetProperty("type", out var imgTypeElement) &&
                             imgTypeElement.ValueKind == JsonValueKind.String &&
                             imgTypeElement.GetString() == "image_url")
                    {
                        // Standard image token estimate (low-res)
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
        /// Simple token estimation from messages using a fixed ratio.
        /// Ultra-simple fallback when everything else fails.
        /// </summary>
        private int EstimateTokensFromMessages(List<Message> messages, double charsPerToken)
        {
            int totalChars = messages.Sum(m =>
                (m.Content != null ? m.Content.ToString()?.Length ?? 0 : 0) +
                (m.Role?.Length ?? 0) +
                (m.Name?.Length ?? 0));

            return Math.Max(1, (int)Math.Ceiling(totalChars / charsPerToken));
        }
    }
}

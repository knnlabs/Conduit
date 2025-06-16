using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

using TiktokenSharp;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Token counter implementation using TiktokenSharp for OpenAI-compatible tokenization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The TiktokenCounter provides token counting functionality using the TiktokenSharp library,
    /// which implements OpenAI's tokenization algorithm. This service is essential for:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Accurately estimating token usage for cost calculation</description></item>
    ///   <item><description>Ensuring messages fit within model context windows</description></item>
    ///   <item><description>Determining appropriate chunking strategies for large content</description></item>
    /// </list>
    /// <para>
    /// This implementation provides robust handling for different content types including:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Simple string content</description></item>
    ///   <item><description>Multimodal content with text and images</description></item>
    ///   <item><description>Complex JSON structures</description></item>
    /// </list>
    /// <para>
    /// When the exact encoding for a model cannot be determined, or when tokenization fails,
    /// this implementation falls back to a simple character-based estimation (approximate 4 characters per token).
    /// </para>
    /// </remarks>
    public class TiktokenCounter : ITokenCounter
    {
        // Cache encodings for performance
        private static readonly Dictionary<string, TikToken> _encodings = new();
        private static readonly object _lock = new();
        private readonly ILogger<TiktokenCounter> _logger;
        private readonly IModelCapabilityService? _capabilityService;

        /// <summary>
        /// Initializes a new instance of the <see cref="TiktokenCounter"/> class.
        /// </summary>
        /// <param name="logger">The logger for recording diagnostic information.</param>
        /// <param name="capabilityService">Service for retrieving model capabilities from configuration.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
        public TiktokenCounter(ILogger<TiktokenCounter> logger, IModelCapabilityService? capabilityService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _capabilityService = capabilityService;

            if (capabilityService == null)
            {
                _logger.LogWarning("ModelCapabilityService not available, using fallback tokenizer detection");
            }
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
                            if (message.Content is string contentStr)
                            {
                                // Simple string content
                                tokenCount += encoding.Encode(contentStr).Count;
                            }
                            else if (message.Content is JsonElement jsonElement)
                            {
                                // Handle JsonElement (common when deserialized from JSON)
                                tokenCount += EstimateJsonElementTokens(jsonElement, encoding);
                            }
                            else
                            {
                                // Try to handle content parts or other objects
                                string textContent = ExtractTextFromContentObject(message.Content);
                                tokenCount += encoding.Encode(textContent).Count;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error encoding content. Using fallback estimate.");
                            // Fallback calculation
                            string contentStr = message.Content.ToString() ?? "";
                            tokenCount += contentStr.Length / 4;
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
        /// <param name="modelName">The name of the model to get encoding for.</param>
        /// <returns>The appropriate TikToken encoding, or null if it cannot be determined.</returns>
        /// <remarks>
        /// <para>
        /// This method determines the appropriate encoding based on the model name using these steps:
        /// </para>
        /// <list type="number">
        ///   <item><description>Identifies the encoding type based on model name patterns</description></item>
        ///   <item><description>Uses a thread-safe caching mechanism to avoid repeatedly creating encodings</description></item>
        ///   <item><description>Falls back to the most modern encoding (cl100k_base) when uncertain</description></item>
        /// </list>
        /// <para>
        /// The current encoding mappings are:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>cl100k_base: GPT-3.5 and GPT-4 models</description></item>
        ///   <item><description>p50k_base: Legacy models (davinci, curie, babbage, ada)</description></item>
        /// </list>
        /// </remarks>
        private TikToken? GetEncodingForModel(string modelName)
        {
            try
            {
                string encodingName = "cl100k_base"; // Default for newer models

                // Try to get tokenizer type from capability service first
                if (_capabilityService != null)
                {
                    try
                    {
                        var tokenizerType = _capabilityService.GetTokenizerTypeAsync(modelName).GetAwaiter().GetResult();
                        if (!string.IsNullOrEmpty(tokenizerType))
                        {
                            encodingName = tokenizerType;
                            _logger.LogDebug("Using tokenizer {TokenizerType} from capability service for model {Model}", tokenizerType, modelName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error getting tokenizer type from capability service for model {Model}", modelName);
                    }
                }

                // Map non-OpenAI tokenizer types to their closest OpenAI equivalent
                // since TiktokenSharp only supports OpenAI encodings
                if (encodingName == "claude" || encodingName == "gemini")
                {
                    // Use cl100k_base as approximation for non-OpenAI models
                    _logger.LogDebug("Using cl100k_base approximation for {TokenizerType} tokenizer on model {Model}", encodingName, modelName);
                    encodingName = "cl100k_base";
                }
                else if (encodingName == "o200k_base")
                {
                    // o200k_base is newer than cl100k_base, but if not supported, fall back
                    // Try to use it, but we'll handle the error below if it's not supported
                    _logger.LogDebug("Attempting to use o200k_base tokenizer for model {Model}", modelName);
                }

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
                            _logger.LogWarning(ex, "Failed to get encoding {EncodingName} for model {ModelName}, trying cl100k_base fallback", encodingName, modelName);

                            // Try fallback to cl100k_base if the specific encoding isn't supported
                            if (encodingName != "cl100k_base")
                            {
                                try
                                {
                                    encodingName = "cl100k_base";
                                    encoding = TikToken.EncodingForModel(encodingName);
                                    _encodings[encodingName] = encoding;
                                    _logger.LogInformation("Successfully used cl100k_base fallback for model {ModelName}", modelName);
                                }
                                catch (Exception fallbackEx)
                                {
                                    _logger.LogError(fallbackEx, "Failed to get fallback encoding cl100k_base");
                                    return null;
                                }
                            }
                            else
                            {
                                return null;
                            }
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
        /// Estimates tokens for content in JsonElement format (common when deserializing JSON).
        /// </summary>
        /// <param name="element">The JsonElement to estimate token count for.</param>
        /// <param name="encoding">The tokenizer encoding to use.</param>
        /// <returns>The estimated token count.</returns>
        /// <remarks>
        /// <para>
        /// This method handles different types of JsonElement content:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>String elements: Directly tokenized</description></item>
        ///   <item><description>Arrays: Processes each element, especially for content parts</description></item>
        ///   <item><description>Text content parts: Extracts and tokenizes text with type="text"</description></item>
        ///   <item><description>Image content parts: Uses fixed token estimates based on type="image_url"</description></item>
        /// </list>
        /// <para>
        /// For image tokens, the implementation uses a fixed estimate since actual image token
        /// usage depends on resolution which isn't always available at counting time.
        /// </para>
        /// </remarks>
        private int EstimateJsonElementTokens(JsonElement element, TikToken encoding)
        {
            int tokenCount = 0;

            if (element.ValueKind == JsonValueKind.String)
            {
                string? stringValue = element.GetString();
                if (stringValue != null)
                {
                    tokenCount += encoding.Encode(stringValue).Count;
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                // For arrays (like content parts), process each element
                foreach (var item in element.EnumerateArray())
                {
                    // Check if it's a text content part
                    if (item.TryGetProperty("type", out var typeElement) &&
                        typeElement.ValueKind == JsonValueKind.String &&
                        typeElement.GetString() == "text" &&
                        item.TryGetProperty("text", out var textElement) &&
                        textElement.ValueKind == JsonValueKind.String)
                    {
                        // It's a text content part
                        string? text = textElement.GetString();
                        if (text != null)
                        {
                            tokenCount += encoding.Encode(text).Count;
                        }
                    }
                    else if (item.TryGetProperty("type", out var imgTypeElement) &&
                             imgTypeElement.ValueKind == JsonValueKind.String &&
                             imgTypeElement.GetString() == "image_url")
                    {
                        // For image tokens, OpenAI uses a formula based on resolution
                        // As a base, we'll add a fixed count that's average for a medium-res image
                        // High-res images actually use more tokens than text in the same message
                        tokenCount += 65; // An average low-res image cost
                    }
                }
            }

            return tokenCount;
        }

        /// <summary>
        /// Extracts text content from a complex content object.
        /// </summary>
        /// <param name="content">The content object to extract text from.</param>
        /// <returns>A string representation of the textual content.</returns>
        /// <remarks>
        /// <para>
        /// This method handles various content object formats:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>Serializes the object to JSON then parses it as a JsonDocument</description></item>
        ///   <item><description>For arrays (likely content parts): extracts text from each part with type="text"</description></item>
        ///   <item><description>For string values: returns them directly</description></item>
        ///   <item><description>For other types: falls back to ToString()</description></item>
        /// </list>
        /// <para>
        /// This extraction is particularly useful for multimodal content where we need to
        /// extract only the textual parts for token counting.
        /// </para>
        /// </remarks>
        private string ExtractTextFromContentObject(object content)
        {
            try
            {
                // First try to serialize the content to JSON
                string json = JsonSerializer.Serialize(content);

                // Then parse it as a JsonElement to use our existing logic
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                StringBuilder sb = new StringBuilder();

                // If it's an array, likely it's content parts
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
                            // It's a text content part
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
        /// Provides a fallback method for estimating tokens when the proper encoder can't be used.
        /// </summary>
        /// <param name="messages">The list of messages to estimate token count for.</param>
        /// <returns>The estimated token count.</returns>
        /// <remarks>
        /// <para>
        /// This method uses a simple character-based approximation when the proper tokenizer
        /// cannot be used. It follows these steps:
        /// </para>
        /// <list type="number">
        ///   <item><description>Counts the total characters across all message parts (content, role, name)</description></item>
        ///   <item><description>Divides by 4 to approximate tokens (based on the heuristic that English text averages ~4 chars per token)</description></item>
        /// </list>
        /// <para>
        /// While not as accurate as proper tokenization, this method provides a reasonable
        /// estimate when the correct encoder is unavailable or fails.
        /// </para>
        /// </remarks>
        private int FallbackEstimateTokens(List<Message> messages)
        {
            // Very rough estimation based on characters
            int totalCharacters = messages.Sum(m =>
                (m.Content != null ? m.Content.ToString()?.Length ?? 0 : 0) +
                (m.Role?.Length ?? 0) +
                (m.Name?.Length ?? 0));

            // Rough estimate: 1 token ≈ 4 characters in English
            return totalCharacters / 4;
        }

        /// <summary>
        /// Provides a fallback method for estimating tokens for a single text string.
        /// </summary>
        /// <param name="text">The text to estimate token count for.</param>
        /// <returns>The estimated token count.</returns>
        /// <remarks>
        /// <para>
        /// This method provides a simple character-based approximation when the proper tokenizer
        /// cannot be used. It divides the character count by 4, which is a reasonable
        /// approximation for English text (average ~4 characters per token).
        /// </para>
        /// <para>
        /// While not as accurate as proper tokenization, this method provides a reasonable
        /// fallback when the correct encoder is unavailable or fails.
        /// </para>
        /// </remarks>
        private int FallbackEstimateTokens(string text)
        {
            // Rough approximation: average 4 characters per token
            return text.Length / 4;
        }
    }
}

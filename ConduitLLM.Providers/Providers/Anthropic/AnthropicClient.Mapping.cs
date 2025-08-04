using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.Helpers;
using ConduitLLM.Providers.Providers.Anthropic.Models;
using ConduitLLM.Providers.Utilities;

using Microsoft.Extensions.Logging;
using CoreModels = ConduitLLM.Core.Models;

namespace ConduitLLM.Providers.Providers.Anthropic
{
    /// <summary>
    /// AnthropicClient partial class containing request/response mapping functionality.
    /// </summary>
    public partial class AnthropicClient
    {
        /// <summary>
        /// Maps the provider-agnostic request to Anthropic API format.
        /// </summary>
        /// <param name="request">The generic chat completion request to map.</param>
        /// <returns>An Anthropic-compatible message request.</returns>
        /// <remarks>
        /// <para>
        /// This method handles several key transformations specific to the Anthropic API:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>Extracts system messages and moves them to Anthropic's dedicated system prompt field</description></item>
        ///   <item><description>Processes user and assistant messages into the format expected by Anthropic</description></item>
        ///   <item><description>Handles tool calls using Anthropic's content blocks format</description></item>
        ///   <item><description>Manages tool responses (tool_result) with the appropriate format</description></item>
        ///   <item><description>Maps common parameters like temperature and top_p</description></item>
        /// </list>
        /// <para>
        /// Special handling is implemented for multimodal content and tool usage, converting
        /// from the provider-agnostic format to Anthropic's content blocks structure.
        /// </para>
        /// </remarks>
        private AnthropicMessageRequest MapToAnthropicRequest(CoreModels.ChatCompletionRequest request)
        {
            string systemPrompt = "";

            // Extract the system message if present
            var userAndAssistantMessages = new List<AnthropicMessage>();
            foreach (var message in request.Messages)
            {
                if (message.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                {
                    // Anthropic uses a dedicated system prompt field instead of a message
                    systemPrompt = ContentHelper.GetContentAsString(message.Content);
                }
                else if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ||
                         message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    // Process standard messages
                    AnthropicMessage anthropicMessage;

                    // Handle tool calls and results if present
                    if (message.ToolCalls != null && message.ToolCalls.Count > 0)
                    {
                        // Convert to Anthropic's content format
                        var contentParts = ContentHelper.ExtractMultimodalContent(message.Content ?? "");
                        var contentBlocks = contentParts.Select(part =>
                            new AnthropicContentBlock { Type = "text", Text = part }).ToList();

                        // Add tool calls at the end
                        foreach (var toolCall in message.ToolCalls)
                        {
                            contentBlocks.Add(new AnthropicContentBlock
                            {
                                Type = "tool_use",
                                Id = toolCall.Id,
                                Name = toolCall.Function?.Name ?? "",
                                Input = toolCall.Function?.Arguments ?? "{}"
                            });
                        }

                        anthropicMessage = new AnthropicMessage
                        {
                            Role = message.Role.ToLowerInvariant(),
                            Content = contentBlocks
                        };
                    }
                    else if (message.ToolCallId != null)
                    {
                        // Tool result message
                        anthropicMessage = new AnthropicMessage
                        {
                            Role = message.Role.ToLowerInvariant(),
                            Content = new List<AnthropicContentBlock> {
                                new AnthropicContentBlock {
                                    Type = "tool_result",
                                    ToolCallId = message.ToolCallId,
                                    Content = ContentHelper.GetContentAsString(message.Content)
                                }
                            }
                        };
                    }
                    else if (!ContentHelper.IsTextOnly(message.Content))
                    {
                        // Multimodal message with images
                        var contentBlocks = MapToAnthropicContentBlocks(message.Content);

                        anthropicMessage = new AnthropicMessage
                        {
                            Role = message.Role.ToLowerInvariant(),
                            Content = contentBlocks
                        };
                    }
                    else
                    {
                        // Standard text message
                        anthropicMessage = new AnthropicMessage
                        {
                            Role = message.Role.ToLowerInvariant(),
                            Content = ContentHelper.GetContentAsString(message.Content)
                        };
                    }

                    userAndAssistantMessages.Add(anthropicMessage);
                }
                // Ignore any other role types (like function)
            }

            // Create the Anthropic request
            var anthropicRequest = new AnthropicMessageRequest
            {
                Model = ProviderModelId,
                Messages = userAndAssistantMessages,
                SystemPrompt = !string.IsNullOrEmpty(systemPrompt) ? systemPrompt : null,
                MaxTokens = request.MaxTokens ?? 4096, // Default max tokens if not specified
                Temperature = ParameterConverter.ToTemperature(request.Temperature),
                TopP = ParameterConverter.ToProbability(request.TopP, 0.0, 1.0),
                TopK = request.TopK,
                Stream = request.Stream ?? false,
                StopSequences = request.Stop,
                Metadata = request.User != null ? new AnthropicMetadata { UserId = request.User } : null
            };

            return anthropicRequest;
        }

        /// <summary>
        /// Maps multimodal content to Anthropic's content blocks format.
        /// </summary>
        /// <param name="content">The content object which may contain text and images</param>
        /// <returns>A list of Anthropic content blocks</returns>
        private List<AnthropicContentBlock> MapToAnthropicContentBlocks(object? content)
        {
            var contentBlocks = new List<AnthropicContentBlock>();

            if (content == null)
                return contentBlocks;

            // Add text content blocks
            var textParts = ContentHelper.ExtractMultimodalContent(content);
            foreach (var text in textParts)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    contentBlocks.Add(new AnthropicContentBlock
                    {
                        Type = "text",
                        Text = text
                    });
                }
            }

            // Add image content blocks
            var imageUrls = ContentHelper.ExtractImageUrls(content);
            foreach (var imageUrl in imageUrls)
            {
                // Anthropic requires base64 images, so make sure we have a base64 data URL
                if (imageUrl.IsBase64DataUrl)
                {
                    // Extract the MIME type and base64 data
                    var mimeType = imageUrl.MimeType;
                    var base64Data = imageUrl.Base64Data;

                    if (!string.IsNullOrEmpty(mimeType) && !string.IsNullOrEmpty(base64Data))
                    {
                        contentBlocks.Add(new AnthropicContentBlock
                        {
                            Type = "image",
                            Source = new AnthropicImageSource
                            {
                                Type = "base64",
                                MediaType = mimeType,
                                Data = base64Data
                            }
                        });
                    }
                }
                else
                {
                    // If it's a URL, we need to download it and convert to base64
                    // We'll use the async method, but make it synchronous for this method
                    try
                    {
                        var imageData = ImageUtility.DownloadImageAsync(imageUrl.Url)
                            .ConfigureAwait(false).GetAwaiter().GetResult();

                        // Try to determine the MIME type
                        string mimeType = "image/jpeg"; // Default fallback
                        if (imageData.Length >= 2)
                        {
                            if (imageData[0] == 0xFF && imageData[1] == 0xD8) // JPEG
                                mimeType = "image/jpeg";
                            else if (imageData.Length >= 8 &&
                                    imageData[0] == 0x89 && imageData[1] == 0x50 &&
                                    imageData[2] == 0x4E && imageData[3] == 0x47) // PNG
                                mimeType = "image/png";
                            else if (imageData.Length >= 3 &&
                                    imageData[0] == 0x47 && imageData[1] == 0x49 &&
                                    imageData[2] == 0x46) // GIF
                                mimeType = "image/gif";
                            else if (imageData.Length >= 4 &&
                                    (imageData[0] == 0x42 && imageData[1] == 0x4D)) // BMP
                                mimeType = "image/bmp";
                        }

                        // Convert to base64
                        var base64Data = Convert.ToBase64String(imageData);

                        contentBlocks.Add(new AnthropicContentBlock
                        {
                            Type = "image",
                            Source = new AnthropicImageSource
                            {
                                Type = "base64",
                                MediaType = mimeType,
                                Data = base64Data
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to download and convert image from URL: {Url}", imageUrl.Url);
                        // Skip this image
                    }
                }
            }

            return contentBlocks;
        }

        /// <summary>
        /// Maps the Anthropic API response to provider-agnostic format.
        /// </summary>
        /// <param name="response">The response from the Anthropic API.</param>
        /// <param name="originalModelAlias">The original model alias from the request.</param>
        /// <returns>A provider-agnostic chat completion response.</returns>
        /// <remarks>
        /// <para>
        /// This method transforms the Anthropic-specific response structure into the standardized
        /// format used throughout the application. Key transformations include:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>Converting Anthropic's content blocks into a single content string and/or tool calls</description></item>
        ///   <item><description>Mapping Anthropic's usage metrics (input_tokens, output_tokens) to standard format</description></item>
        ///   <item><description>Preserving the original model alias if it was different from the provider model ID</description></item>
        ///   <item><description>Standardizing object types and structure to match the OpenAI-like format used across the application</description></item>
        /// </list>
        /// <para>
        /// When processing content, this method handles both string content and content blocks,
        /// extracting text blocks and tool use blocks into their respective formats.
        /// </para>
        /// </remarks>
        private CoreModels.ChatCompletionResponse MapFromAnthropicResponse(
            AnthropicMessageResponse response,
            string? originalModelAlias)
        {
            string responseContent = "";
            List<CoreModels.ToolCall>? toolCalls = null;

            // Process the content of the response, which could be a string or blocks
            if (response.Content is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                var textContent = new StringBuilder();
                var toolUseBlocks = new List<JsonElement>();

                // Process each content block
                foreach (JsonElement block in jsonElement.EnumerateArray())
                {
                    if (block.TryGetProperty("type", out var typeElement))
                    {
                        string? blockType = typeElement.GetString();

                        if (blockType == "text" && block.TryGetProperty("text", out var textElement))
                        {
                            textContent.Append(textElement.GetString());
                        }
                        else if (blockType == "tool_use")
                        {
                            toolUseBlocks.Add(block);
                        }
                    }
                }

                responseContent = textContent.ToString();

                // Process tool calls if any
                if (toolUseBlocks.Count > 0)
                {
                    toolCalls = new List<CoreModels.ToolCall>();

                    foreach (var block in toolUseBlocks)
                    {
                        string? id = null;
                        string? name = null;
                        string? input = null;

                        if (block.TryGetProperty("id", out var idElement))
                            id = idElement.GetString();

                        if (block.TryGetProperty("name", out var nameElement))
                            name = nameElement.GetString();

                        if (block.TryGetProperty("input", out var inputElement))
                            input = inputElement.GetString();

                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                        {
                            toolCalls.Add(new CoreModels.ToolCall
                            {
                                Id = id,
                                Type = "function", // Standardize as "function" in our API
                                Function = new CoreModels.FunctionCall
                                {
                                    Name = name,
                                    Arguments = input ?? "{}"
                                }
                            });
                        }
                    }
                }
            }
            else
            {
                // Fallback for string content or other content types
                responseContent = ContentHelper.GetContentAsString(response.Content);
            }

            // Create the standardized response
            var result = new CoreModels.ChatCompletionResponse
            {
                Id = response.Id ?? Guid.NewGuid().ToString(),
                Object = "chat.completion", // Standardize as OpenAI-like type
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = response.Model ?? "unknown-model",
                Choices = new List<CoreModels.Choice>
                {
                    new CoreModels.Choice
                    {
                        Index = 0,
                        Message = new CoreModels.Message
                        {
                            Role = "assistant",
                            Content = responseContent ?? string.Empty,
                            ToolCalls = toolCalls
                        },
                        FinishReason = response.StopReason ?? "unknown"
                    }
                },
                Usage = new CoreModels.Usage
                {
                    PromptTokens = response.Usage.InputTokens,
                    CompletionTokens = response.Usage.OutputTokens,
                    TotalTokens = response.Usage.InputTokens + response.Usage.OutputTokens
                },
                OriginalModelAlias = originalModelAlias
            };

            return result;
        }
    }
}
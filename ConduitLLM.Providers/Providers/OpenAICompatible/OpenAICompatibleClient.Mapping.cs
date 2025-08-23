using Microsoft.Extensions.Logging;
using CoreModels = ConduitLLM.Core.Models;
using ConduitLLM.Providers.OpenAI;
using ProviderHelpers = ConduitLLM.Providers.Helpers;
using ConduitLLM.Providers.Utilities;

namespace ConduitLLM.Providers.OpenAICompatible
{
    /// <summary>
    /// OpenAICompatibleClient partial class containing request/response mapping functionality.
    /// </summary>
    public abstract partial class OpenAICompatibleClient
    {
        /// <summary>
        /// Maps the provider-agnostic request to OpenAI format.
        /// </summary>
        /// <param name="request">The provider-agnostic request.</param>
        /// <returns>An object representing the OpenAI-formatted request.</returns>
        /// <remarks>
        /// This method maps the generic request to the format expected by OpenAI-compatible APIs.
        /// Derived classes can override this method to provide custom mapping.
        /// </remarks>
        protected virtual object MapToOpenAIRequest(CoreModels.ChatCompletionRequest request)
        {
            // Map tools if present
            List<object>? openAiTools = null;
            if (request.Tools != null && request.Tools.Count() > 0)
            {
                openAiTools = request.Tools.Select(t => new
                {
                    type = t.Type ?? "function",
                    function = new
                    {
                        name = t.Function?.Name ?? "unknown",
                        description = t.Function?.Description,
                        parameters = t.Function?.Parameters
                    }
                }).Cast<object>().ToList();
            }

            // Map tool choice if present
            object? openAiToolChoice = null;
            if (request.ToolChoice != null)
            {
                // Use the GetSerializedValue method to get the properly formatted object
                openAiToolChoice = request.ToolChoice;
            }

            // Map messages with their content - handle multimodal content for vision models
            var messages = request.Messages.Select(m =>
            {
                // Check if this is a multimodal message
                if (ProviderHelpers.ContentHelper.IsTextOnly(m.Content))
                {
                    // Simple text-only message
                    return new OpenAIMessage
                    {
                        Role = m.Role,
                        Content = ProviderHelpers.ContentHelper.GetContentAsString(m.Content),
                        Name = m.Name,
                        ToolCalls = m.ToolCalls?.Select(tc => new
                        {
                            id = tc.Id,
                            type = tc.Type ?? "function",
                            function = new
                            {
                                name = tc.Function?.Name,
                                arguments = tc.Function?.Arguments
                            }
                        }).Cast<object>().ToList(),
                        ToolCallId = m.ToolCallId
                    };
                }
                else
                {
                    // Multimodal message with potential images
                    return new OpenAIMessage
                    {
                        Role = m.Role,
                        Content = MapMultimodalContent(m.Content),
                        Name = m.Name,
                        ToolCalls = m.ToolCalls?.Select(tc => new
                        {
                            id = tc.Id,
                            type = tc.Type ?? "function",
                            function = new
                            {
                                name = tc.Function?.Name,
                                arguments = tc.Function?.Arguments
                            }
                        }).Cast<object>().ToList(),
                        ToolCallId = m.ToolCallId
                    };
                }
            }).ToList();

            // Create the OpenAI request as a dictionary to support extension data
            var openAiRequest = new Dictionary<string, object?>
            {
                ["model"] = ProviderModelId,  // Always use the provider's model ID, not the alias
                ["messages"] = messages
            };
            
            // Add optional standard parameters
            if (request.MaxTokens != null)
                openAiRequest["max_tokens"] = request.MaxTokens;
            if (request.Temperature != null)
                openAiRequest["temperature"] = ParameterConverter.ToTemperature(request.Temperature);
            if (request.TopP != null)
                openAiRequest["top_p"] = ParameterConverter.ToProbability(request.TopP, 0.0, 1.0);
            if (request.N != null)
                openAiRequest["n"] = request.N;
            if (request.Stop != null)
                openAiRequest["stop"] = ParameterConverter.ConvertStopSequences(request.Stop);
            if (request.PresencePenalty != null)
                openAiRequest["presence_penalty"] = ParameterConverter.ToProbability(request.PresencePenalty);
            if (request.FrequencyPenalty != null)
                openAiRequest["frequency_penalty"] = ParameterConverter.ToProbability(request.FrequencyPenalty);
            if (request.LogitBias != null)
                openAiRequest["logit_bias"] = ParameterConverter.ConvertLogitBias(request.LogitBias);
            if (request.User != null)
                openAiRequest["user"] = request.User;
            if (request.Seed != null)
                openAiRequest["seed"] = request.Seed;
            if (openAiTools != null)
                openAiRequest["tools"] = openAiTools;
            if (openAiToolChoice != null)
                openAiRequest["tool_choice"] = openAiToolChoice;
            // Only send ResponseFormat if explicitly requested and not "text" (default)
            // Some providers like SambaNova don't support response_format with type "text"
            if (request.ResponseFormat != null && request.ResponseFormat.Type != "text")
                openAiRequest["response_format"] = new ResponseFormat { Type = request.ResponseFormat.Type ?? "text" };
            if (request.Stream != null)
                openAiRequest["stream"] = request.Stream;
                
            // Pass through any extension data (model-specific parameters)
            if (request.ExtensionData != null)
            {
                foreach (var kvp in request.ExtensionData)
                {
                    // Don't override standard parameters
                    if (!openAiRequest.ContainsKey(kvp.Key))
                    {
                        openAiRequest[kvp.Key] = kvp.Value;
                    }
                }
            }
            
            return openAiRequest;
        }

        /// <summary>
        /// Maps multimodal content to the format expected by OpenAI's API.
        /// </summary>
        /// <param name="content">The content object which may contain text and images</param>
        /// <returns>A properly formatted list of content parts for OpenAI</returns>
        protected virtual object MapMultimodalContent(object? content)
        {
            if (content == null)
                return "";

            if (content is string textContent)
                return textContent;

            // Create a list to hold the formatted content parts
            var contentParts = new List<object>();

            // Extract text parts
            var textParts = ProviderHelpers.ContentHelper.ExtractMultimodalContent(content);
            foreach (var text in textParts)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    contentParts.Add(new
                    {
                        type = "text",
                        text = text
                    });
                }
            }

            // Extract image URLs
            var imageUrls = ProviderHelpers.ContentHelper.ExtractImageUrls(content);
            foreach (var imageUrl in imageUrls)
            {
                contentParts.Add(new
                {
                    type = "image_url",
                    image_url = new
                    {
                        url = imageUrl.Url,
                        detail = string.IsNullOrEmpty(imageUrl.Detail) ? "auto" : imageUrl.Detail
                    }
                });
            }

            // If no parts were added, return an empty string
            if (contentParts.Count() == 0)
                return "";

            return contentParts;
        }

        /// <summary>
        /// Maps the OpenAI response to provider-agnostic format.
        /// </summary>
        /// <param name="responseObj">The response from the OpenAI API.</param>
        /// <param name="originalModelAlias">The original model alias from the request.</param>
        /// <returns>A provider-agnostic chat completion response.</returns>
        /// <remarks>
        /// This method maps the OpenAI-formatted response to the generic format used by the application.
        /// Derived classes can override this method to provide custom mapping.
        /// </remarks>
        protected virtual CoreModels.ChatCompletionResponse MapFromOpenAIResponse(
            object responseObj,
            string? originalModelAlias)
        {
            if (responseObj == null)
            {
                Logger.LogError("Received null response from OpenAI-compatible provider");
                return CreateEmptyResponse(originalModelAlias);
            }

            // Cast to the strongly-typed response
            var response = responseObj as OpenAIChatCompletionResponse;
            if (response == null)
            {
                Logger.LogError("Response is not of expected type OpenAIChatCompletionResponse. Type: {Type}", 
                    responseObj.GetType()?.FullName ?? "null");
                return CreateEmptyResponse(originalModelAlias);
            }

            try
            {
                // Map the strongly-typed response
                return new CoreModels.ChatCompletionResponse
                {
                    Id = response.Id ?? Guid.NewGuid().ToString(),
                    Object = response.Object ?? "chat.completion",
                    Created = response.Created ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Model = originalModelAlias ?? response.Model ?? "unknown",
                    Choices = response.Choices?.Select(c => new CoreModels.Choice
                    {
                        Index = c.Index,
                        FinishReason = c.FinishReason ?? "stop",
                        Message = c.Message != null ? new CoreModels.Message
                        {
                            Role = c.Message.Role ?? "assistant",
                            Content = c.Message.Content
                        } : new CoreModels.Message
                        {
                            Role = "assistant",
                            Content = null
                        }
                    }).ToList() ?? new List<CoreModels.Choice>(),
                    Usage = response.Usage != null ? new CoreModels.Usage
                    {
                        PromptTokens = response.Usage.PromptTokens,
                        CompletionTokens = response.Usage.CompletionTokens,
                        TotalTokens = response.Usage.TotalTokens
                    } : null,
                    SystemFingerprint = response.SystemFingerprint,
                    Seed = response.Seed,
                    OriginalModelAlias = originalModelAlias
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error mapping OpenAI response: {Message}", ex.Message);
                return CreateEmptyResponse(originalModelAlias);
            }
        }

        /// <summary>
        /// Creates an empty chat completion response for error cases.
        /// </summary>
        /// <param name="originalModelAlias">The original model alias from the request.</param>
        /// <returns>An empty chat completion response.</returns>
        private CoreModels.ChatCompletionResponse CreateEmptyResponse(string? originalModelAlias)
        {
            return new CoreModels.ChatCompletionResponse
            {
                Id = Guid.NewGuid().ToString(),
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = originalModelAlias ?? ProviderModelId,
                Choices = new List<CoreModels.Choice>(),
                OriginalModelAlias = originalModelAlias
            };
        }

    }
}
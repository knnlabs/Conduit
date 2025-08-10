using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;
using CoreModels = ConduitLLM.Core.Models;
using OpenAIModels = ConduitLLM.Providers.OpenAI;
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
            if (request.Tools != null && request.Tools.Count > 0)
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
                    return new OpenAIModels.OpenAIMessage
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
                    return new OpenAIModels.OpenAIMessage
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

            // Create the OpenAI request
            return new OpenAIModels.OpenAIChatCompletionRequest
            {
                Model = ProviderModelId,  // Always use the provider's model ID, not the alias
                Messages = messages,
                MaxTokens = request.MaxTokens,
                Temperature = ParameterConverter.ToTemperature(request.Temperature),
                TopP = ParameterConverter.ToProbability(request.TopP, 0.0, 1.0),
                N = request.N,
                Stop = ParameterConverter.ConvertStopSequences(request.Stop),
                PresencePenalty = ParameterConverter.ToProbability(request.PresencePenalty),
                FrequencyPenalty = ParameterConverter.ToProbability(request.FrequencyPenalty),
                LogitBias = ParameterConverter.ConvertLogitBias(request.LogitBias),
                User = request.User,
                Seed = request.Seed,
                Tools = openAiTools,
                ToolChoice = openAiToolChoice,
                ResponseFormat = request.ResponseFormat != null ? new OpenAIModels.ResponseFormat { Type = request.ResponseFormat.Type ?? "text" } : new OpenAIModels.ResponseFormat { Type = "text" },
                Stream = request.Stream ?? false
            };
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
            if (contentParts.Count == 0)
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
            // Cast using dynamic to avoid multiple type-specific methods
            dynamic response = responseObj;

            try
            {
                // Create the basic response with required fields
                var result = CreateBasicChatCompletionResponse(response, originalModelAlias);

                // Add optional properties if they exist
                result = AddOptionalResponseProperties(result, response);

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error mapping OpenAI response: {Message}", ex.Message);

                // Create a minimal response with as much data as we can salvage
                return CreateFallbackChatCompletionResponse(response, originalModelAlias);
            }
        }

        /// <summary>
        /// Creates a basic chat completion response with required fields.
        /// </summary>
        /// <param name="response">The dynamic response from the provider.</param>
        /// <param name="originalModelAlias">The original model alias from the request.</param>
        /// <returns>A basic chat completion response.</returns>
        private CoreModels.ChatCompletionResponse CreateBasicChatCompletionResponse(
            dynamic response,
            string? originalModelAlias)
        {
            return new CoreModels.ChatCompletionResponse
            {
                Id = response.Id,
                Object = response.Object,
                Created = response.Created,
                Model = originalModelAlias ?? response.Model,
                Choices = MapDynamicChoices(response.Choices),
                Usage = MapUsage(response.Usage),
                OriginalModelAlias = originalModelAlias
            };
        }

        /// <summary>
        /// Creates a fallback chat completion response when the normal mapping fails.
        /// </summary>
        /// <param name="response">The dynamic response from the provider.</param>
        /// <param name="originalModelAlias">The original model alias from the request.</param>
        /// <returns>A minimal chat completion response.</returns>
        private CoreModels.ChatCompletionResponse CreateFallbackChatCompletionResponse(
            dynamic response,
            string? originalModelAlias)
        {
            try
            {
                // Attempt to create a basic response with as much as we can extract
                return new CoreModels.ChatCompletionResponse
                {
                    Id = TryGetProperty(response, "Id", Guid.NewGuid().ToString()),
                    Object = TryGetProperty(response, "Object", "chat.completion"),
                    Created = TryGetProperty(response, "Created", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    Model = originalModelAlias ?? TryGetProperty(response, "Model", ProviderModelId),
                    Choices = new List<CoreModels.Choice>(),
                    OriginalModelAlias = originalModelAlias
                };
            }
            catch
            {
                // Absolute fallback if everything fails
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

        /// <summary>
        /// Maps the usage information from a dynamic response.
        /// </summary>
        /// <param name="usageInfo">The dynamic usage information.</param>
        /// <returns>A strongly-typed Usage object, or null if the input is null.</returns>
        private CoreModels.Usage? MapUsage(dynamic usageInfo)
        {
            if (usageInfo == null)
            {
                return null;
            }

            try
            {
                return new CoreModels.Usage
                {
                    PromptTokens = usageInfo.PromptTokens,
                    CompletionTokens = usageInfo.CompletionTokens,
                    TotalTokens = usageInfo.TotalTokens
                };
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error mapping usage information: {Message}", ex.Message);
                return null;
            }
        }
    }
}
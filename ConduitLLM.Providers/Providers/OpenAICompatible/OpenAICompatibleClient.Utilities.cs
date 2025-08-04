using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using CoreModels = ConduitLLM.Core.Models;

namespace ConduitLLM.Providers.Providers.OpenAICompatible
{
    /// <summary>
    /// OpenAICompatibleClient partial class containing utility and helper methods.
    /// </summary>
    public abstract partial class OpenAICompatibleClient
    {
        /// <summary>
        /// Adds optional properties to a chat completion response if they exist in the provider response.
        /// </summary>
        /// <param name="response">The chat completion response to enhance.</param>
        /// <param name="providerResponse">The dynamic provider response.</param>
        /// <returns>The enhanced chat completion response.</returns>
        private CoreModels.ChatCompletionResponse AddOptionalResponseProperties(
            CoreModels.ChatCompletionResponse response,
            dynamic providerResponse)
        {
            // Try to add SystemFingerprint
            try
            {
                var hasSysFp = HasProperty(providerResponse, "SystemFingerprint");
                if (hasSysFp)
                {
                    response.SystemFingerprint = providerResponse.SystemFingerprint;
                }
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
            {
                // Property doesn't exist, which is OK for most providers
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error adding SystemFingerprint property: {Message}", ex.Message);
            }

            // Try to add Seed
            try
            {
                var hasSeed = HasProperty(providerResponse, "Seed");
                if (hasSeed)
                {
                    response.Seed = providerResponse.Seed;
                }
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
            {
                // Property doesn't exist, which is OK for most providers
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error adding Seed property: {Message}", ex.Message);
            }

            return response;
        }

        /// <summary>
        /// Tries to get a property value from a dynamic object, returning a default value if not found.
        /// </summary>
        /// <typeparam name="T">The type of value to return.</typeparam>
        /// <param name="obj">The dynamic object to get the property from.</param>
        /// <param name="propertyName">The name of the property to get.</param>
        /// <param name="defaultValue">The default value to return if the property is not found.</param>
        /// <returns>The property value if found, or the default value if not.</returns>
        private T TryGetProperty<T>(dynamic obj, string propertyName, T defaultValue)
        {
            try
            {
                var hasProperty = HasProperty(obj, propertyName);
                if (hasProperty)
                {
                    // Try to get the property using reflection
                    var property = obj.GetType().GetProperty(propertyName);
                    if (property != null)
                    {
                        return (T)property.GetValue(obj, null);
                    }

                    // If reflection fails, try dynamic access
                    if (obj is IDictionary<string, object> dictObj && dictObj.TryGetValue(propertyName, out var dictValue))
                    {
                        return (T)dictValue;
                    }
                }
            }
            catch
            {
                // Property doesn't exist or couldn't be accessed
                // This is expected behavior for optional properties, so we use Debug level
                // Suppress logging for now since we're in a dynamic context
                // This is expected behavior when optional properties don't exist
            }

            return defaultValue;
        }

        /// <summary>
        /// Checks if a dynamic object has a specific property.
        /// </summary>
        /// <param name="obj">The dynamic object to check.</param>
        /// <param name="propertyName">The name of the property to check for.</param>
        /// <returns>True if the property exists, false otherwise.</returns>
        private bool HasProperty(dynamic obj, string propertyName)
        {
            try
            {
                // Try to access the property using reflection
                var result = obj.GetType().GetProperty(propertyName) != null;
                return result;
            }
            catch
            {
                try
                {
                    // Alternatively, try to convert to JSON and check if the property exists
                    var jsonString = System.Text.Json.JsonSerializer.Serialize(obj);
                    var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonString);
                    System.Text.Json.JsonElement outValue;
                    return jsonDoc.RootElement.TryGetProperty(propertyName, out outValue);
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Maps the OpenAI streaming chunk to provider-agnostic format.
        /// </summary>
        /// <param name="chunkObj">The chunk from the OpenAI streaming API.</param>
        /// <param name="originalModelAlias">The original model alias from the request.</param>
        /// <returns>A provider-agnostic chat completion chunk.</returns>
        /// <remarks>
        /// This method maps the OpenAI-formatted streaming chunk to the generic format used by the application.
        /// Derived classes can override this method to provide custom mapping.
        /// </remarks>
        protected virtual CoreModels.ChatCompletionChunk MapFromOpenAIChunk(
            object chunkObj,
            string? originalModelAlias)
        {
            // Cast using dynamic to avoid multiple type-specific methods
            dynamic chunk = chunkObj;

            return new CoreModels.ChatCompletionChunk
            {
                Id = chunk.Id,
                Object = chunk.Object,
                Created = chunk.Created,
                Model = originalModelAlias ?? chunk.Model, // Use original alias if provided
                SystemFingerprint = chunk.SystemFingerprint,
                Choices = MapDynamicStreamingChoices(chunk.Choices),
                OriginalModelAlias = originalModelAlias,
                // Map usage data if present (typically in the final chunk)
                Usage = HasProperty(chunk, "usage") && chunk.usage != null ? MapUsage(chunk.usage) : null
            };
        }

        /// <summary>
        /// Maps dynamic choices from a response to strongly-typed Choice objects.
        /// </summary>
        /// <param name="dynamicChoices">The dynamic choices collection from response.</param>
        /// <returns>A list of strongly-typed Choice objects.</returns>
        private List<CoreModels.Choice> MapDynamicChoices(dynamic dynamicChoices)
        {
            var choices = new List<CoreModels.Choice>();

            // Handle null choices
            if (dynamicChoices == null)
            {
                return choices;
            }

            try
            {
                foreach (var choice in dynamicChoices)
                {
                    try
                    {
                        var mappedChoice = MapSingleChoice(choice);
                        choices.Add(mappedChoice);
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail on individual choice processing
                        Logger.LogWarning("Error processing choice: {Error}", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log and return whatever choices we managed to process
                Logger.LogError(ex, "Error mapping choices");
            }

            return choices;
        }

        /// <summary>
        /// Maps a single dynamic choice to a strongly-typed Choice object.
        /// </summary>
        /// <param name="choice">The dynamic choice to map.</param>
        /// <returns>A strongly-typed Choice object.</returns>
        private CoreModels.Choice MapSingleChoice(dynamic choice)
        {
            var mappedChoice = new CoreModels.Choice
            {
                Index = choice.Index,
                FinishReason = choice.FinishReason,
                Message = new CoreModels.Message
                {
                    Role = choice.Message.Role,
                    Content = choice.Message.Content
                }
            };

            // Handle tool calls if present
            if (choice.Message.ToolCalls != null)
            {
                mappedChoice.Message.ToolCalls = MapResponseToolCalls(choice.Message.ToolCalls);
            }

            // Handle tool_call_id if present (for tool response messages)
            if (choice.Message.ToolCallId != null)
            {
                mappedChoice.Message.ToolCallId = choice.Message.ToolCallId?.ToString();
            }

            return mappedChoice;
        }

        /// <summary>
        /// Maps dynamic tool calls from a response to strongly-typed ToolCall objects.
        /// </summary>
        /// <param name="toolCalls">The dynamic tool calls to map.</param>
        /// <returns>A list of strongly-typed ToolCall objects.</returns>
        private List<CoreModels.ToolCall> MapResponseToolCalls(dynamic toolCalls)
        {
            var mappedToolCalls = new List<CoreModels.ToolCall>();

            foreach (var toolCall in toolCalls)
            {
                try
                {
                    var mappedToolCall = MapSingleResponseToolCall(toolCall);
                    mappedToolCalls.Add(mappedToolCall);
                }
                catch (Exception ex)
                {
                    // Log but continue with other tool calls
                    Logger.LogWarning(ex, "Error mapping tool call");
                }
            }

            return mappedToolCalls;
        }

        /// <summary>
        /// Maps a single dynamic tool call from a response to a strongly-typed ToolCall object.
        /// </summary>
        /// <param name="toolCall">The dynamic tool call to map.</param>
        /// <returns>A strongly-typed ToolCall object.</returns>
        private CoreModels.ToolCall MapSingleResponseToolCall(dynamic toolCall)
        {
            return new CoreModels.ToolCall
            {
                Id = toolCall.id?.ToString() ?? Guid.NewGuid().ToString(),
                Type = toolCall.type?.ToString() ?? "function",
                Function = new CoreModels.FunctionCall
                {
                    Name = toolCall.function?.name?.ToString() ?? "unknown",
                    Arguments = toolCall.function?.arguments?.ToString() ?? "{}"
                }
            };
        }

        /// <summary>
        /// Maps dynamic streaming choices to strongly-typed StreamingChoice objects.
        /// </summary>
        /// <param name="dynamicChoices">The dynamic streaming choices collection.</param>
        /// <returns>A list of strongly-typed StreamingChoice objects.</returns>
        private List<CoreModels.StreamingChoice> MapDynamicStreamingChoices(dynamic dynamicChoices)
        {
            try
            {
                var choices = new List<CoreModels.StreamingChoice>();

                // Handle null choices
                if (dynamicChoices == null)
                {
                    return choices;
                }

                foreach (var choice in dynamicChoices)
                {
                    try
                    {
                        var mappedChoice = MapSingleStreamingChoice(choice);
                        choices.Add(mappedChoice);
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail on individual choice processing
                        Logger.LogWarning(ex, "Error processing streaming choice");
                    }
                }

                return choices;
            }
            catch (Exception ex)
            {
                // Log and return empty choices rather than failing
                Logger.LogError(ex, "Error mapping streaming choices");
                return new List<CoreModels.StreamingChoice>();
            }
        }

        /// <summary>
        /// Maps a single dynamic streaming choice to a strongly-typed StreamingChoice object.
        /// </summary>
        /// <param name="choice">The dynamic choice to map.</param>
        /// <returns>A strongly-typed StreamingChoice object.</returns>
        private CoreModels.StreamingChoice MapSingleStreamingChoice(dynamic choice)
        {
            var streamingChoice = new CoreModels.StreamingChoice
            {
                Index = choice.Index,
                FinishReason = choice.FinishReason,
                Delta = new CoreModels.DeltaContent
                {
                    Role = choice.Delta?.Role,
                    Content = choice.Delta?.Content
                }
            };

            // Handle tool calls if present
            if (choice.Delta != null && choice.Delta.ToolCalls != null)
            {
                streamingChoice.Delta.ToolCalls = MapToolCalls(choice.Delta.ToolCalls);
            }

            return streamingChoice;
        }

        /// <summary>
        /// Maps dynamic tool calls to strongly-typed ToolCallChunk objects.
        /// </summary>
        /// <param name="toolCalls">The dynamic tool calls to map.</param>
        /// <returns>A list of strongly-typed ToolCallChunk objects.</returns>
        private List<CoreModels.ToolCallChunk> MapToolCalls(dynamic toolCalls)
        {
            var mappedToolCalls = new List<CoreModels.ToolCallChunk>();

            foreach (var toolCall in toolCalls)
            {
                try
                {
                    var mappedToolCall = MapSingleToolCall(toolCall);
                    mappedToolCalls.Add(mappedToolCall);
                }
                catch (Exception ex)
                {
                    // Log but don't fail
                    Logger.LogWarning(ex, "Error processing tool call in stream");
                }
            }

            return mappedToolCalls;
        }

        /// <summary>
        /// Maps a single dynamic tool call to a strongly-typed ToolCallChunk object.
        /// </summary>
        /// <param name="toolCall">The dynamic tool call to map.</param>
        /// <returns>A strongly-typed ToolCallChunk object.</returns>
        private CoreModels.ToolCallChunk MapSingleToolCall(dynamic toolCall)
        {
            var mappedToolCall = new CoreModels.ToolCallChunk
            {
                Index = toolCall.Index,
                Id = toolCall.Id,
                Type = toolCall.Type
            };

            if (toolCall.Function != null)
            {
                mappedToolCall.Function = new CoreModels.FunctionCallChunk
                {
                    Name = toolCall.Function.Name,
                    Arguments = toolCall.Function.Arguments
                };
            }

            return mappedToolCall;
        }

        /// <summary>
        /// Configure the HTTP client with provider-specific settings.
        /// </summary>
        /// <param name="client">The HTTP client to configure.</param>
        /// <param name="apiKey">The API key to use for authentication.</param>
        /// <remarks>
        /// This method adds standard headers and authentication to the HTTP client.
        /// Derived classes can override this method to provide provider-specific configuration.
        /// </remarks>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            base.ConfigureHttpClient(client, apiKey);

            // Set the base address if not already set
            if (client.BaseAddress == null && !string.IsNullOrEmpty(BaseUrl))
            {
                client.BaseAddress = new Uri(BaseUrl);
            }

            // Add OpenAI API version header if needed
            // client.DefaultRequestHeaders.Add("OpenAI-Version", "2023-05-15");
        }

        /// <inheritdoc />
        public override Task<CoreModels.ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null)
        {
            var model = modelId ?? ProviderModelId;
            
            // For OpenAI-compatible providers, we provide sensible defaults
            // Individual providers can override this with more specific capabilities
            return Task.FromResult(new CoreModels.ProviderCapabilities
            {
                Provider = ProviderName,
                ModelId = model,
                ChatParameters = new CoreModels.ChatParameterSupport
                {
                    Temperature = true,
                    MaxTokens = true,
                    TopP = true,
                    TopK = false, // Most OpenAI-compatible APIs don't support top-k
                    Stop = true,
                    PresencePenalty = true,
                    FrequencyPenalty = true,
                    LogitBias = true,
                    N = true,
                    User = true,
                    Seed = true,
                    ResponseFormat = true,
                    Tools = true,
                    Constraints = new CoreModels.ParameterConstraints
                    {
                        TemperatureRange = new CoreModels.Range<double>(0.0, 2.0),
                        TopPRange = new CoreModels.Range<double>(0.0, 1.0),
                        MaxStopSequences = 4,
                        MaxTokenLimit = 4096 // Conservative default
                    }
                },
                Features = new CoreModels.FeatureSupport
                {
                    Streaming = true,
                    Embeddings = false, // Usually separate models
                    ImageGeneration = false, // Usually separate models
                    VisionInput = false, // Provider-specific
                    FunctionCalling = true,
                    AudioTranscription = false, // Provider-specific
                    TextToSpeech = false // Provider-specific
                }
            });
        }

        /// <summary>
        /// Extracts a more helpful error message from exception details.
        /// </summary>
        /// <param name="ex">The exception to extract information from.</param>
        /// <returns>An enhanced error message.</returns>
        /// <remarks>
        /// This method attempts to extract more helpful error information from exceptions.
        /// It looks for patterns in error messages and extracts the most relevant information.
        /// </remarks>
        protected virtual string ExtractEnhancedErrorMessage(Exception ex)
        {
            // Try to extract error details in order of preference:

            // 1. Look for "Response:" pattern in the message
            var msg = ex.Message;
            var responseIdx = msg.IndexOf("Response:");
            if (responseIdx >= 0)
            {
                var extracted = msg.Substring(responseIdx + "Response:".Length).Trim();
                if (!string.IsNullOrEmpty(extracted))
                {
                    return extracted;
                }
            }

            // 2. Look for JSON content in the message
            var jsonStart = msg.IndexOf("{");
            var jsonEnd = msg.LastIndexOf("}");
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonPart = msg.Substring(jsonStart, jsonEnd - jsonStart + 1);
                try
                {
                    var json = JsonDocument.Parse(jsonPart);
                    if (json.RootElement.TryGetProperty("error", out var errorElement))
                    {
                        if (errorElement.TryGetProperty("message", out var messageElement))
                        {
                            return messageElement.GetString() ?? msg;
                        }
                    }
                }
                catch
                {
                    // If parsing fails, continue to the next method
                }
            }

            // 3. Look for Body data in the exception's Data dictionary
            if (ex.Data.Contains("Body") && ex.Data["Body"] is string body && !string.IsNullOrEmpty(body))
            {
                return body;
            }

            // 4. Try inner exception
            if (ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message))
            {
                return ex.InnerException.Message;
            }

            // 5. Fallback to original message
            return msg;
        }
    }
}
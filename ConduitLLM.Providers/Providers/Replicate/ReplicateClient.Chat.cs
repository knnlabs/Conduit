using System.Runtime.CompilerServices;
using System.Text.Json;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Replicate
{
    public partial class ReplicateClient
    {
        /// <inheritdoc/>
        public override async Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateChatCompletionAsync");

            Logger.LogInformation("Creating chat completion with Replicate for model '{ModelId}'", ProviderModelId);

            try
            {
                // Map the request to Replicate format and start prediction
                var predictionRequest = MapToPredictionRequest(request);
                var predictionResponse = await StartPredictionAsync(predictionRequest, apiKey, cancellationToken);

                // Poll until prediction completes or fails
                var finalPrediction = await PollPredictionUntilCompletedAsync(predictionResponse.Id, apiKey, cancellationToken);

                // Process the final result
                return MapToChatCompletionResponse(finalPrediction, request.Model);
            }
            catch (LLMCommunicationException)
            {
                // Re-throw LLMCommunicationException directly
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unexpected error occurred while processing Replicate chat completion");
                throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "StreamChatCompletionAsync");

            Logger.LogInformation("Creating streaming chat completion with Replicate for model '{ModelId}'", ProviderModelId);

            // Variables to hold data outside the try block
            ReplicatePredictionRequest? predictionRequest = null;
            ReplicatePredictionResponse? predictionResponse = null;
            ReplicatePredictionResponse? finalPrediction = null;

            try
            {
                // Replicate doesn't natively support streaming in the common SSE format
                // Instead, we'll simulate streaming by getting the full response and breaking it into chunks

                // Start the prediction
                predictionRequest = MapToPredictionRequest(request);
                predictionResponse = await StartPredictionAsync(predictionRequest, apiKey, cancellationToken);
            }
            catch (LLMCommunicationException)
            {
                // Re-throw LLMCommunicationException directly
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unexpected error occurred starting Replicate prediction");
                throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
            }

            // First chunk with role "assistant" - outside try block so we can yield
            yield return CreateChatCompletionChunk(
                string.Empty,
                ProviderModelId,
                true,
                null,
                request.Model);

            try
            {
                // Poll until prediction completes or fails
                if (predictionResponse != null)
                {
                    finalPrediction = await PollPredictionUntilCompletedAsync(
                        predictionResponse.Id,
                        apiKey,
                        cancellationToken,
                        true); // Set yield progress to true
                }
            }
            catch (LLMCommunicationException)
            {
                // Re-throw LLMCommunicationException directly
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unexpected error occurred polling Replicate prediction");
                throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
            }

            // Extract content and yield the result - outside try block
            if (finalPrediction != null)
            {
                var content = ExtractTextFromPredictionOutput(finalPrediction.Output);
                if (!string.IsNullOrEmpty(content))
                {
                    // Yield the content as a chunk
                    yield return CreateChatCompletionChunk(
                        content,
                        ProviderModelId,
                        false,
                        "stop",
                        request.Model);
                }
            }
        }

        private ReplicatePredictionRequest MapToPredictionRequest(ChatCompletionRequest request)
        {
            // Prepare the input based on the model
            var input = new Dictionary<string, object>();

            // For Llama models, handle with the system message format
            if (ProviderModelId.Contains("llama", StringComparison.OrdinalIgnoreCase))
            {
                // Extract system message if present
                var systemMessage = request.Messages.FirstOrDefault(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));
                var systemPrompt = systemMessage != null ? systemMessage.Content?.ToString() : null;

                // Create a list of chat messages for the 'messages' parameter (excluding system)
                var chatMessages = request.Messages
                    .Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                    .Select(m => new ReplicateLlamaChatMessage
                    {
                        Role = m.Role,
                        Content = m.Content?.ToString() ?? string.Empty
                    })
                    .ToList();

                // Add the messages to the input
                input["messages"] = chatMessages;

                // Add system prompt if present
                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    input["system_prompt"] = systemPrompt;
                }
            }
            else
            {
                // For models that expect a simple text prompt, concatenate messages
                var promptBuilder = new System.Text.StringBuilder();

                foreach (var message in request.Messages)
                {
                    if (message.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                    {
                        promptBuilder.AppendLine($"System: {message.Content}");
                        promptBuilder.AppendLine();
                    }
                    else if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                    {
                        promptBuilder.AppendLine($"User: {message.Content}");
                    }
                    else if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                    {
                        promptBuilder.AppendLine($"Assistant: {message.Content}");
                    }
                }

                // Add a final prompt marker
                promptBuilder.Append("Assistant: ");

                // Add the prompt to the input
                input["prompt"] = promptBuilder.ToString();
            }

            // Add optional parameters if provided
            if (request.Temperature.HasValue)
            {
                input["temperature"] = request.Temperature.Value;
            }

            if (request.MaxTokens.HasValue)
            {
                input["max_length"] = request.MaxTokens.Value;
            }

            if (request.TopP.HasValue)
            {
                input["top_p"] = request.TopP.Value;
            }

            if (request.Stop != null && request.Stop.Count() > 0)
            {
                input["stop_sequences"] = request.Stop;
            }

            // Pass through any extension data (model-specific parameters)
            if (request.ExtensionData != null)
            {
                Logger.LogWarning("ExtensionData has {Count} items for Replicate", request.ExtensionData.Count);
                foreach (var kvp in request.ExtensionData)
                {
                    Logger.LogWarning("ExtensionData contains: {Key} = {Value} (Type: {Type})", 
                        kvp.Key, kvp.Value.ToString(), kvp.Value.ValueKind);
                    
                    // Don't override standard parameters that we've already set
                    if (!input.ContainsKey(kvp.Key))
                    {
                        // Convert JsonElement to actual value for proper serialization
                        var converted = ConvertJsonElement(kvp.Value);
                        if (converted != null)
                        {
                            input[kvp.Key] = converted;
                            Logger.LogWarning("Added to Replicate request: {Key} = {Value} (Type: {Type})", 
                                kvp.Key, converted, converted.GetType().Name);
                        }
                        else
                        {
                            Logger.LogWarning("Skipping ExtensionData key {Key} with null value", kvp.Key);
                        }
                    }
                    else
                    {
                        Logger.LogWarning("Skipping ExtensionData key {Key} as it already exists in input", kvp.Key);
                    }
                }
            }
            else
            {
                Logger.LogWarning("ExtensionData is NULL for Replicate request");
            }

            return new ReplicatePredictionRequest
            {
                Version = ProviderModelId,
                Input = input
            };
        }

        private ChatCompletionResponse MapToChatCompletionResponse(ReplicatePredictionResponse prediction, string originalModelAlias)
        {
            // Extract content from the prediction output - format depends on the model
            var content = ExtractTextFromPredictionOutput(prediction.Output);

            // Estimate token usage (not precise, just a rough estimate)
            var inputStr = prediction.Input != null ? JsonSerializer.Serialize(prediction.Input) : string.Empty;
            var promptTokens = EstimateTokenCount(inputStr);
            var completionTokens = EstimateTokenCount(content);

            return new ChatCompletionResponse
            {
                Id = prediction.Id,
                Object = "chat.completion",
                Created = ((DateTimeOffset)prediction.CreatedAt).ToUnixTimeSeconds(),
                Model = originalModelAlias,
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new Message
                        {
                            Role = "assistant",
                            Content = content
                        },
                        FinishReason = "stop"
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    TotalTokens = promptTokens + completionTokens
                },
                OriginalModelAlias = originalModelAlias
            };
        }
    }
}
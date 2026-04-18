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

            Logger.LogDebug("Creating chat completion with Replicate for model '{ModelId}'", ProviderModelId);

            return await ExecuteApiRequestAsync(async () =>
            {
                // Map the request to Replicate format and start prediction
                var predictionRequest = MapToPredictionRequest(request);
                var predictionResponse = await StartPredictionAsync(predictionRequest, apiKey, cancellationToken);

                // Poll until prediction completes or fails
                var finalPrediction = await PollPredictionUntilCompletedAsync(predictionResponse.Id, apiKey, cancellationToken);

                var response = MapToChatCompletionResponse(finalPrediction, request.Model);
                RecordUsage(response.Usage, "CreateChatCompletion");
                return response;
            }, "CreateChatCompletion", cancellationToken);
        }

        /// <inheritdoc/>
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "StreamChatCompletionAsync");

            Logger.LogDebug("Creating streaming chat completion with Replicate for model '{ModelId}'", ProviderModelId);

            using var logScope = BeginProviderLogScope("StreamChatCompletion");
            var instrumentation = BeginStreamingScope("StreamChatCompletion");

            ReplicatePredictionRequest? predictionRequest;
            ReplicatePredictionResponse? predictionResponse;
            ReplicatePredictionResponse? finalPrediction = null;

            try
            {
                try
                {
                    // Replicate doesn't natively support streaming in the common SSE format
                    // Instead, we'll simulate streaming by getting the full response and breaking it into chunks
                    predictionRequest = MapToPredictionRequest(request);
                    predictionResponse = await StartPredictionAsync(predictionRequest, apiKey, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    instrumentation.RecordFailure(nameof(OperationCanceledException));
                    throw;
                }
                catch (LLMCommunicationException)
                {
                    instrumentation.RecordFailure(nameof(LLMCommunicationException));
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "An unexpected error occurred starting Replicate prediction");
                    instrumentation.RecordFailure(ex.GetType().Name);
                    throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
                }

                // First chunk with role "assistant"
                instrumentation.RecordChunk();
                yield return CreateChatCompletionChunk(string.Empty, ProviderModelId, isFirst: true);

                try
                {
                    if (predictionResponse != null)
                    {
                        finalPrediction = await PollPredictionUntilCompletedAsync(
                            predictionResponse.Id, apiKey, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    instrumentation.RecordFailure(nameof(OperationCanceledException));
                    throw;
                }
                catch (LLMCommunicationException)
                {
                    instrumentation.RecordFailure(nameof(LLMCommunicationException));
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "An unexpected error occurred polling Replicate prediction");
                    instrumentation.RecordFailure(ex.GetType().Name);
                    throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
                }

                if (finalPrediction != null)
                {
                    var content = ExtractTextFromPredictionOutput(finalPrediction.Output);
                    if (!string.IsNullOrEmpty(content))
                    {
                        // Replicate doesn't report token usage; estimate from output size.
                        var promptTokens = finalPrediction.Input != null
                            ? EstimateTokenCount(JsonSerializer.Serialize(finalPrediction.Input))
                            : 0;
                        var completionTokens = EstimateTokenCount(content);
                        RecordUsage(new Usage
                        {
                            PromptTokens = promptTokens,
                            CompletionTokens = completionTokens,
                            TotalTokens = promptTokens + completionTokens
                        }, "StreamChatCompletion");

                        instrumentation.RecordChunk();
                        yield return CreateChatCompletionChunk(
                            content, ProviderModelId, isFirst: false, finishReason: "stop");
                    }
                }
            }
            finally
            {
                instrumentation.Dispose();
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

            if (request.Stop != null && request.Stop.Any())
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
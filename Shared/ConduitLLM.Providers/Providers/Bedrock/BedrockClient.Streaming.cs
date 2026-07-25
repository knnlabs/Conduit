using System.Runtime.CompilerServices;
using System.Text.Json;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Streaming;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Bedrock
{
    /// <summary>
    /// BedrockClient partial class containing streaming via ConverseStream.
    /// </summary>
    /// <remarks>
    /// ConverseStream responds with the AWS binary event-stream framing rather than SSE; frames are
    /// decoded by <see cref="AwsEventStreamReader"/> and mapped onto OpenAI-style chunks. Tool-call
    /// indices are assigned in order of appearance because Bedrock's <c>contentBlockIndex</c> counts
    /// every content block (text included) while OpenAI's <c>tool_calls[].index</c> counts only tool
    /// calls.
    /// </remarks>
    public partial class BedrockClient
    {
        /// <inheritdoc />
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "StreamChatCompletion");

            using var logScope = BeginProviderLogScope("StreamChatCompletion");
            var instrumentation = BeginStreamingScope("StreamChatCompletion");
            HttpClient? httpClient = null;
            HttpResponseMessage? response = null;
            var modelId = request.Model ?? ProviderModelId;

            try
            {
                try
                {
                    httpClient = CreateHttpClient(apiKey);
                    var payload = SerializePayload(MapToConverseRequest(request));
                    var url = BuildModelUrl(modelId, "converse-stream");
                    var httpRequest = BuildRequest(HttpMethod.Post, url, payload, RuntimeService, apiKey);

                    response = await httpClient.SendAsync(
                        httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    await ThrowOnErrorAsync(response, "streaming chat completion", cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    instrumentation.RecordFailure(nameof(OperationCanceledException));
                    throw;
                }
                catch (Exception ex)
                {
                    instrumentation.RecordFailure(ex.GetType().Name);
                    throw;
                }

                var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var streamId = $"bedrock-{Guid.NewGuid():N}";
                // Maps Bedrock content block index -> OpenAI tool call index.
                var toolCallIndexByBlock = new Dictionary<int, int>();
                var reportedUsage = false;

                await foreach (var message in AwsEventStreamReader.ReadMessagesAsync(stream, cancellationToken))
                {
                    if (message.MessageType == "exception" || message.ExceptionType != null)
                    {
                        var error = ExtractErrorFromJson(message.PayloadText, message.PayloadText);
                        instrumentation.RecordFailure(message.ExceptionType ?? "BedrockStreamException");
                        throw new LLMCommunicationException(
                            $"Bedrock streaming error ({message.ExceptionType ?? "exception"}): {error}");
                    }

                    var chunk = MapStreamEvent(message, streamId, created, modelId, toolCallIndexByBlock);
                    if (chunk == null)
                    {
                        continue;
                    }

                    if (!reportedUsage && chunk.Usage != null)
                    {
                        RecordUsage(chunk.Usage, "StreamChatCompletion");
                        reportedUsage = true;
                    }

                    instrumentation.RecordChunk();
                    yield return chunk;
                }
            }
            finally
            {
                response?.Dispose();
                httpClient?.Dispose();
                instrumentation.Dispose();
            }
        }

        private ChatCompletionChunk? MapStreamEvent(
            AwsEventStreamMessage message,
            string streamId,
            long created,
            string modelId,
            Dictionary<int, int> toolCallIndexByBlock)
        {
            switch (message.EventType)
            {
                case "messageStart":
                    return CreateChunk(streamId, created, modelId,
                        new DeltaContent { Role = MessageRole.Assistant });

                case "contentBlockStart":
                {
                    var start = Deserialize<BedrockStreamContentBlockStart>(message);
                    if (start?.Start?.ToolUse is not { } toolUse)
                    {
                        return null;
                    }

                    var toolIndex = toolCallIndexByBlock.Count;
                    toolCallIndexByBlock[start.ContentBlockIndex] = toolIndex;
                    return CreateChunk(streamId, created, modelId, new DeltaContent
                    {
                        ToolCalls = new List<ToolCallChunk>
                        {
                            new()
                            {
                                Index = toolIndex,
                                Id = toolUse.ToolUseId,
                                Type = "function",
                                Function = new FunctionCallChunk { Name = toolUse.Name, Arguments = string.Empty }
                            }
                        }
                    });
                }

                case "contentBlockDelta":
                {
                    var delta = Deserialize<BedrockStreamContentBlockDelta>(message);
                    if (delta?.Delta?.Text is { Length: > 0 } text)
                    {
                        return CreateChunk(streamId, created, modelId, new DeltaContent { Content = text });
                    }

                    if (delta?.Delta?.ReasoningContent?.Text is { Length: > 0 } reasoning)
                    {
                        return CreateChunk(streamId, created, modelId, new DeltaContent { Reasoning = reasoning });
                    }

                    if (delta?.Delta?.ToolUse?.Input is { Length: > 0 } arguments)
                    {
                        var toolIndex = toolCallIndexByBlock.GetValueOrDefault(delta.ContentBlockIndex, 0);
                        return CreateChunk(streamId, created, modelId, new DeltaContent
                        {
                            ToolCalls = new List<ToolCallChunk>
                            {
                                new()
                                {
                                    Index = toolIndex,
                                    Function = new FunctionCallChunk { Arguments = arguments }
                                }
                            }
                        });
                    }

                    return null;
                }

                case "messageStop":
                {
                    var stop = Deserialize<BedrockStreamMessageStop>(message);
                    var chunk = CreateChunk(streamId, created, modelId, new DeltaContent());
                    chunk.Choices[0].FinishReason = MapStopReason(stop?.StopReason);
                    return chunk;
                }

                case "metadata":
                {
                    var metadata = Deserialize<BedrockStreamMetadata>(message);
                    if (metadata?.Usage is not { } usage)
                    {
                        return null;
                    }

                    var chunk = CreateChunk(streamId, created, modelId, new DeltaContent());
                    chunk.Usage = new Usage
                    {
                        PromptTokens = usage.InputTokens,
                        CompletionTokens = usage.OutputTokens,
                        TotalTokens = usage.TotalTokens
                            ?? (usage.InputTokens ?? 0) + (usage.OutputTokens ?? 0)
                    };
                    return chunk;
                }

                case "contentBlockStop":
                    return null;

                default:
                    Logger.LogDebug("Ignoring unrecognized Bedrock stream event type {EventType}", message.EventType);
                    return null;
            }
        }

        private T? Deserialize<T>(AwsEventStreamMessage message) where T : class
        {
            try
            {
                return JsonSerializer.Deserialize<T>(message.Payload, DefaultJsonOptions);
            }
            catch (JsonException ex)
            {
                Logger.LogWarning(ex, "Failed to parse Bedrock stream event {EventType}", message.EventType);
                return null;
            }
        }

        private static ChatCompletionChunk CreateChunk(string id, long created, string modelId, DeltaContent delta) =>
            new()
            {
                Id = id,
                Object = "chat.completion.chunk",
                Created = created,
                Model = modelId,
                Choices = new List<StreamingChoice> { new() { Index = 0, Delta = delta } }
            };
    }
}

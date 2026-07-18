using System.Runtime.CompilerServices;

using ConduitLLM.Core.Models;
using ConduitLLM.Core.Exceptions;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.MiniMax
{
    /// <summary>
    /// MiniMaxClient partial class containing streaming functionality.
    /// </summary>
    public partial class MiniMaxClient
    {
        /// <inheritdoc/>
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
            bool reportedUsage = false;

            try
            {
                try
                {
                    httpClient = CreateHttpClient(apiKey);

                    var miniMaxRequest = new MiniMaxChatCompletionRequest
                    {
                        Model = request.Model ?? ProviderModelId,
                        Messages = ConvertMessages(request.Messages, includeNames: true),
                        Stream = true,
                        MaxTokens = request.MaxTokens,
                        Temperature = request.Temperature,
                        TopP = request.TopP,
                        Tools = ConvertTools(request.Tools),
                        ToolChoice = ConvertToolChoice(request.ToolChoice),
                        ReplyConstraints = request.ResponseFormat != null ? new ReplyConstraints
                        {
                            GuidanceType = request.ResponseFormat.Type == "json_object" ? "json_schema" : null,
                            JsonSchema = request.ResponseFormat.Type == "json_object" ? new { type = "object" } : null
                        } : null
                    };

                    var endpoint = $"{_baseUrl}/v1/text/chatcompletion_v2";

                    if (Logger.IsEnabled(LogLevel.Debug))
                    {
                        var requestJson = System.Text.Json.JsonSerializer.Serialize(miniMaxRequest);
                        Logger.LogDebug("MiniMax Streaming Request to {Endpoint}: {Request}", endpoint, requestJson);
                    }

                    response = await Core.Utilities.HttpClientHelper.SendStreamingRequestAsync(
                        httpClient, HttpMethod.Post, endpoint, miniMaxRequest, null, null, Logger, cancellationToken);

                    Logger.LogDebug("MiniMax streaming response status: {StatusCode}", response.StatusCode);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        Logger.LogError("MiniMax streaming failed with status {Status}: {Content}",
                            response.StatusCode, errorContent);
                        throw new LLMCommunicationException($"MiniMax streaming failed: {response.StatusCode} - {errorContent}");
                    }
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

                var streamEnum = Core.Utilities.StreamHelper.ProcessSseStreamAsync<MiniMaxStreamChunk>(
                    response!, Logger, null, cancellationToken);

                await foreach (var chunk in streamEnum.WithCancellation(cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Logger.LogInformation("MiniMax streaming cancelled by client");
                        yield break;
                    }

                    if (chunk == null)
                    {
                        Logger.LogDebug("Received null chunk from MiniMax stream");
                        continue;
                    }

                    Logger.LogDebug("Received MiniMax chunk with ID: {Id}, Choices: {ChoiceCount}",
                        chunk.Id, chunk.Choices?.Count ?? 0);

                    // Check for MiniMax error response embedded in SSE stream
                    if (chunk.BaseResp is { } baseResp && baseResp.StatusCode != 0)
                    {
                        Logger.LogError("MiniMax streaming error: {StatusCode} - {StatusMsg}",
                            baseResp.StatusCode, baseResp.StatusMsg);
                        instrumentation.RecordFailure("MiniMaxStreamError");
                        throw new LLMCommunicationException($"MiniMax error: {baseResp.StatusMsg}");
                    }

                    ChatCompletionChunk convertedChunk;
                    try
                    {
                        convertedChunk = ConvertToChunk(chunk, request.Model ?? ProviderModelId);
                    }
                    catch (System.Text.Json.JsonException jsonEx)
                    {
                        Logger.LogError(jsonEx, "Failed to parse MiniMax chunk. Raw chunk: {Chunk}",
                            System.Text.Json.JsonSerializer.Serialize(chunk));
                        instrumentation.RecordFailure(nameof(System.Text.Json.JsonException));
                        throw new LLMCommunicationException($"Failed to parse MiniMax chunk: {jsonEx.Message}", jsonEx);
                    }
                    catch (Exception convEx)
                    {
                        Logger.LogError(convEx, "Failed to convert MiniMax chunk to standard format");
                        instrumentation.RecordFailure(convEx.GetType().Name);
                        throw new LLMCommunicationException($"Failed to convert MiniMax chunk: {convEx.Message}", convEx);
                    }

                    instrumentation.RecordChunk();

                    if (!reportedUsage && chunk.Usage is { } chunkUsage &&
                        (chunkUsage.PromptTokens > 0 || chunkUsage.CompletionTokens > 0 || chunkUsage.TotalTokens > 0))
                    {
                        RecordUsage(new Usage
                        {
                            PromptTokens = chunkUsage.PromptTokens,
                            CompletionTokens = chunkUsage.CompletionTokens,
                            TotalTokens = chunkUsage.TotalTokens
                        }, "StreamChatCompletion");
                        reportedUsage = true;
                    }

                    yield return convertedChunk;
                }

                Logger.LogDebug("MiniMax streaming completed");
            }
            finally
            {
                response?.Dispose();
                httpClient?.Dispose();
                instrumentation.Dispose();
            }
        }

        private ChatCompletionChunk ConvertToChunk(MiniMaxStreamChunk miniMaxChunk, string modelId)
        {
            Logger.LogDebug("Converting MiniMax chunk: Id={Id}, ChoiceCount={ChoiceCount}",
                miniMaxChunk.Id, miniMaxChunk.Choices?.Count ?? 0);

            var chunk = new ChatCompletionChunk
            {
                Id = miniMaxChunk.Id ?? Guid.NewGuid().ToString(),
                Object = "chat.completion.chunk",
                Created = miniMaxChunk.Created ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = modelId,
                Choices = new List<StreamingChoice>()
            };

            if (miniMaxChunk.Choices != null)
            {
                foreach (var choice in miniMaxChunk.Choices)
                {
                    // DEVIATION FROM OPENAI SPEC: MiniMax sends a non-standard final chunk
                    // OpenAI spec: All chunks should only use 'delta' field for content
                    // MiniMax behavior: Final chunk with finish_reason="stop" contains:
                    //   - A complete 'message' field with the full assembled content
                    //   - object: "chat.completion" instead of "chat.completion.chunk"
                    // This is redundant (content was already streamed) and breaks OpenAI compatibility.
                    // We must check both delta and message fields to handle this non-standard format.

                    string? content = null;
                    string? role = null;
                    MiniMaxFunctionCall? functionCall = null;

                    if (choice.Message != null)
                    {
                        Logger.LogDebug("MiniMax non-standard final chunk detected with complete message");

                        content = !string.IsNullOrEmpty(choice.Message.Content?.ToString())
                            ? choice.Message.Content.ToString()
                            : choice.Message.ReasoningContent;
                        role = choice.Message.Role;
                        functionCall = choice.Message.FunctionCall;

                        if (!string.IsNullOrEmpty(content) && choice.FinishReason == "stop")
                        {
                            Logger.LogDebug("Skipping redundant complete message in MiniMax final chunk");
                            content = null;
                        }
                    }
                    else if (choice.Delta != null)
                    {
                        content = !string.IsNullOrEmpty(choice.Delta.Content)
                            ? choice.Delta.Content
                            : choice.Delta.ReasoningContent;
                        role = choice.Delta.Role;
                        functionCall = choice.Delta.FunctionCall;
                    }

                    Logger.LogDebug("MiniMax choice: Index={Index}, Content={Content}, Role={Role}, FinishReason={FinishReason}, HasMessage={HasMessage}",
                        choice.Index, content, role, choice.FinishReason, choice.Message != null);

                    chunk.Choices.Add(new StreamingChoice
                    {
                        Index = choice.Index,
                        Delta = new DeltaContent
                        {
                            Role = role,
                            Content = content,
                            ToolCalls = ConvertDeltaFunctionCallToToolCalls(functionCall)
                        },
                        FinishReason = choice.FinishReason
                    });
                }
            }

            return chunk;
        }

        private List<ToolCallChunk>? ConvertDeltaFunctionCallToToolCalls(MiniMaxFunctionCall? functionCall)
        {
            if (functionCall == null)
                return null;

            return new List<ToolCallChunk>
            {
                new ToolCallChunk
                {
                    Index = 0,
                    Id = Guid.NewGuid().ToString(),
                    Type = "function",
                    Function = new FunctionCallChunk
                    {
                        Name = functionCall.Name,
                        Arguments = functionCall.Arguments
                    }
                }
            };
        }
    }
}

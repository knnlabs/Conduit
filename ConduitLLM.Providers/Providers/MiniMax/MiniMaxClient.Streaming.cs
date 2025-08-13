using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

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

            // Process the stream and collect chunks or errors
            var (chunks, error) = await ProcessStreamAsync(request, apiKey, cancellationToken);
            
            // If there was an error, throw it
            if (error != null)
            {
                throw error;
            }
            
            // Yield all the chunks
            foreach (var chunk in chunks)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Logger.LogInformation("MiniMax streaming cancelled during yield");
                    yield break;
                }
                yield return chunk;
            }
        }
        
        private async Task<(List<ChatCompletionChunk> chunks, Exception? error)> ProcessStreamAsync(
            ChatCompletionRequest request,
            string? apiKey,
            CancellationToken cancellationToken)
        {
            var chunks = new List<ChatCompletionChunk>();
            HttpClient? httpClient = null;
            HttpResponseMessage? response = null;
            
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

                // MiniMax streaming uses the v2 API endpoint
                var endpoint = $"{_baseUrl}/v1/text/chatcompletion_v2";
                
                // Log the full request details for debugging
                var requestJson = System.Text.Json.JsonSerializer.Serialize(miniMaxRequest);
                Logger.LogInformation("MiniMax Streaming Request to {Endpoint}: {Request}", endpoint, requestJson);
                
                response = await Core.Utilities.HttpClientHelper.SendStreamingRequestAsync(
                    httpClient, HttpMethod.Post, endpoint, miniMaxRequest, null, null, Logger, cancellationToken);

                Logger.LogInformation("MiniMax streaming response status: {StatusCode}, Headers: {Headers}", 
                    response.StatusCode, response.Headers.ToString());

                // Check if response is successful
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    Logger.LogError("MiniMax streaming failed with status {Status}: {Content}", 
                        response.StatusCode, errorContent);
                    return (chunks, new LLMCommunicationException($"MiniMax streaming failed: {response.StatusCode} - {errorContent}"));
                }

                var streamEnum = Core.Utilities.StreamHelper.ProcessSseStreamAsync<MiniMaxStreamChunk>(
                    response, Logger, null, cancellationToken);

                await foreach (var chunk in streamEnum.WithCancellation(cancellationToken))
                {
                    // Check cancellation token explicitly
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Logger.LogInformation("MiniMax streaming cancelled by client");
                        break;
                    }
                    
                    if (chunk != null)
                    {
                        Logger.LogDebug("Received MiniMax chunk with ID: {Id}, Choices: {ChoiceCount}", 
                            chunk.Id, chunk.Choices?.Count ?? 0);
                        
                        // Check for MiniMax error response
                        if (chunk.BaseResp is { } baseResp && baseResp.StatusCode != 0)
                        {
                            Logger.LogError("MiniMax streaming error: {StatusCode} - {StatusMsg}", 
                                baseResp.StatusCode, baseResp.StatusMsg);
                            return (chunks, new LLMCommunicationException($"MiniMax error: {baseResp.StatusMsg}"));
                        }
                        
                        ChatCompletionChunk? convertedChunk = null;
                        
                        try
                        {
                            convertedChunk = ConvertToChunk(chunk, request.Model ?? ProviderModelId);
                        }
                        catch (System.Text.Json.JsonException jsonEx)
                        {
                            Logger.LogError(jsonEx, "Failed to parse MiniMax chunk. Raw chunk: {Chunk}", 
                                System.Text.Json.JsonSerializer.Serialize(chunk));
                            return (chunks, new LLMCommunicationException($"Failed to parse MiniMax chunk: {jsonEx.Message}", jsonEx));
                        }
                        catch (Exception convEx)
                        {
                            Logger.LogError(convEx, "Failed to convert MiniMax chunk to standard format");
                            return (chunks, new LLMCommunicationException($"Failed to convert MiniMax chunk: {convEx.Message}", convEx));
                        }
                        
                        if (convertedChunk != null)
                            chunks.Add(convertedChunk);
                    }
                    else
                    {
                        Logger.LogDebug("Received null chunk from MiniMax stream");
                    }
                }
                
                Logger.LogInformation("MiniMax streaming completed");
                return (chunks, null);
            }
            catch (OperationCanceledException ex)
            {
                Logger.LogInformation("MiniMax streaming was cancelled");
                return (chunks, ex);
            }
            catch (LLMCommunicationException ex)
            {
                return (chunks, ex);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to send streaming request to MiniMax. Endpoint: {Endpoint}", 
                    $"{_baseUrl}/v1/text/chatcompletion_v2");
                return (chunks, new LLMCommunicationException($"Failed to connect to MiniMax streaming API: {ex.Message}", ex));
            }
            finally
            {
                // Ensure proper resource disposal
                response?.Dispose();
                httpClient?.Dispose();
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
                        // MiniMax's non-standard final chunk with complete message
                        // This should NOT exist in OpenAI-compliant streaming
                        Logger.LogDebug("MiniMax non-standard final chunk detected with complete message");
                        
                        // Extract content from the message field
                        // MiniMax's Message.Content can be string or object, so handle accordingly
                        // Also check ReasoningContent for models that use reasoning tokens
                        content = !string.IsNullOrEmpty(choice.Message.Content?.ToString()) 
                            ? choice.Message.Content.ToString()
                            : choice.Message.ReasoningContent;
                        role = choice.Message.Role;
                        functionCall = choice.Message.FunctionCall;
                        
                        // Since this is the complete message, we only send the final piece
                        // to avoid duplicating what was already streamed
                        // This is a workaround for MiniMax's protocol violation
                        if (!string.IsNullOrEmpty(content) && choice.FinishReason == "stop")
                        {
                            // Skip the complete message in final chunk to avoid duplication
                            // The content has already been streamed incrementally
                            Logger.LogDebug("Skipping redundant complete message in MiniMax final chunk");
                            content = null; // Don't send the complete message again
                        }
                    }
                    else if (choice.Delta != null)
                    {
                        // Standard OpenAI-compliant streaming chunk with delta
                        // MiniMax may use ReasoningContent for models with reasoning tokens
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

            // Note: ChatCompletionChunk doesn't have Usage property in standard implementation
            // Usage is typically tracked separately or sent in final chunk

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
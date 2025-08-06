using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Models;
using ConduitLLM.Core.Exceptions;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.MiniMax
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

            using var httpClient = CreateHttpClient(apiKey);
            
            var miniMaxRequest = new MiniMaxChatCompletionRequest
            {
                Model = MapModelName(request.Model ?? ProviderModelId),
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
            
            HttpResponseMessage response;
            try
            {
                response = await Core.Utilities.HttpClientHelper.SendStreamingRequestAsync(
                    httpClient, HttpMethod.Post, endpoint, miniMaxRequest, null, null, Logger, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to send streaming request to MiniMax. Endpoint: {Endpoint}", endpoint);
                throw new LLMCommunicationException($"Failed to connect to MiniMax streaming API: {ex.Message}", ex);
            }

            Logger.LogInformation("MiniMax streaming response status: {StatusCode}, Headers: {Headers}", 
                response.StatusCode, response.Headers.ToString());

            // Check if response is successful
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Logger.LogError("MiniMax streaming failed with status {Status}: {Content}", 
                    response.StatusCode, errorContent);
                throw new LLMCommunicationException($"MiniMax streaming failed: {response.StatusCode} - {errorContent}");
            }

            IAsyncEnumerable<MiniMaxStreamChunk?> streamEnum;
            try
            {
                streamEnum = Core.Utilities.StreamHelper.ProcessSseStreamAsync<MiniMaxStreamChunk>(
                    response, Logger, null, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to initialize MiniMax stream processing");
                throw new LLMCommunicationException($"Failed to process MiniMax stream: {ex.Message}", ex);
            }

            await foreach (var chunk in streamEnum)
            {
                if (chunk != null)
                {
                    Logger.LogDebug("Received MiniMax chunk with ID: {Id}, Choices: {ChoiceCount}", 
                        chunk.Id, chunk.Choices?.Count ?? 0);
                    
                    // Check for MiniMax error response
                    if (chunk.BaseResp is { } baseResp && baseResp.StatusCode != 0)
                    {
                        Logger.LogError("MiniMax streaming error: {StatusCode} - {StatusMsg}", 
                            baseResp.StatusCode, baseResp.StatusMsg);
                        throw new LLMCommunicationException($"MiniMax error: {baseResp.StatusMsg}");
                    }
                    
                    ChatCompletionChunk? convertedChunk = null;
                    Exception? conversionError = null;
                    
                    try
                    {
                        convertedChunk = ConvertToChunk(chunk, request.Model ?? ProviderModelId);
                    }
                    catch (System.Text.Json.JsonException jsonEx)
                    {
                        Logger.LogError(jsonEx, "Failed to parse MiniMax chunk. Raw chunk: {Chunk}", 
                            System.Text.Json.JsonSerializer.Serialize(chunk));
                        conversionError = new LLMCommunicationException($"Failed to parse MiniMax chunk: {jsonEx.Message}", jsonEx);
                    }
                    catch (Exception convEx)
                    {
                        Logger.LogError(convEx, "Failed to convert MiniMax chunk to standard format");
                        conversionError = new LLMCommunicationException($"Failed to convert MiniMax chunk: {convEx.Message}", convEx);
                    }
                    
                    if (conversionError != null)
                        throw conversionError;
                    
                    if (convertedChunk != null)
                        yield return convertedChunk;
                }
                else
                {
                    Logger.LogDebug("Received null chunk from MiniMax stream");
                }
            }
            
            Logger.LogInformation("MiniMax streaming completed");
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
                    var content = choice.Delta?.Content;
                    var role = choice.Delta?.Role;
                    
                    Logger.LogDebug("MiniMax choice: Index={Index}, Content={Content}, Role={Role}, FinishReason={FinishReason}", 
                        choice.Index, content, role, choice.FinishReason);
                    
                    chunk.Choices.Add(new StreamingChoice
                    {
                        Index = choice.Index,
                        Delta = new DeltaContent
                        {
                            Role = role,
                            Content = content,
                            ToolCalls = ConvertDeltaFunctionCallToToolCalls(choice.Delta?.FunctionCall)
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
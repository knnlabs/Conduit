using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.MiniMax
{
    /// <summary>
    /// MiniMaxClient partial class containing chat completion methods.
    /// </summary>
    public partial class MiniMaxClient
    {
        /// <inheritdoc />
        public override async Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateChatCompletion");

            return await ExecuteApiRequestAsync(async () =>
            {
                using var httpClient = CreateHttpClient(apiKey);
                
                var miniMaxRequest = new MiniMaxChatCompletionRequest
                {
                    Model = MapModelName(request.Model ?? ProviderModelId),
                    Messages = ConvertMessages(request.Messages, includeNames: request.Stream == true),
                    Stream = request.Stream ?? false,
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

                // MiniMax uses different endpoints for streaming vs non-streaming
                // Streaming uses the v2 API which requires name fields in messages
                var endpoint = request.Stream == true 
                    ? $"{_baseUrl}/v1/text/chatcompletion_v2"
                    : $"{_baseUrl}/v1/chat/completions";
                // Log the request for debugging
                var requestJson = JsonSerializer.Serialize(miniMaxRequest);
                Logger.LogInformation("MiniMax request: {Request}", requestJson);

                // Make direct HTTP call to debug
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
                httpRequest.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                
                var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
                var rawContent = await httpResponse.Content.ReadAsStringAsync();
                
                Logger.LogInformation("MiniMax HTTP Status: {Status}", httpResponse.StatusCode);
                Logger.LogInformation("MiniMax raw response: {Response}", rawContent);
                
                if (!httpResponse.IsSuccessStatusCode)
                {
                    throw new LLMCommunicationException($"MiniMax API returned {httpResponse.StatusCode}: {rawContent}");
                }
                
                // Now deserialize
                MiniMaxChatCompletionResponse response;
                try
                {
                    response = JsonSerializer.Deserialize<MiniMaxChatCompletionResponse>(rawContent, new JsonSerializerOptions
                    {
                        // MiniMax uses snake_case, not camelCase
                        PropertyNameCaseInsensitive = true,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                    })!;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error deserializing MiniMax response: {Response}", rawContent);
                    throw new LLMCommunicationException("Failed to deserialize MiniMax response", ex);
                }

                // Log the raw response for debugging
                if (response == null)
                {
                    Logger.LogWarning("MiniMax response is null");
                    throw new LLMCommunicationException("MiniMax returned null response");
                }

                var responseJson = JsonSerializer.Serialize(response);
                Logger.LogInformation("MiniMax response: {Response}", responseJson);
                Logger.LogInformation("MiniMax response choices count: {Count}", response.Choices?.Count ?? 0);
                if (response.Choices != null && response.Choices.Count > 0)
                {
                    Logger.LogInformation("First choice message: {Message}", 
                        JsonSerializer.Serialize(response.Choices[0].Message));
                    if (response.Choices[0].Message != null)
                    {
                        Logger.LogInformation("Message content: '{Content}', ReasoningContent: '{Reasoning}'", 
                            response.Choices[0].Message.Content, 
                            response.Choices[0].Message.ReasoningContent);
                    }
                }

                // Check for MiniMax error response
                if (response.BaseResp is { } baseResp && baseResp.StatusCode != 0)
                {
                    Logger.LogError("MiniMax error: {StatusCode} - {StatusMsg}", 
                        baseResp.StatusCode, baseResp.StatusMsg);
                    throw new LLMCommunicationException($"MiniMax error: {baseResp.StatusMsg}");
                }

                return ConvertToCoreResponse(response, request.Model ?? ProviderModelId);
            }, "CreateChatCompletion", cancellationToken);
        }
    }
}
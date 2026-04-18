using System.Text;
using System.Text.Json;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.MiniMax
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
                    Model = request.Model ?? ProviderModelId,
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

                var requestJson = JsonSerializer.Serialize(miniMaxRequest);
                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug("MiniMax request to {Endpoint}: {Request}", endpoint, requestJson);
                }

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
                httpRequest.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                using var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
                var rawContent = await httpResponse.Content.ReadAsStringAsync();

                Logger.LogDebug("MiniMax HTTP Status: {Status}", httpResponse.StatusCode);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    Logger.LogError("MiniMax API returned {Status}: {Response}", httpResponse.StatusCode, rawContent);
                    throw new LLMCommunicationException($"MiniMax API returned {httpResponse.StatusCode}: {rawContent}");
                }

                MiniMaxChatCompletionResponse response;
                try
                {
                    response = JsonSerializer.Deserialize<MiniMaxChatCompletionResponse>(rawContent, CaseInsensitiveJsonOptions)!;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error deserializing MiniMax response: {Response}", rawContent);
                    throw new LLMCommunicationException("Failed to deserialize MiniMax response", ex);
                }

                if (response == null)
                {
                    Logger.LogWarning("MiniMax response is null");
                    throw new LLMCommunicationException("MiniMax returned null response");
                }

                Logger.LogDebug("MiniMax response choices count: {Count}", response.Choices?.Count ?? 0);

                // Check for MiniMax error response
                if (response.BaseResp is { } baseResp && baseResp.StatusCode != 0)
                {
                    Logger.LogError("MiniMax error: {StatusCode} - {StatusMsg}",
                        baseResp.StatusCode, baseResp.StatusMsg);
                    throw new LLMCommunicationException($"MiniMax error: {baseResp.StatusMsg}");
                }

                var coreResponse = ConvertToCoreResponse(response, request.Model ?? ProviderModelId);
                RecordUsage(coreResponse.Usage, "CreateChatCompletion");
                return coreResponse;
            }, "CreateChatCompletion", cancellationToken);
        }
    }
}
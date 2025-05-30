using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.Helpers;
using ConduitLLM.Providers.InternalModels;
using ConduitLLM.Providers.InternalModels.HuggingFaceModels;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Client for interacting with HuggingFace Inference API.
    /// </summary>
    public class HuggingFaceClient : BaseLLMClient
    {
        // Constants
        private const string DefaultApiBase = "https://api-inference.huggingface.co/models/";

        /// <summary>
        /// Initializes a new instance of the <see cref="HuggingFaceClient"/> class.
        /// </summary>
        /// <param name="credentials">The provider credentials.</param>
        /// <param name="providerModelId">The provider's model identifier.</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory.</param>
        public HuggingFaceClient(
            ProviderCredentials credentials,
            string providerModelId,
            ILogger<HuggingFaceClient> logger,
            IHttpClientFactory? httpClientFactory = null)
            : base(
                EnsureHuggingFaceCredentials(credentials),
                providerModelId,
                logger,
                httpClientFactory,
                "huggingface")
        {
        }

        private static ProviderCredentials EnsureHuggingFaceCredentials(ProviderCredentials credentials)
        {
            if (credentials == null)
            {
                throw new ArgumentNullException(nameof(credentials));
            }

            if (string.IsNullOrWhiteSpace(credentials.ApiKey))
            {
                throw new ConfigurationException("API key is missing for HuggingFace Inference API provider.");
            }

            return credentials;
        }

        /// <inheritdoc />
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Configure base address
            string apiBase = string.IsNullOrWhiteSpace(Credentials.ApiBase)
                ? DefaultApiBase
                : Credentials.ApiBase.TrimEnd('/');

            client.BaseAddress = new Uri(apiBase);
        }

        /// <inheritdoc />
        public override async Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "ChatCompletion");

            return await ExecuteApiRequestAsync(async () =>
            {
                // Create HTTP client with authentication
                using var client = CreateHttpClient(apiKey);

                // Determine endpoint based on model type
                string apiEndpoint = GetHuggingFaceEndpoint(request.Model ?? ProviderModelId);

                // Different models require different input formats
                // For simplicity, we'll convert messages to a text prompt
                string formattedPrompt = FormatChatMessages(request.Messages);

                // Create request based on model type
                var hfRequest = new HuggingFaceTextGenerationRequest
                {
                    Inputs = formattedPrompt,
                    Parameters = new HuggingFaceParameters
                    {
                        MaxNewTokens = request.MaxTokens,
                        Temperature = request.Temperature,
                        TopP = request.TopP,
                        DoSample = true,
                        ReturnFullText = false
                    },
                    Options = new HuggingFaceOptions
                    {
                        WaitForModel = true
                    }
                };

                // Send request
                using var response = await client.PostAsJsonAsync(
                    apiEndpoint,
                    hfRequest,
                    new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull },
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await ReadErrorContentAsync(response, cancellationToken);
                    Logger.LogError("HuggingFace Inference API request failed with status code {StatusCode}. Response: {ErrorContent}",
                        response.StatusCode, errorContent);
                    throw new LLMCommunicationException(
                        $"HuggingFace Inference API request failed with status code {response.StatusCode}. Response: {errorContent}");
                }

                // Parse response
                string contentType = response.Content.Headers.ContentType?.MediaType ?? "application/json";

                if (contentType.Contains("json"))
                {
                    return await ProcessJsonResponseAsync(response, request.Model ?? ProviderModelId, cancellationToken);
                }
                else
                {
                    // Handle plain text response
                    string textResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                    return CreateChatCompletionResponse(request.Model ?? ProviderModelId, textResponse);
                }
            }, "ChatCompletion", cancellationToken);
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "StreamChatCompletion");

            Logger.LogInformation("Streaming is not natively supported in HuggingFace Inference API client. Simulating streaming.");

            // HuggingFace Inference API doesn't support streaming directly
            // Simulate streaming by breaking up the response
            var fullResponse = await CreateChatCompletionAsync(request, apiKey, cancellationToken);

            if (fullResponse.Choices == null || !fullResponse.Choices.Any() ||
                fullResponse.Choices[0].Message?.Content == null)
            {
                yield break;
            }

            // Simulate streaming by breaking up the content
            string content = ContentHelper.GetContentAsString(fullResponse.Choices[0].Message!.Content);

            // Generate a random ID for this streaming session
            string streamId = Guid.NewGuid().ToString();

            // Initial chunk with role
            yield return new ChatCompletionChunk
            {
                Id = streamId,
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model ?? ProviderModelId,
                Choices = new List<StreamingChoice>
                {
                    new StreamingChoice
                    {
                        Index = 0,
                        Delta = new DeltaContent
                        {
                            Role = "assistant",
                            Content = null
                        }
                    }
                }
            };

            // Break content into chunks (words or sentences could be used)
            var words = content.Split(' ');

            // Send content in chunks
            StringBuilder currentChunk = new StringBuilder();
            foreach (var word in words)
            {
                // Add delay to simulate real streaming
                await Task.Delay(25, cancellationToken);

                currentChunk.Append(word).Append(' ');

                // Send every few words
                if (currentChunk.Length > 0)
                {
                    yield return new ChatCompletionChunk
                    {
                        Id = streamId,
                        Object = "chat.completion.chunk",
                        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Model = request.Model ?? ProviderModelId,
                        Choices = new List<StreamingChoice>
                        {
                            new StreamingChoice
                            {
                                Index = 0,
                                Delta = new DeltaContent
                                {
                                    Content = currentChunk.ToString()
                                }
                            }
                        }
                    };

                    currentChunk.Clear();
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }
            }

            // Final chunk with finish reason
            yield return new ChatCompletionChunk
            {
                Id = streamId,
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model ?? ProviderModelId,
                Choices = new List<StreamingChoice>
                {
                    new StreamingChoice
                    {
                        Index = 0,
                        Delta = new DeltaContent(),
                        FinishReason = fullResponse.Choices[0].FinishReason
                    }
                }
            };
        }

        /// <inheritdoc />
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("HuggingFace Inference API does not provide a model listing endpoint. Returning commonly used models.");

            // HuggingFace Inference API doesn't have an endpoint to list models
            // Return a list of popular models as an example
            await Task.Delay(1, cancellationToken); // Adding await to make this truly async

            return new List<ExtendedModelInfo>
            {
                ExtendedModelInfo.Create("gpt2", ProviderName, "gpt2"),
                ExtendedModelInfo.Create("mistralai/Mistral-7B-Instruct-v0.2", ProviderName, "mistralai/Mistral-7B-Instruct-v0.2"),
                ExtendedModelInfo.Create("meta-llama/Llama-2-7b-chat-hf", ProviderName, "meta-llama/Llama-2-7b-chat-hf"),
                ExtendedModelInfo.Create("facebook/bart-large-cnn", ProviderName, "facebook/bart-large-cnn"),
                ExtendedModelInfo.Create("google/flan-t5-xl", ProviderName, "google/flan-t5-xl"),
                ExtendedModelInfo.Create("EleutherAI/gpt-neox-20b", ProviderName, "EleutherAI/gpt-neox-20b"),
                ExtendedModelInfo.Create("bigscience/bloom", ProviderName, "bigscience/bloom"),
                ExtendedModelInfo.Create("microsoft/DialoGPT-large", ProviderName, "microsoft/DialoGPT-large"),
                ExtendedModelInfo.Create("sentence-transformers/all-MiniLM-L6-v2", ProviderName, "sentence-transformers/all-MiniLM-L6-v2"),
                ExtendedModelInfo.Create("tiiuae/falcon-7b-instruct", ProviderName, "tiiuae/falcon-7b-instruct")
            };
        }

        /// <inheritdoc />
        public override Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Embeddings are not yet supported in the HuggingFace client.");
        }

        /// <inheritdoc />
        public override Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Image generation is not yet supported in the HuggingFace client.");
        }

        #region Helper Methods

        private string GetHuggingFaceEndpoint(string modelId)
        {
            // If the base URL already contains the model endpoint, don't add it again
            if (Credentials.ApiBase != null && Credentials.ApiBase.Contains("/models/"))
            {
                return string.Empty; // Use the base address as is
            }

            return modelId;
        }

        private string FormatChatMessages(List<Message> messages)
        {
            // Different models expect different formats
            // For simplicity, we'll use a generic chat format here
            // In a real implementation, you would adapt this based on the model

            StringBuilder formattedChat = new StringBuilder();

            // Extract system message if present
            var systemMessage = messages.FirstOrDefault(m =>
                m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));

            if (systemMessage != null)
            {
                formattedChat.AppendLine(ContentHelper.GetContentAsString(systemMessage.Content));
                formattedChat.AppendLine();
            }

            // Format the conversation
            foreach (var message in messages.Where(m =>
                !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)))
            {
                string role = message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                    ? "Assistant"
                    : "Human";

                formattedChat.AppendLine($"{role}: {ContentHelper.GetContentAsString(message.Content)}");
            }

            // Add final prompt for assistant
            formattedChat.Append("Assistant: ");

            return formattedChat.ToString();
        }

        private async Task<ChatCompletionResponse> ProcessJsonResponseAsync(
            HttpResponseMessage response,
            string originalModelAlias,
            CancellationToken cancellationToken)
        {
            try
            {
                // Try to parse as array first (some HF models return array of results)
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (jsonContent.TrimStart().StartsWith("["))
                {
                    // Parse as array
                    var arrayResponse = JsonSerializer.Deserialize<List<HuggingFaceTextGenerationResponse>>(
                        jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (arrayResponse != null && arrayResponse.Count > 0)
                    {
                        return CreateChatCompletionResponse(originalModelAlias, arrayResponse[0].GeneratedText ?? string.Empty);
                    }
                }
                else
                {
                    // Parse as single object
                    var objectResponse = JsonSerializer.Deserialize<HuggingFaceTextGenerationResponse>(
                        jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (objectResponse != null)
                    {
                        return CreateChatCompletionResponse(originalModelAlias, objectResponse.GeneratedText ?? string.Empty);
                    }
                }

                // If all fails, return error
                Logger.LogError("Could not parse HuggingFace response: {Content}", jsonContent);
                throw new LLMCommunicationException("Invalid response format from HuggingFace Inference API");
            }
            catch (JsonException ex)
            {
                Logger.LogError(ex, "Error parsing JSON response from HuggingFace");
                throw new LLMCommunicationException("Error parsing JSON response from HuggingFace Inference API", ex);
            }
        }

        private ChatCompletionResponse CreateChatCompletionResponse(string model, string content)
        {
            // Estimate token counts based on text length
            int estimatedPromptTokens = EstimateTokenCount(content);
            int estimatedCompletionTokens = EstimateTokenCount(content);

            return new ChatCompletionResponse
            {
                Id = Guid.NewGuid().ToString(),
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = model,
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
                    // HuggingFace doesn't provide token usage
                    // Provide estimated counts based on text length
                    PromptTokens = estimatedPromptTokens,
                    CompletionTokens = estimatedCompletionTokens,
                    TotalTokens = estimatedPromptTokens + estimatedCompletionTokens
                }
            };
        }

        private int EstimateTokenCount(string text)
        {
            // Rough token count estimation
            if (string.IsNullOrEmpty(text))
                return 0;

            // Approximately 4 characters per token for English text
            // This is a rough estimate that works for many models but isn't exact
            return Math.Max(1, text.Length / 4);
        }

        #endregion
    }
}
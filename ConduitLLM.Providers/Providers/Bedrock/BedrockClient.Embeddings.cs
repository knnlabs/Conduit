using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Providers.Bedrock.Models;

namespace ConduitLLM.Providers.Providers.Bedrock
{
    /// <summary>
    /// BedrockClient partial class containing embedding functionality.
    /// </summary>
    public partial class BedrockClient
    {
        /// <inheritdoc />
        public override async Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateEmbedding");

            string modelId = request.Model ?? ProviderModelId;

            if (modelId.Contains("cohere.embed", StringComparison.OrdinalIgnoreCase))
            {
                return await CreateCohereEmbeddingAsync(request, modelId, cancellationToken);
            }
            else if (modelId.Contains("amazon.titan-embed", StringComparison.OrdinalIgnoreCase))
            {
                return await CreateTitanEmbeddingAsync(request, modelId, cancellationToken);
            }
            else
            {
                throw new UnsupportedProviderException($"The model {modelId} does not support embeddings in Bedrock");
            }
        }

        /// <summary>
        /// Creates embeddings using Cohere models via Bedrock.
        /// </summary>
        private async Task<EmbeddingResponse> CreateCohereEmbeddingAsync(
            EmbeddingRequest request, 
            string modelId, 
            CancellationToken cancellationToken)
        {
            // Convert input to list format for Cohere
            var inputTexts = request.Input is string singleInput 
                ? new List<string> { singleInput }
                : request.Input is List<string> listInput 
                    ? listInput 
                    : throw new ArgumentException("Invalid input format for embeddings");

            var cohereRequest = new BedrockCohereEmbeddingRequest
            {
                Texts = inputTexts,
                InputType = "search_document", // Default for general embeddings
                Truncate = "END"
            };

            using var client = CreateHttpClient(PrimaryKeyCredential.ApiKey);
            string apiUrl = $"/model/{modelId}/invoke";

            // Send request with AWS Signature V4 authentication
            var response = await SendBedrockRequestAsync(client, HttpMethod.Post, apiUrl, cohereRequest, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"Bedrock API error: {response.StatusCode} - {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var cohereResponse = JsonSerializer.Deserialize<BedrockCohereEmbeddingResponse>(responseContent, JsonOptions);
            
            if (cohereResponse?.Embeddings == null)
            {
                throw new ConduitException("Invalid response from Cohere embedding model");
            }

            var embeddingObjects = cohereResponse.Embeddings.Select((embedding, index) => new EmbeddingData
            {
                Index = index,
                Embedding = embedding,
                Object = "embedding"
            }).ToList();

            return new EmbeddingResponse
            {
                Object = "list",
                Data = embeddingObjects,
                Model = modelId,
                Usage = new Usage
                {
                    PromptTokens = cohereResponse.Meta?.BilledUnits?.InputTokens ?? EstimateTokenCount(string.Join(" ", inputTexts)),
                    CompletionTokens = 0, // Embeddings don't generate completion tokens
                    TotalTokens = cohereResponse.Meta?.BilledUnits?.InputTokens ?? EstimateTokenCount(string.Join(" ", inputTexts))
                }
            };
        }

        /// <summary>
        /// Creates embeddings using Amazon Titan models via Bedrock.
        /// </summary>
        private async Task<EmbeddingResponse> CreateTitanEmbeddingAsync(
            EmbeddingRequest request, 
            string modelId, 
            CancellationToken cancellationToken)
        {
            // Titan only supports single input text
            var inputText = request.Input is string singleInput 
                ? singleInput
                : request.Input is List<string> listInput && listInput.Count == 1
                    ? listInput[0]
                    : throw new ArgumentException("Amazon Titan embeddings only support single text input");

            var titanRequest = new BedrockTitanEmbeddingRequest
            {
                InputText = inputText,
                Dimensions = request.Dimensions,
                Normalize = true // Recommended for most use cases
            };

            using var client = CreateHttpClient(PrimaryKeyCredential.ApiKey);
            string apiUrl = $"/model/{modelId}/invoke";

            // Send request with AWS Signature V4 authentication
            var response = await SendBedrockRequestAsync(client, HttpMethod.Post, apiUrl, titanRequest, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"Bedrock API error: {response.StatusCode} - {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var titanResponse = JsonSerializer.Deserialize<BedrockTitanEmbeddingResponse>(responseContent, JsonOptions);
            
            if (titanResponse?.Embedding == null)
            {
                throw new ConduitException("Invalid response from Titan embedding model");
            }

            var embeddingObject = new EmbeddingData
            {
                Index = 0,
                Embedding = titanResponse.Embedding,
                Object = "embedding"
            };

            return new EmbeddingResponse
            {
                Object = "list",
                Data = new List<EmbeddingData> { embeddingObject },
                Model = modelId,
                Usage = new Usage
                {
                    PromptTokens = titanResponse.InputTextTokenCount ?? EstimateTokenCount(inputText),
                    CompletionTokens = 0, // Embeddings don't generate completion tokens
                    TotalTokens = titanResponse.InputTextTokenCount ?? EstimateTokenCount(inputText)
                }
            };
        }
    }
}
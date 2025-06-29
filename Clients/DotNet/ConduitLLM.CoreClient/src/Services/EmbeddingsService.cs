using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.CoreClient.Client;
using ConduitLLM.CoreClient.Constants;
using ConduitLLM.CoreClient.Exceptions;
using ConduitLLM.CoreClient.Models;
using ConduitLLM.CoreClient.Utils;

namespace ConduitLLM.CoreClient.Services
{
    /// <summary>
    /// Service for creating text embeddings using the Conduit Core API.
    /// </summary>
    public class EmbeddingsService
    {
        private readonly BaseClient _client;
        private readonly ILogger<EmbeddingsService>? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmbeddingsService"/> class.
        /// </summary>
        /// <param name="client">The base client for making API requests.</param>
        /// <param name="logger">Optional logger for debugging and monitoring.</param>
        public EmbeddingsService(BaseClient client, ILogger<EmbeddingsService>? logger = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger;
        }

        /// <summary>
        /// Creates embeddings for the given input text(s).
        /// </summary>
        /// <param name="request">The embedding request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The embedding response containing the generated embeddings.</returns>
        /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
        /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
        public async Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateRequest(request);
                
                _logger?.LogDebug("Creating embeddings with model {Model}", request.Model);

                var response = await _client.PostForServiceAsync<EmbeddingResponse>(
                    ApiEndpoints.V1.Embeddings.Base,
                    request,
                    cancellationToken);

                _logger?.LogDebug("Successfully created {Count} embeddings using {Tokens} tokens", 
                    response.Data.Count, response.Usage.TotalTokens);

                return response;
            }
            catch (Exception ex) when (!(ex is ConduitCoreException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Creates embeddings for a single text input.
        /// </summary>
        /// <param name="text">The input text.</param>
        /// <param name="model">The model to use (defaults to text-embedding-3-small).</param>
        /// <param name="dimensions">Optional number of dimensions for the output embeddings.</param>
        /// <param name="encodingFormat">The format to return the embeddings in.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The embedding vector for the input text.</returns>
        public async Task<float[]> CreateEmbeddingAsync(
            string text,
            string? model = null,
            int? dimensions = null,
            string? encodingFormat = null,
            CancellationToken cancellationToken = default)
        {
            var request = new EmbeddingRequest
            {
                Input = text,
                Model = model ?? EmbeddingModels.Default,
                Dimensions = dimensions,
                EncodingFormat = encodingFormat
            };

            var response = await CreateEmbeddingAsync(request, cancellationToken);
            
            if (response.Data.Count == 0)
                throw new ConduitCoreException("No embeddings returned", null, null, null, null);

            return ConvertEmbeddingToFloatArray(response.Data[0].Embedding);
        }

        /// <summary>
        /// Creates embeddings for multiple text inputs.
        /// </summary>
        /// <param name="texts">The input texts.</param>
        /// <param name="model">The model to use (defaults to text-embedding-3-small).</param>
        /// <param name="dimensions">Optional number of dimensions for the output embeddings.</param>
        /// <param name="encodingFormat">The format to return the embeddings in.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of embedding vectors for each input text.</returns>
        public async Task<List<float[]>> CreateEmbeddingsAsync(
            IEnumerable<string> texts,
            string? model = null,
            int? dimensions = null,
            string? encodingFormat = null,
            CancellationToken cancellationToken = default)
        {
            var textList = texts.ToList();
            if (textList.Count == 0)
                throw new ArgumentException("At least one text input is required", nameof(texts));

            var request = new EmbeddingRequest
            {
                Input = textList,
                Model = model ?? EmbeddingModels.Default,
                Dimensions = dimensions,
                EncodingFormat = encodingFormat
            };

            var response = await CreateEmbeddingAsync(request, cancellationToken);
            
            // Sort by index to ensure correct order
            return response.Data
                .OrderBy(d => d.Index)
                .Select(d => ConvertEmbeddingToFloatArray(d.Embedding))
                .ToList();
        }

        /// <summary>
        /// Calculates the cosine similarity between two embedding vectors.
        /// </summary>
        /// <param name="embedding1">The first embedding vector.</param>
        /// <param name="embedding2">The second embedding vector.</param>
        /// <returns>The cosine similarity between -1 and 1.</returns>
        public static double CalculateCosineSimilarity(float[] embedding1, float[] embedding2)
        {
            if (embedding1.Length != embedding2.Length)
                throw new ArgumentException("Embeddings must have the same dimensions");

            double dotProduct = 0;
            double magnitude1 = 0;
            double magnitude2 = 0;

            for (int i = 0; i < embedding1.Length; i++)
            {
                dotProduct += embedding1[i] * embedding2[i];
                magnitude1 += embedding1[i] * embedding1[i];
                magnitude2 += embedding2[i] * embedding2[i];
            }

            magnitude1 = Math.Sqrt(magnitude1);
            magnitude2 = Math.Sqrt(magnitude2);

            if (magnitude1 == 0 || magnitude2 == 0)
                return 0;

            return dotProduct / (magnitude1 * magnitude2);
        }

        /// <summary>
        /// Finds the most similar text from a list of candidates to a query text.
        /// </summary>
        /// <param name="query">The query text.</param>
        /// <param name="candidates">The list of candidate texts.</param>
        /// <param name="model">The model to use for embeddings.</param>
        /// <param name="topK">Number of top results to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of candidates sorted by similarity with their scores.</returns>
        public async Task<List<(string Text, double Similarity)>> FindMostSimilarAsync(
            string query,
            IEnumerable<string> candidates,
            string? model = null,
            int topK = 1,
            CancellationToken cancellationToken = default)
        {
            var candidateList = candidates.ToList();
            if (candidateList.Count == 0)
                throw new ArgumentException("At least one candidate is required", nameof(candidates));

            // Create embeddings for query and all candidates
            var allTexts = new List<string> { query };
            allTexts.AddRange(candidateList);

            var embeddings = await CreateEmbeddingsAsync(allTexts, model, cancellationToken: cancellationToken);
            var queryEmbedding = embeddings[0];
            var candidateEmbeddings = embeddings.Skip(1).ToList();

            // Calculate similarities
            var similarities = candidateList
                .Select((text, index) => (
                    Text: text,
                    Similarity: CalculateCosineSimilarity(queryEmbedding, candidateEmbeddings[index])
                ))
                .OrderByDescending(x => x.Similarity)
                .Take(topK)
                .ToList();

            return similarities;
        }

        private static void ValidateRequest(EmbeddingRequest request)
        {
            if (request == null)
                throw new ValidationException("Request cannot be null", "request");

            if (request.Input == null)
                throw new ValidationException("Input is required", "input");

            if (string.IsNullOrWhiteSpace(request.Model))
                throw new ValidationException("Model is required", "model");

            // Validate input type
            if (request.Input is string str)
            {
                if (string.IsNullOrWhiteSpace(str))
                    throw new ValidationException("Input text cannot be empty", "input");
            }
            else if (request.Input is IEnumerable<string> texts)
            {
                var textList = texts.ToList();
                if (textList.Count == 0)
                    throw new ValidationException("At least one input text is required", "input");
                
                if (textList.Any(string.IsNullOrWhiteSpace))
                    throw new ValidationException("Input texts cannot be null or empty", "input");
            }
            else
            {
                throw new ValidationException("Input must be a string or array of strings", "input");
            }

            if (request.EncodingFormat != null && 
                request.EncodingFormat != EmbeddingEncodingFormats.Float &&
                request.EncodingFormat != EmbeddingEncodingFormats.Base64)
            {
                throw new ValidationException($"Encoding format must be '{EmbeddingEncodingFormats.Float}' or '{EmbeddingEncodingFormats.Base64}'", "encoding_format");
            }

            if (request.Dimensions.HasValue && request.Dimensions.Value <= 0)
                throw new ValidationException("Dimensions must be a positive integer", "dimensions");
        }

        private static float[] ConvertEmbeddingToFloatArray(object embedding)
        {
            if (embedding is System.Text.Json.JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var floats = new List<float>();
                    foreach (var element in jsonElement.EnumerateArray())
                    {
                        floats.Add((float)element.GetDouble());
                    }
                    return floats.ToArray();
                }
                else if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    // Base64 encoded embedding
                    var base64 = jsonElement.GetString();
                    if (string.IsNullOrEmpty(base64))
                        throw new ConduitCoreException("Base64 embedding string is empty", null, null, null, null);
                    
                    var bytes = Convert.FromBase64String(base64);
                    var floats = new float[bytes.Length / sizeof(float)];
                    Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
                    return floats;
                }
            }
            else if (embedding is IEnumerable<float> floatEnumerable)
            {
                return floatEnumerable.ToArray();
            }
            else if (embedding is float[] floatArray)
            {
                return floatArray;
            }

            throw new ConduitCoreException($"Unexpected embedding type: {embedding?.GetType()?.Name ?? "null"}", null, null, null, null);
        }
    }
}
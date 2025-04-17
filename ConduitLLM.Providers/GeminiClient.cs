using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices; // For IAsyncEnumerable
using System.Text;
using System.Text.Json;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.InternalModels;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers;

/// <summary>
/// Client for interacting with the Google Gemini API.
/// </summary>
public class GeminiClient : ILLMClient
{
    // TODO: Refactor to use IHttpClientFactory
    private readonly HttpClient _httpClient;
    private readonly ProviderCredentials _credentials;
    private readonly string _providerModelId; // The actual model ID for Gemini (e.g., gemini-1.5-flash-latest)
    private readonly ILogger<GeminiClient> _logger;
    private readonly string _apiBaseUri;
    private readonly string _apiVersion;

    // Base URL for Gemini API
    private const string DefaultApiBase = "https://generativelanguage.googleapis.com/";
    private const string ApiVersion = "v1beta"; // Or specific version like v1

    public GeminiClient(ProviderCredentials credentials, string providerModelId, ILogger<GeminiClient> logger, HttpClient? httpClient = null)
    {
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _providerModelId = providerModelId ?? throw new ArgumentNullException(nameof(providerModelId));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            throw new ConfigurationException($"API key is missing for provider '{credentials.ProviderName}'.");
        }

        // Allow injection of HttpClient for testing
        _httpClient = httpClient ?? new HttpClient();
        
        // Use ApiBase from credentials if provided, otherwise default
        string apiBase = string.IsNullOrWhiteSpace(credentials.ApiBase) ? DefaultApiBase : credentials.ApiBase;
        // Ensure ApiBase ends with a slash
        if (!apiBase.EndsWith('/'))
        {
            apiBase += "/";
        }
        _apiBaseUri = apiBase;
        _apiVersion = ApiVersion;
        _httpClient.BaseAddress = new Uri(apiBase);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // Google Gemini use API key in query parameter, not in headers
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");
    }

    /// <inheritdoc />
    public async Task<ChatCompletionResponse> CreateChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null, // Added optional API key
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Determine the API key to use: override if provided, otherwise use configured key
        string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : _credentials.ApiKey!;
        if (string.IsNullOrWhiteSpace(effectiveApiKey))
        {
             // This should ideally not happen if constructor validation is correct, but double-check
             throw new ConfigurationException($"API key is missing for provider '{_credentials.ProviderName}' and no override was provided.");
        }

        _logger.LogInformation("Mapping Core request to Gemini request for model alias '{ModelAlias}', provider model ID '{ProviderModelId}'", request.Model, _providerModelId);
        var geminiRequest = MapToGeminiRequest(request);

        HttpResponseMessage? response = null;
        try
        {
            var requestUri = $"{_apiBaseUri}{_apiVersion}/models/{_providerModelId}:generateContent?key={effectiveApiKey}";
            _logger.LogDebug("Sending request to Gemini API: {Endpoint}", requestUri);

            response = await _httpClient.PostAsJsonAsync(requestUri, geminiRequest, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiGenerateContentResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
                
                if (geminiResponse == null)
                {
                    _logger.LogError("Failed to deserialize Gemini response despite 200 OK status");
                    throw new LLMCommunicationException("Failed to deserialize the successful response from Gemini API.");
                }

                // Check for safety filtering response
                if (geminiResponse.Candidates != null && 
                    geminiResponse.Candidates.Count > 0 && 
                    geminiResponse.Candidates[0].FinishReason == "SAFETY" &&
                    geminiResponse.Candidates[0].SafetyRatings != null)
                {
                    string safetyDetails = string.Join(", ", geminiResponse.Candidates[0].SafetyRatings?
                        .Where(r => r != null && r.Probability != "NEGLIGIBLE")
                        .Select(r => $"{r.Category}: {r.Probability}") ?? Array.Empty<string>());
                    
                    _logger.LogWarning("Gemini response blocked due to safety settings: {Details}", safetyDetails);
                    throw new LLMCommunicationException($"Gemini response blocked due to safety settings: {safetyDetails}");
                }

                var coreResponse = MapToCoreResponse(geminiResponse, request.Model);
                _logger.LogInformation("Successfully received and mapped Gemini response.");
                return coreResponse;
            }
            else
            {
                string errorContent = await ReadErrorContentAsync(response, cancellationToken).ConfigureAwait(false);
                _logger.LogError("Gemini API request failed with status code {StatusCode}. Response: {ErrorContent}", response.StatusCode, errorContent);
                
                try
                {
                    // Try to parse as Gemini error JSON
                    var errorDto = JsonSerializer.Deserialize<GeminiErrorResponse>(errorContent);
                    if (errorDto?.Error != null)
                    {
                        string errorMessage = $"Gemini API Error {errorDto.Error.Code} ({errorDto.Error.Status}): {errorDto.Error.Message}";
                        _logger.LogError(errorMessage);
                        throw new LLMCommunicationException(errorMessage);
                    }
                }
                catch (JsonException)
                {
                    // If it's not a parsable JSON or doesn't follow the expected format, use a generic message
                    _logger.LogWarning("Could not parse Gemini error response as JSON. Treating as plain text.");
                }
                
                // Default error if JSON parsing failed or unexpected format
                throw new LLMCommunicationException($"Gemini API request failed with status code {response.StatusCode}. Response: {errorContent}");
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON error processing Gemini response.");
            throw new LLMCommunicationException("Error deserializing Gemini response.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request error communicating with Gemini API.");
            throw new LLMCommunicationException($"HTTP request error communicating with Gemini API: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning(ex, "Gemini API request timed out.");
            throw new LLMCommunicationException("Gemini API request timed out.", ex);
        }
        catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(ex, "Gemini API request was cancelled by the caller.");
            throw; // Re-throw cancellation
        }
        catch (LLMCommunicationException)
        {
            // Re-throw LLMCommunicationException directly, already logged
            throw;
        }
        catch (Exception ex) // Catch-all for unexpected errors
        {
            _logger.LogError(ex, "An unexpected error occurred during Gemini API request.");
            throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
        }
        finally
        {
            response?.Dispose();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null, // Added optional API key
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Determine the API key to use
        string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : _credentials.ApiKey!;
        if (string.IsNullOrWhiteSpace(effectiveApiKey))
        {
            throw new ConfigurationException($"API key is missing for provider '{_credentials.ProviderName}' and no override was provided.");
        }

        _logger.LogInformation("Preparing streaming request to Gemini for model alias '{ModelAlias}', provider model ID '{ProviderModelId}'", request.Model, _providerModelId);

        // Create Gemini request
        var geminiRequest = MapToGeminiRequest(request);
        
        // Store original model alias for response mapping
        string originalModelAlias = request.Model;

        // Setup and send initial request
        HttpResponseMessage? response = null;
        try
        {
            response = await SetupAndSendStreamingRequestAsync(geminiRequest, effectiveApiKey, cancellationToken).ConfigureAwait(false);
        }
        catch (LLMCommunicationException)
        {
            // If it's already a properly formatted LLMCommunicationException, just re-throw it
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate Gemini streaming request");
            throw new LLMCommunicationException($"Failed to initiate Gemini stream: {ex.Message}", ex);
        }

        // Process the stream
        try
        {
            await foreach (var chunk in ProcessGeminiStreamAsync(response, originalModelAlias, cancellationToken).ConfigureAwait(false))
            {
                yield return chunk;
            }
        }
        finally
        {
            response?.Dispose();
        }
    }

    // Helper to setup and send the initial streaming request
    private async Task<HttpResponseMessage> SetupAndSendStreamingRequestAsync(
        GeminiGenerateContentRequest geminiRequest,
        string effectiveApiKey,
        CancellationToken cancellationToken)
    {
        // Add streaming parameter to the URL
        var endpoint = $"{_apiBaseUri}{_apiVersion}/models/{_providerModelId}:streamGenerateContent?key={effectiveApiKey}&alt=sse";
        _logger.LogDebug("Sending streaming request to Gemini API: {Endpoint}", endpoint);

        // Create request message
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(geminiRequest)
        };

        // Send request with ResponseHeadersRead to get streaming
        var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await ReadErrorContentAsync(response, cancellationToken).ConfigureAwait(false);
            _logger.LogError("Gemini streaming request failed with status {Status}. Response: {ErrorContent}", response.StatusCode, errorContent);
            
            try
            {
                // Try to parse as Gemini error JSON
                var errorDto = JsonSerializer.Deserialize<GeminiErrorResponse>(errorContent);
                if (errorDto?.Error != null)
                {
                    // Direct error format used by the test
                    throw new LLMCommunicationException($"Gemini API Error {errorDto.Error.Code} ({errorDto.Error.Status}): {errorDto.Error.Message}");
                }
            }
            catch (JsonException)
            {
                // Not JSON, fall through to default message
            }
            
            throw new LLMCommunicationException($"Gemini API request failed with status code {response.StatusCode}. Response: {errorContent}");
        }

        return response;
    }

    // Helper to process the actual stream content
    private async IAsyncEnumerable<ChatCompletionChunk> ProcessGeminiStreamAsync(HttpResponseMessage response, string originalModelAlias, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Stream? responseStream = null;
        StreamReader? reader = null;

        try
        {
            responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            reader = new StreamReader(responseStream, Encoding.UTF8);

            // Process the stream line by line (expecting SSE format due to alt=sse)
            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false); // Use ReadLineAsync with CancellationToken

                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:"))
                {
                    // Skip empty lines or non-data lines if using SSE format
                    continue;
                }

                string jsonData = line.Substring("data:".Length).Trim();

                if (string.IsNullOrWhiteSpace(jsonData))
                {
                    continue; // Skip if data part is empty
                }

                ChatCompletionChunk? chunk = null;
                try
                {
                    var geminiResponse = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(jsonData);
                    if (geminiResponse != null)
                    {
                        // Map the Gemini response (which contains the delta) to our core chunk
                        // Pass originalModelAlias for mapping
                        chunk = MapToCoreChunk(geminiResponse, originalModelAlias);
                    }
                    else
                    {
                        _logger.LogWarning("Deserialized Gemini stream chunk was null. JSON: {JsonData}", jsonData);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "JSON deserialization error processing Gemini stream chunk. JSON: {JsonData}", jsonData);
                    // Throw to indicate stream corruption
                    throw new LLMCommunicationException($"Error deserializing Gemini stream chunk: {ex.Message}. Data: {jsonData}", ex);
                }
                catch (LLMCommunicationException llmEx) // Catch mapping/validation errors from MapToCoreChunk
                {
                     _logger.LogError(llmEx, "Error processing Gemini stream chunk content. JSON: {JsonData}", jsonData);
                     throw; // Re-throw specific communication errors
                }
                catch (Exception ex) // Catch unexpected mapping errors
                {
                     _logger.LogError(ex, "Unexpected error mapping Gemini stream chunk. JSON: {JsonData}", jsonData);
                     throw new LLMCommunicationException($"Unexpected error mapping Gemini stream chunk: {ex.Message}. Data: {jsonData}", ex);
                }

                if (chunk != null) // MapToCoreChunk might return null if there's no usable delta
                {
                    yield return chunk;
                }
            }
             _logger.LogInformation("Finished processing Gemini stream.");
        }
        // Exceptions during stream reading/processing will propagate out
        finally
        {
            // Ensure resources are disposed even if exceptions occur during processing
            reader?.Dispose();
            responseStream?.Dispose();
            response.Dispose(); // Dispose the response object itself
            _logger.LogDebug("Disposed Gemini stream resources.");
        }
    }

    // --- Mapping Logic ---

    private GeminiGenerateContentRequest MapToGeminiRequest(ChatCompletionRequest coreRequest)
    {
        var contents = new List<GeminiContent>();
        // Gemini requires alternating user/model roles. Handle system prompt separately.
        string? systemInstruction = null; // Gemini v1.5 supports system_instruction at the top level

        foreach (var msg in coreRequest.Messages)
        {
            if (string.IsNullOrWhiteSpace(msg.Role)) throw new ArgumentException("Message role cannot be null or empty.", nameof(coreRequest.Messages));
            if (string.IsNullOrWhiteSpace(msg.Content)) throw new ArgumentException("Message content cannot be null or empty.", nameof(coreRequest.Messages));

            string geminiRole;
            if (msg.Role.Equals(MessageRole.System, StringComparison.OrdinalIgnoreCase))
            {
                // Handle system message - Gemini v1.5 prefers system_instruction
                // For older models, prepend to the first user message or handle as context.
                // Assuming v1.5+ for now. If multiple system messages, maybe concatenate? Use last one?
                systemInstruction = msg.Content;
                continue; // Don't add system messages to 'contents'
            }
            else if (msg.Role.Equals(MessageRole.User, StringComparison.OrdinalIgnoreCase))
            {
                geminiRole = "user";
            }
            else if (msg.Role.Equals(MessageRole.Assistant, StringComparison.OrdinalIgnoreCase))
            {
                geminiRole = "model"; // Gemini uses "model" for assistant role
            }
            else
            {
                 _logger.LogWarning("Unsupported message role '{Role}' encountered for Gemini provider. Skipping message.", msg.Role);
                 continue;
            }

            contents.Add(new GeminiContent
            {
                Role = geminiRole,
                Parts = new List<GeminiPart> { new GeminiPart { Text = msg.Content } }
            });
        }

        // Basic validation for Gemini's turn structure
        if (contents.Count == 0 && string.IsNullOrWhiteSpace(systemInstruction)) // Need at least one user message if no system instruction
        {
             throw new ArgumentException("No user messages provided for Gemini request.", nameof(coreRequest.Messages));
        }
        if (contents.Count > 0 && contents.Last().Role != "user")
        {
            // Gemini requires the last message to be from the user if contents are present
            throw new ArgumentException("Invalid message sequence for Gemini. The last message must be from the 'user'.", nameof(coreRequest.Messages));
        }
        // TODO: Add validation for strictly alternating user/model roles if needed.

        var config = new GeminiGenerationConfig
        {
            Temperature = (float?)coreRequest.Temperature,
            TopP = (float?)coreRequest.TopP,
            // TopK = coreRequest.TopK, // Map if added to Core model
            CandidateCount = coreRequest.N, // Map N to candidateCount
            MaxOutputTokens = coreRequest.MaxTokens,
            StopSequences = coreRequest.Stop
        };

         return new GeminiGenerateContentRequest
         {
             Contents = contents,
             GenerationConfig = (config.Temperature.HasValue || config.TopP.HasValue || config.TopK.HasValue ||
                                config.CandidateCount.HasValue || config.MaxOutputTokens.HasValue || config.StopSequences != null)
                                ? config : null
             // SafetySettings = ... // Map if needed
             // SystemInstruction = ... // Add if supporting v1.5+ explicitly
         };
    }


    private ChatCompletionResponse MapToCoreResponse(GeminiGenerateContentResponse geminiResponse, string originalModelAlias)
    {
        // Validated in caller: geminiResponse, Candidates, Candidates[0], Content, Parts, Text, UsageMetadata are not null.
        var firstCandidate = geminiResponse.Candidates![0];
        var firstPart = firstCandidate.Content!.Parts!.First();
        var usageMetadata = geminiResponse.UsageMetadata!;

        var choice = new Choice
        {
            Index = firstCandidate.Index,
            Message = new Message
            {
                // Gemini uses "model" for assistant role
                Role = firstCandidate.Content.Role == "model" ? MessageRole.Assistant : firstCandidate.Content.Role,
                Content = firstPart.Text ?? string.Empty // Add null check with empty string default
            },
            FinishReason = MapFinishReason(firstCandidate.FinishReason) ?? string.Empty // Add null check with empty string default
        };

        return new ChatCompletionResponse
        {
            Id = Guid.NewGuid().ToString(), // Gemini doesn't provide an ID
            Object = "chat.completion", // Mimic OpenAI structure
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), // Use current time as Gemini doesn't provide it
            Model = originalModelAlias, // Return the alias the user requested
            Choices = new List<Choice> { choice },
            Usage = new Usage
            {
                PromptTokens = usageMetadata.PromptTokenCount,
                CompletionTokens = usageMetadata.CandidatesTokenCount, // Sum across candidates (usually 1)
                TotalTokens = usageMetadata.TotalTokenCount
            }
            // SystemFingerprint = null // Gemini doesn't provide this
        };
    }

     // New mapping function for streaming chunks (maps from GeminiGenerateContentResponse)
    private ChatCompletionChunk? MapToCoreChunk(GeminiGenerateContentResponse geminiResponse, string originalModelAlias)
    {
        // Extract the relevant delta information from the Gemini response structure
        var firstCandidate = geminiResponse.Candidates?.FirstOrDefault();
        var firstPart = firstCandidate?.Content?.Parts?.FirstOrDefault();
        string? deltaText = firstPart?.Text;
        string? finishReason = MapFinishReason(firstCandidate?.FinishReason) ?? string.Empty; // Add null check with empty string default

        // Only yield a chunk if there's actual text content or a finish reason
        if (string.IsNullOrEmpty(deltaText) && finishReason == null)
        {
            // This might happen for intermediate responses without text, or prompt feedback responses.
            // Check for prompt feedback block reason
            if (geminiResponse.PromptFeedback?.BlockReason != null)
            {
                 _logger.LogWarning("Gemini stream blocked due to prompt feedback. Reason: {BlockReason}", geminiResponse.PromptFeedback.BlockReason);
                 // Throw an exception to stop the stream processing?
                 throw new LLMCommunicationException($"Gemini stream blocked due to prompt feedback. Reason: {geminiResponse.PromptFeedback.BlockReason}");
            }
             // Check for safety block in candidate
            if (firstCandidate?.FinishReason == "SAFETY")
            {
                 _logger.LogWarning("Gemini stream blocked due to safety settings (finishReason: SAFETY).");
                 throw new LLMCommunicationException("Gemini stream blocked due to safety settings.");
            }

            _logger.LogTrace("Skipping Gemini stream chunk mapping as no delta text or finish reason found.");
            return null;
        }


        var choice = new StreamingChoice
        {
            Index = firstCandidate?.Index ?? 0,
            Delta = new DeltaContent
            {
                // Gemini doesn't explicitly provide role in delta chunks, assume assistant?
                // Role = firstCandidate?.Content?.Role == "model" ? MessageRole.Assistant : null, // Role might only be in first chunk?
                Content = deltaText // Can be null if only finish reason is present
            },
            FinishReason = finishReason // Can be null
        };

        return new ChatCompletionChunk
        {
            Id = Guid.NewGuid().ToString(), // Gemini doesn't provide chunk IDs
            Object = "chat.completion.chunk",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), // Use current time
            Model = originalModelAlias,
            Choices = new List<StreamingChoice> { choice }
            // Usage data is typically aggregated at the end for Gemini, not per chunk
        };
    }

    private static string? MapFinishReason(string? geminiFinishReason)
    {
        return geminiFinishReason switch
        {
            "STOP" => "stop", // Normal completion
            "MAX_TOKENS" => "length",
            "SAFETY" => "content_filter", // Map safety stop to content_filter
            "RECITATION" => "content_filter", // Map recitation stop to content_filter
            "OTHER" => null, // Unknown reason
            _ => geminiFinishReason // Pass through null or unknown values
        };
    }

    // Helper to read error content safely
    private static async Task<string> ReadErrorContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return $"Failed to read error content: {ex.Message}";
        }
    }

     /// <inheritdoc />
    public async Task<List<string>> ListModelsAsync(string? apiKey = null, CancellationToken cancellationToken = default)
    {
        // Determine the API key to use
        string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : _credentials.ApiKey!;
        if (string.IsNullOrWhiteSpace(effectiveApiKey))
        {
            throw new ConfigurationException($"API key is missing for provider '{_credentials.ProviderName}' and no override was provided.");
        }

        // Construct endpoint URL with the effective API key
        string endpoint = $"{_apiBaseUri}{_apiVersion}/models?key={effectiveApiKey}";
        _logger.LogDebug("Sending request to list Gemini models from: {Endpoint}", endpoint);

        try
        {
            // Gemini uses GET for listing models
            var response = await _httpClient.GetAsync(endpoint, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await ReadErrorContentAsync(response, cancellationToken).ConfigureAwait(false);
                string errorMessage = $"Gemini API list models request failed with status code {response.StatusCode}.";
                 try
                {
                    var errorResponse = JsonSerializer.Deserialize<GeminiErrorResponse>(errorContent);
                    if (errorResponse?.Error != null)
                    {
                        errorMessage = $"Gemini API Error {errorResponse.Error.Code} ({errorResponse.Error.Status}): {errorResponse.Error.Message}";
                        _logger.LogError("Gemini API list models request failed. Status: {StatusCode}, Error Status: {ErrorStatus}, Message: {ErrorMessage}", response.StatusCode, errorResponse.Error.Status, errorResponse.Error.Message);
                    } else { throw new JsonException("Failed to parse Gemini error response."); }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Gemini API list models request failed with status code {StatusCode}. Failed to parse error response body. Response: {ErrorContent}", response.StatusCode, errorContent);
                    errorMessage += $" Failed to parse error response: {errorContent}";
                }
                throw new LLMCommunicationException(errorMessage);
            }

            var modelListResponse = await response.Content.ReadFromJsonAsync<GeminiModelListResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);

            if (modelListResponse == null || modelListResponse.Models == null)
            {
                 _logger.LogError("Failed to deserialize the successful model list response from Gemini API.");
                 throw new LLMCommunicationException("Failed to deserialize the model list response from Gemini API.");
            }

            // Extract just the model IDs (using the Id property which parses "models/...")
            // Also filter for models that support 'generateContent' as we are focused on chat
            var modelIds = modelListResponse.Models
                                            .Where(m => m.SupportedGenerationMethods?.Contains("generateContent") ?? false)
                                            .Select(m => m.Id)
                                            .ToList();

            _logger.LogInformation("Successfully retrieved {ModelCount} chat-compatible models from Gemini.", modelIds.Count);
            return modelIds;
        }
        catch (JsonException ex)
        {
             _logger.LogError(ex, "JSON deserialization error processing Gemini model list response.");
             throw new LLMCommunicationException("Error deserializing Gemini model list response.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request error communicating with Gemini API for model list.");
            throw new LLMCommunicationException($"HTTP request error communicating with Gemini API for model list: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning(ex, "Gemini API list models request timed out.");
            throw new LLMCommunicationException("Gemini API list models request timed out.", ex);
        }
        catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
             _logger.LogInformation(ex, "Gemini API list models request was canceled.");
             throw; // Re-throw cancellation
        }
        catch (Exception ex) // Catch-all
        {
            _logger.LogError(ex, "An unexpected error occurred while listing Gemini models.");
            throw new LLMCommunicationException($"An unexpected error occurred while listing models: {ex.Message}", ex);
        }
    }

    public Task<EmbeddingResponse> CreateEmbeddingAsync(EmbeddingRequest request, string? apiKey = null, CancellationToken cancellationToken = default)
        => Task.FromException<EmbeddingResponse>(new NotSupportedException("Embeddings are not supported by GeminiClient."));

    public Task<ImageGenerationResponse> CreateImageAsync(ImageGenerationRequest request, string? apiKey = null, CancellationToken cancellationToken = default)
        => Task.FromException<ImageGenerationResponse>(new NotSupportedException("Image generation is not supported by GeminiClient."));
}

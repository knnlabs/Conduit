using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.InternalModels;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers;

/// <summary>
/// Client for interacting with AWS Bedrock API.
/// </summary>
public class BedrockClient : ILLMClient
{
    private readonly HttpClient _httpClient;
    private readonly string _providerModelId;
    private readonly ILogger<BedrockClient> _logger;

    // AWS Bedrock requires AWS Signature V4
    // In a real implementation, we'd use the AWS SDK for .NET
    // This is a simplified version for demonstration purposes
    
    public BedrockClient(
        string providerModelId,
        ILogger<BedrockClient> logger,
        HttpClient? httpClient = null)
    {
        _providerModelId = providerModelId ?? throw new ArgumentNullException(nameof(providerModelId));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");
    }

    /// <inheritdoc />
    public async Task<ChatCompletionResponse> CreateChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        _logger.LogInformation("Creating chat completion with AWS Bedrock for model {Model}", _providerModelId);
        
        try
        {
            // Determine the model provider and format request accordingly
            if (_providerModelId.Contains("claude", StringComparison.OrdinalIgnoreCase))
            {
                return await CreateAnthropicClaudeChatCompletionAsync(request, cancellationToken);
            }
            // Add other model providers as needed (Cohere, AI21, etc.)
            else
            {
                throw new UnsupportedProviderException($"Unsupported Bedrock model: {_providerModelId}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request error communicating with AWS Bedrock API");
            throw new LLMCommunicationException($"HTTP request error communicating with AWS Bedrock API: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization error processing AWS Bedrock response");
            throw new LLMCommunicationException("Error deserializing AWS Bedrock response", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning(ex, "AWS Bedrock API request timed out");
            throw new LLMCommunicationException("AWS Bedrock API request timed out", ex);
        }
        catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(ex, "AWS Bedrock API request was canceled");
            throw; // Re-throw cancellation
        }
        catch (Exception ex) when (ex is not UnsupportedProviderException 
                                  && ex is not ConfigurationException 
                                  && ex is not LLMCommunicationException)
        {
            _logger.LogError(ex, "An unexpected error occurred while processing AWS Bedrock chat completion");
            throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
        }
    }

    private async Task<ChatCompletionResponse> CreateAnthropicClaudeChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        // Map to Bedrock Claude format
        var claudeRequest = new BedrockClaudeChatRequest
        {
            MaxTokens = request.MaxTokens,
            Temperature = (float?)request.Temperature,
            TopP = request.TopP.HasValue ? (float?)request.TopP.Value : null,
            Messages = new List<BedrockClaudeMessage>()
        };
        
        // Extract system message if present
        var systemMessage = request.Messages.FirstOrDefault(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));
        if (systemMessage != null)
        {
            claudeRequest.System = systemMessage.Content;
        }
        
        // Map user and assistant messages
        foreach (var message in request.Messages.Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)))
        {
            claudeRequest.Messages.Add(new BedrockClaudeMessage
            {
                Role = message.Role.ToLowerInvariant() switch
                {
                    "user" => "user",
                    "assistant" => "assistant",
                    _ => message.Role // Keep as-is for other roles
                },
                Content = new List<BedrockClaudeContent>
                {
                    new BedrockClaudeContent { Type = "text", Text = message.Content }
                }
            });
        }

        // In a real implementation, we would use AWS SDK for .NET with AWS signature V4
        // This is a simplified version for demonstration purposes
        string modelId = GetBedrockModelId(_providerModelId);
        string apiUrl = $"https://api.bedrock.amazonaws.com/model/{modelId}/invoke";
        
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        requestMessage.Content = JsonContent.Create(claudeRequest, options: new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        
        // Add AWS signature headers
        AddAwsAuthenticationHeaders(requestMessage, JsonSerializer.Serialize(claudeRequest));
        
        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("AWS Bedrock API request failed with status code {StatusCode}. Response: {ErrorContent}", 
                response.StatusCode, errorContent);
            throw new LLMCommunicationException(
                $"AWS Bedrock API request failed with status code {response.StatusCode}. Response: {errorContent}");
        }
        
        var bedrockResponse = await response.Content.ReadFromJsonAsync<BedrockClaudeChatResponse>(
            cancellationToken: cancellationToken);
        
        if (bedrockResponse == null || bedrockResponse.Content == null || !bedrockResponse.Content.Any())
        {
            _logger.LogError("Failed to deserialize the response from AWS Bedrock API or response content is empty");
            throw new LLMCommunicationException("Failed to deserialize the response from AWS Bedrock API or response content is empty");
        }
        
        // Map to core response format
        return new ChatCompletionResponse
        {
            Id = bedrockResponse.Id ?? Guid.NewGuid().ToString(),
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = request.Model, // Return the requested model alias
            Choices = new List<Choice>
            {
                new Choice
                {
                    Index = 0,
                    Message = new Message
                    {
                        Role = bedrockResponse.Role ?? "assistant",
                        Content = bedrockResponse.Content.FirstOrDefault()?.Text ?? string.Empty
                    },
                    FinishReason = MapBedrockStopReason(bedrockResponse.StopReason)
                }
            },
            Usage = new Usage
            {
                PromptTokens = bedrockResponse.Usage?.InputTokens ?? 0,
                CompletionTokens = bedrockResponse.Usage?.OutputTokens ?? 0,
                TotalTokens = (bedrockResponse.Usage?.InputTokens ?? 0) + (bedrockResponse.Usage?.OutputTokens ?? 0)
            }
        };
    }

    private string MapBedrockStopReason(string? stopReason)
    {
        return stopReason?.ToLowerInvariant() switch
        {
            "stop_sequence" => "stop",
            "max_tokens" => "length",
            _ => stopReason ?? "unknown"
        };
    }

    /// <inheritdoc />
    public virtual async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _logger.LogInformation("Streaming chat completion with AWS Bedrock for model {Model}", _providerModelId);

        var config = new AmazonBedrockRuntimeConfig
        {
            RegionEndpoint = Amazon.RegionEndpoint.USEast1
        };
        using var client = new AmazonBedrockRuntimeClient(config);

        var bedrockRequest = new BedrockClaudeChatRequest
        {
            MaxTokens = request.MaxTokens,
            Temperature = (float?)request.Temperature,
            TopP = request.TopP.HasValue ? (float?)request.TopP.Value : null,
            Messages = request.Messages.Select(m => new BedrockClaudeMessage
            {
                Role = m.Role,
                Content = new List<BedrockClaudeContent> { new BedrockClaudeContent { Text = m.Content } }
            }).ToList()
        };
        var requestBody = JsonSerializer.Serialize(bedrockRequest);
        var invokeRequest = new InvokeModelWithResponseStreamRequest
        {
            ModelId = _providerModelId,
            Body = new MemoryStream(Encoding.UTF8.GetBytes(requestBody)),
            ContentType = "application/json",
            Accept = "application/json"
        };

        var response = await client.InvokeModelWithResponseStreamAsync(invokeRequest, cancellationToken);
        foreach (var ev in response.Body)
        {
            if (ev is Amazon.BedrockRuntime.Model.PayloadPart payloadPart)
            {
                ChatCompletionChunk? chunk = null;
                var json = Encoding.UTF8.GetString(payloadPart.Bytes.ToArray());
                try
                {
                    var chunkObj = JsonSerializer.Deserialize<BedrockStreamingResponse>(json);
                    if (chunkObj != null && !string.IsNullOrEmpty(chunkObj.Completion))
                    {
                        chunk = new ChatCompletionChunk
                        {
                            Id = Guid.NewGuid().ToString(),
                            Object = "chat.completion.chunk",
                            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            Model = request.Model,
                            Choices = new List<StreamingChoice>
                            {
                                new StreamingChoice
                                {
                                    Index = 0,
                                    Delta = new DeltaContent
                                    {
                                        Content = chunkObj.Completion
                                    },
                                    FinishReason = chunkObj.StopReason
                                }
                            }
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse Bedrock streaming chunk: {Json}", json);
                }
                if (chunk != null)
                {
                    yield return chunk;
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task<List<string>> ListModelsAsync(string? apiKey = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing available models from AWS Bedrock");
        
        // In a real implementation, we would use AWS SDK to list available models
        // For now, return a static list of commonly available models
        
        await Task.Delay(1, cancellationToken); // Adding await to make this truly async
        
        return new List<string>
        {
            "anthropic.claude-3-opus-20240229-v1:0",
            "anthropic.claude-3-sonnet-20240229-v1:0",
            "anthropic.claude-3-haiku-20240307-v1:0",
            "anthropic.claude-v2",
            "anthropic.claude-instant-v1",
            "amazon.titan-text-express-v1",
            "amazon.titan-text-lite-v1",
            "amazon.titan-embed-text-v1",
            "cohere.command-text-v14",
            "cohere.command-light-text-v14",
            "cohere.embed-english-v3",
            "cohere.embed-multilingual-v3",
            "meta.llama2-13b-chat-v1",
            "meta.llama2-70b-chat-v1",
            "meta.llama3-8b-instruct-v1:0",
            "meta.llama3-70b-instruct-v1:0",
            "ai21.j2-mid-v1",
            "ai21.j2-ultra-v1",
            "stability.stable-diffusion-xl-v1"
        };
    }
    
    #region Helper Methods
    
    private string GetBedrockModelId(string modelAlias)
    {
        // Map internal model aliases to actual AWS Bedrock model IDs if needed
        // For now, we assume the model alias is the actual model ID
        return modelAlias;
    }
    
    private void AddAwsAuthenticationHeaders(HttpRequestMessage request, string requestBody)
    {
        // In a real implementation, this would add AWS Signature V4 authentication headers
        // For simplicity, we're not implementing the full AWS authentication here
        
        // Placeholder for AWS signature implementation
        // In production, use AWS SDK for .NET or implement AWS Signature V4
        
        // For demo purpose, we'll just add placeholder headers
        request.Headers.Add("X-Amz-Date", DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ"));
        request.Headers.Add("Authorization", "AWS4-HMAC-SHA256 Credential=PLACEHOLDER");
    }
    
    #endregion
}

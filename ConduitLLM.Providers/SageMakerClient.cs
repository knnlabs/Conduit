using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.InternalModels;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers;

/// <summary>
/// Client for interacting with AWS SageMaker endpoints.
/// </summary>
public class SageMakerClient : ILLMClient
{
    private readonly HttpClient _httpClient;
    private readonly ProviderCredentials _credentials;
    private readonly string _endpointName;
    private readonly ILogger<SageMakerClient> _logger;

    // In production, we would use the AWS SDK for .NET
    // This is a simplified version for demonstration
    
    public SageMakerClient(
        ProviderCredentials credentials,
        string endpointName,
        ILogger<SageMakerClient> logger,
        HttpClient? httpClient = null)
    {
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _endpointName = endpointName ?? throw new ArgumentNullException(nameof(endpointName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        if (string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            throw new ConfigurationException("AWS Access Key ID (ApiKey) is missing for AWS SageMaker provider.");
        }
        
        // ApiSecret doesn't exist in ProviderCredentials, so we'll have to use another approach
        // For AWS credentials, we'll assume they're provided through environment variables or AWS credentials file
        
        if (string.IsNullOrWhiteSpace(credentials.ApiBase))
        {
            throw new ConfigurationException("AWS Region (ApiBase) is missing for AWS SageMaker provider.");
        }
        
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
        
        _logger.LogInformation("Creating chat completion with AWS SageMaker endpoint {Endpoint}", _endpointName);
        
        try
        {
            // Convert the core chat request to SageMaker format
            var sageMakerRequest = new SageMakerChatRequest
            {
                Inputs = request.Messages.Select(m => new SageMakerChatMessage
                {
                    Role = MapCoreRoleToSageMakerRole(m.Role),
                    Content = m.Content ?? string.Empty
                }).ToList(),
                Parameters = new SageMakerParameters
                {
                    MaxNewTokens = request.MaxTokens,
                    Temperature = request.Temperature,
                    TopP = request.TopP,
                    DoSample = true,
                    ReturnFullText = false
                }
            };
            
            // In a real implementation, use AWS SDK with AWS Signature v4
            string apiUrl = $"{GetSageMakerRuntimeEndpoint(_credentials.ApiBase ?? "us-east-1")}/endpoints/{_endpointName}/invocations";
            
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            requestMessage.Content = JsonContent.Create(sageMakerRequest, options: new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            
            // Add AWS signature headers
            AddAwsAuthenticationHeaders(requestMessage, JsonSerializer.Serialize(sageMakerRequest));
            
            using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("AWS SageMaker API request failed with status code {StatusCode}. Response: {ErrorContent}",
                    response.StatusCode, errorContent);
                throw new LLMCommunicationException(
                    $"AWS SageMaker API request failed with status code {response.StatusCode}. Response: {errorContent}");
            }
            
            var sageMakerResponse = await response.Content.ReadFromJsonAsync<SageMakerChatResponse>(
                cancellationToken: cancellationToken);
            
            if (sageMakerResponse?.GeneratedOutputs == null || !sageMakerResponse.GeneratedOutputs.Any())
            {
                _logger.LogError("Failed to deserialize the response from AWS SageMaker or response is empty");
                throw new LLMCommunicationException("Failed to deserialize the response from AWS SageMaker or response is empty");
            }
            
            // Map to core response format
            return new ChatCompletionResponse
            {
                Id = Guid.NewGuid().ToString(),
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
                            Role = sageMakerResponse.GeneratedOutputs.FirstOrDefault()?.Role ?? "assistant",
                            Content = sageMakerResponse.GeneratedOutputs.FirstOrDefault()?.Content ?? string.Empty
                        },
                        FinishReason = "stop" // SageMaker doesn't provide finish reason in this format
                    }
                },
                Usage = new Usage
                {
                    // SageMaker doesn't provide token usage, so estimate based on text length
                    // This is a very rough approximation
                    PromptTokens = EstimateTokenCount(string.Join(" ", request.Messages.Select(m => m.Content))),
                    CompletionTokens = EstimateTokenCount(sageMakerResponse.GeneratedOutputs.FirstOrDefault()?.Content ?? string.Empty),
                    TotalTokens = 0 // Will be calculated below
                }
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request error communicating with AWS SageMaker API");
            throw new LLMCommunicationException($"HTTP request error communicating with AWS SageMaker API: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization error processing AWS SageMaker response");
            throw new LLMCommunicationException("Error deserializing AWS SageMaker response", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning(ex, "AWS SageMaker API request timed out");
            throw new LLMCommunicationException("AWS SageMaker API request timed out", ex);
        }
        catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(ex, "AWS SageMaker API request was canceled");
            throw; // Re-throw cancellation
        }
        catch (Exception ex) when (ex is not UnsupportedProviderException
                                  && ex is not ConfigurationException
                                  && ex is not LLMCommunicationException)
        {
            _logger.LogError(ex, "An unexpected error occurred while processing AWS SageMaker chat completion");
            throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        _logger.LogInformation("Streaming chat completion with AWS SageMaker is not natively supported. Using non-streaming endpoint with simulated streaming");
        
        // SageMaker doesn't natively support streaming, so we'll simulate it
        // Get the full response first
        var fullResponse = await CreateChatCompletionAsync(request, apiKey, cancellationToken);
        
        if (fullResponse.Choices == null || !fullResponse.Choices.Any() || 
            string.IsNullOrEmpty(fullResponse.Choices[0].Message?.Content))
        {
            yield break;
        }
        
        // Simulate streaming by breaking up the content
        string content = fullResponse.Choices[0].Message!.Content!;
        
        // Generate a random ID for this streaming session
        string streamId = Guid.NewGuid().ToString();
        
        // Initial chunk with role
        yield return new ChatCompletionChunk
        {
            Id = streamId,
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
                    Model = request.Model,
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
            Model = request.Model,
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
    public async Task<List<string>> ListModelsAsync(string? apiKey = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing SageMaker endpoints is not directly supported through this interface. Returning endpoint name.");
        
        // In a production implementation, we would use the AWS SDK to list endpoints
        // For this sample, we'll just return the configured endpoint
        await Task.Delay(1, cancellationToken); // Adding await to make this truly async
        
        return new List<string> { _endpointName };
    }
    
    #region Helper Methods
    
    private string MapCoreRoleToSageMakerRole(string? coreRole)
    {
        return coreRole?.ToLowerInvariant() switch
        {
            "user" => "user",
            "assistant" => "assistant",
            "system" => "system",
            _ => coreRole ?? "user" // Default to user for unknown roles
        };
    }
    
    private int EstimateTokenCount(string text)
    {
        // Very rough token count estimation
        // In a real implementation, use a proper tokenizer
        if (string.IsNullOrEmpty(text))
            return 0;
            
        // Approximately 4 characters per token for English text
        return text.Length / 4;
    }
    
    private string GetSageMakerRuntimeEndpoint(string region)
    {
        // Ensure region is not null
        if (string.IsNullOrWhiteSpace(region))
        {
            throw new ArgumentNullException(nameof(region), "AWS region cannot be null or empty");
        }
        
        // Format the SageMaker runtime endpoint URL
        return $"https://runtime.sagemaker.{region}.amazonaws.com";
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

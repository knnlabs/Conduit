using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers;
using ConduitLLM.Providers.InternalModels;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using TestIt = ConduitLLM.Tests.TestHelpers.ItExpr;
using MoqIt = Moq.Protected.ItExpr;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ConduitLLM.Tests;

public class VertexAIClientTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<VertexAIClient>> _loggerMock;
    private readonly ProviderCredentials _credentials;

    public VertexAIClientTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_handlerMock.Object);
        _loggerMock = new Mock<ILogger<VertexAIClient>>();

        _credentials = new ProviderCredentials
        {
            ProviderName = "GoogleVertexAI",
            ApiKey = "google-api-key-test",
            ApiBase = "us-central1",
            ApiVersion = "your-project-id" // Using ApiVersion as a placeholder for project ID
        };
    }

    // Helper to create a standard ChatCompletionRequest
    private ChatCompletionRequest CreateTestRequest(string modelAlias = "test-alias")
    {
        return new ChatCompletionRequest
        {
            Model = modelAlias,
            Messages = new List<Message>
            {
                new Message { Role = "system", Content = "You are a helpful AI assistant." },
                new Message { Role = "user", Content = "Hello, Google Vertex AI!" }
            },
            Temperature = 0.7,
            MaxTokens = 100
        };
    }

    // Helper to create a standard successful Gemini response
    private VertexAIPredictionResponse CreateSuccessGeminiResponse()
    {
        return new VertexAIPredictionResponse
        {
            Predictions = new List<VertexAIPrediction>
            {
                new VertexAIPrediction
                {
                    Candidates = new List<VertexAIGeminiCandidate>
                    {
                        new VertexAIGeminiCandidate
                        {
                            Content = new VertexAIGeminiContent
                            {
                                Role = "model",
                                Parts = new List<VertexAIGeminiPart>
                                {
                                    new VertexAIGeminiPart
                                    {
                                        Text = "Hello! I'm Gemini. How can I assist you today?"
                                    }
                                }
                            },
                            FinishReason = "STOP"
                        }
                    }
                }
            }
        };
    }

    // Helper to create a standard successful PaLM response
    private VertexAIPredictionResponse CreateSuccessPaLMResponse()
    {
        return new VertexAIPredictionResponse
        {
            Predictions = new List<VertexAIPrediction>
            {
                new VertexAIPrediction
                {
                    Content = "Hello! I'm PaLM. How can I assist you today?"
                }
            }
        };
    }

    [Fact]
    public async Task CreateChatCompletionAsync_GeminiSuccess()
    {
        // Arrange
        var request = CreateTestRequest("vertex-gemini");
        var modelId = "gemini-1.5-pro";
        var expectedResponse = CreateSuccessGeminiResponse();
        
        // The URL that would be called by the VertexAIClient for Gemini
        var expectedUri = $"https://us-central1-aiplatform.googleapis.com/v1/projects/your-project-id/locations/us-central1/publishers/google/models/{modelId}:predict?key=google-api-key-test";
        
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                MoqIt.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post), 
                MoqIt.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(expectedResponse)
            })
            .Verifiable();

        var client = new VertexAIClient(_credentials, modelId, _loggerMock.Object, _httpClient);

        // Act
        var response = await client.CreateChatCompletionAsync(request, cancellationToken: CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(request.Model, response.Model); // Should return original alias
        Assert.Equal("assistant", response.Choices[0].Message.Role);
        Assert.Contains("Hello! I'm Gemini", response.Choices[0].Message.Content);
    }

    [Fact]
    public async Task CreateChatCompletionAsync_PaLMSuccess()
    {
        // Arrange
        var request = CreateTestRequest("vertex-palm");
        var modelId = "text-bison@002";
        var expectedResponse = CreateSuccessPaLMResponse();
        
        // The URL that would be called by the VertexAIClient for PaLM
        var expectedUri = $"https://us-central1-aiplatform.googleapis.com/v1/projects/your-project-id/locations/us-central1/publishers/google/models/{modelId}:predict?key=google-api-key-test";
        
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                MoqIt.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post), 
                MoqIt.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(expectedResponse)
            })
            .Verifiable();

        var client = new VertexAIClient(_credentials, modelId, _loggerMock.Object, _httpClient);

        // Act
        var response = await client.CreateChatCompletionAsync(request, cancellationToken: CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(request.Model, response.Model); // Should return original alias
        Assert.Equal("assistant", response.Choices[0].Message.Role);
        Assert.Contains("Hello! I'm PaLM", response.Choices[0].Message.Content);
    }

    [Fact]
    public async Task CreateChatCompletionAsync_ApiReturnsError_ThrowsLLMCommunicationException()
    {
        // Arrange
        var request = CreateTestRequest("vertex-gemini");
        var modelId = "gemini-1.5-pro";
        
        var errorResponse = new { error = new { message = "The model is currently overloaded", code = 429 } };
        
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                MoqIt.IsAny<HttpRequestMessage>(), 
                MoqIt.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.TooManyRequests,
                Content = JsonContent.Create(new { error = new { message = errorResponse.error.message, code = errorResponse.error.code } })
            });

        var client = new VertexAIClient(_credentials, modelId, _loggerMock.Object, _httpClient);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LLMCommunicationException>(
            () => client.CreateChatCompletionAsync(request, cancellationToken: CancellationToken.None));
        
        Assert.Contains("429", exception.Message);
    }

    [Fact]
    public async Task CreateChatCompletionAsync_HttpRequestException_ThrowsLLMCommunicationException()
    {
        // Arrange
        var request = CreateTestRequest("vertex-gemini");
        var modelId = "gemini-1.5-pro";
        
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                MoqIt.IsAny<HttpRequestMessage>(), 
                MoqIt.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var client = new VertexAIClient(_credentials, modelId, _loggerMock.Object, _httpClient);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LLMCommunicationException>(
            () => client.CreateChatCompletionAsync(request, cancellationToken: CancellationToken.None));
        
        Assert.Contains("Connection refused", exception.Message);
    }

    [Fact]
    public async Task StreamChatCompletionAsync_ReturnsChunks()
    {
        // Arrange
        var request = CreateTestRequest("vertex-gemini");
        var modelId = "gemini-1.5-pro";
        var expectedResponse = CreateSuccessGeminiResponse();
        
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                MoqIt.IsAny<HttpRequestMessage>(), 
                MoqIt.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(expectedResponse)
            });

        var client = new VertexAIClient(_credentials, modelId, _loggerMock.Object, _httpClient);

        // Act
        int chunkCount = 0;
        await foreach (var chunk in client.StreamChatCompletionAsync(request, cancellationToken: CancellationToken.None))
        {
            // Assert
            Assert.NotNull(chunk);
            Assert.Equal("chat.completion.chunk", chunk.Object);
            Assert.NotEmpty(chunk.Choices);
            chunkCount++;
            
            // We only check a few chunks to keep the test reasonable
            if (chunkCount > 2)
                break;
        }

        // Assert
        Assert.True(chunkCount > 0);
    }

    [Fact]
    public async Task ListModelsAsync_ReturnsModels()
    {
        // Arrange
        var modelId = "gemini-1.5-pro";
        var client = new VertexAIClient(_credentials, modelId, _loggerMock.Object, _httpClient);

        // Act
        var models = await client.ListModelsAsync();

        // Assert
        Assert.NotNull(models);
        Assert.NotEmpty(models);
        Assert.Contains(models, m => m.Contains("gemini"));
    }
}

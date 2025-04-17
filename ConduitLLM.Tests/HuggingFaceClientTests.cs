using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers;
using ConduitLLM.Providers.InternalModels;

using Microsoft.Extensions.Logging;

using Moq;
using Moq.Protected;

using Xunit;

using MoqIt = Moq.Protected.ItExpr;

namespace ConduitLLM.Tests;

public class HuggingFaceClientTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<HuggingFaceClient>> _loggerMock;
    private readonly ProviderCredentials _credentials;

    public HuggingFaceClientTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_handlerMock.Object);
        _loggerMock = new Mock<ILogger<HuggingFaceClient>>();

        _credentials = new ProviderCredentials
        {
            ProviderName = "HuggingFace",
            ApiKey = "hf_test_api_key",
            ApiBase = "https://api-inference.huggingface.co/models"
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
                new Message { Role = "user", Content = "Hello, HuggingFace!" }
            },
            Temperature = 0.7,
            MaxTokens = 100
        };
    }

    // Helper to create a standard successful HuggingFace response
    private HuggingFaceTextGenerationResponse CreateSuccessHuggingFaceResponse(string modelId)
    {
        return new HuggingFaceTextGenerationResponse
        {
            GeneratedText = $"Hello! I'm a {modelId} Hugging Face model. How can I assist you today?"
        };
    }

    [Fact]
    public async Task CreateChatCompletionAsync_Success()
    {
        // Arrange
        var request = CreateTestRequest("huggingface-mistral");
        var modelId = "mistralai/Mistral-7B-Instruct-v0.2";
        var expectedResponse = CreateSuccessHuggingFaceResponse(modelId);
        
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(), 
                Moq.Protected.ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(expectedResponse)
            })
            .Verifiable();

        var client = new HuggingFaceClient(_credentials, modelId, _loggerMock.Object, _httpClient);

        // Act
        var response = await client.CreateChatCompletionAsync(request);
        
        // Assert with defensive null checking
        Assert.NotNull(response);
        Assert.NotNull(response.Choices);
        Assert.NotEmpty(response.Choices);
        
        var firstChoice = response.Choices[0];
        Assert.NotNull(firstChoice);
        
        var message = firstChoice?.Message;
        Assert.NotNull(message);
        Assert.Equal("assistant", message?.Role);
        Assert.Contains(modelId, message?.Content, StringComparison.OrdinalIgnoreCase);
        
        _handlerMock.Protected().Verify(
            "SendAsync",
            Times.AtLeastOnce(),
            Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
            Moq.Protected.ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task CreateChatCompletionAsync_ArrayResponse_Success()
    {
        // Arrange
        var request = CreateTestRequest("huggingface-mistral");
        var modelId = "mistralai/Mistral-7B-Instruct-v0.2";
        var expectedUri = $"https://api-inference.huggingface.co/models/{modelId}";
        
        var arrayResponse = new[]
        {
            new HuggingFaceTextGenerationResponse
            {
                GeneratedText = "Hello! I'm GPT-2. How can I assist you today?"
            }
        };
        
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(), 
                Moq.Protected.ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(arrayResponse)
            });

        var client = new HuggingFaceClient(_credentials, modelId, _loggerMock.Object, _httpClient);

        // Act
        var response = await client.CreateChatCompletionAsync(request);

        // Assert with defensive null checking
        Assert.NotNull(response);
        Assert.NotNull(response.Choices);
        Assert.NotEmpty(response.Choices);
        
        var firstChoice = response.Choices[0];
        Assert.NotNull(firstChoice);
        
        var message = firstChoice?.Message;
        Assert.NotNull(message);
        Assert.Equal("assistant", message?.Role);
        Assert.Contains("GPT-2", message?.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateChatCompletionAsync_PlainTextResponse_Success()
    {
        // Arrange
        var request = CreateTestRequest("huggingface-mistral");
        var modelId = "mistralai/Mistral-7B-Instruct-v0.2";
        var expectedUri = $"https://api-inference.huggingface.co/models/{modelId}";
        
        var plainTextResponse = "Hello! I'm Llama. How can I assist you today?";
        
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(), 
                Moq.Protected.ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(plainTextResponse)
            });

        var client = new HuggingFaceClient(_credentials, modelId, _loggerMock.Object, _httpClient);

        // Act
        var response = await client.CreateChatCompletionAsync(request);

        // Assert with defensive null checking
        Assert.NotNull(response);
        Assert.NotNull(response.Choices);
        Assert.NotEmpty(response.Choices);
        
        var firstChoice = response.Choices[0];
        Assert.NotNull(firstChoice);
        
        var message = firstChoice?.Message;
        Assert.NotNull(message);
        Assert.Equal("assistant", message?.Role);
        Assert.Contains("Llama", message?.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateChatCompletionAsync_ApiReturnsError_ThrowsLLMCommunicationException()
    {
        // Arrange
        var request = CreateTestRequest("huggingface-mistral");
        var modelId = "mistralai/Mistral-7B-Instruct-v0.2";
        
        var errorResponse = new { error = "Model is loading", estimated_time = 20.0 };
        
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                MoqIt.Is<HttpRequestMessage>(req => req != null), 
                MoqIt.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable,
                Content = JsonContent.Create(errorResponse)
            });

        var client = new HuggingFaceClient(_credentials, modelId, _loggerMock.Object, _httpClient);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LLMCommunicationException>(
            () => client.CreateChatCompletionAsync(request));
        
        // Check for a more generic error message instead of specific status code
        Assert.Contains("error", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("model is loading", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StreamChatCompletionAsync_ReturnsChunks()
    {
        // Arrange
        var request = CreateTestRequest("huggingface-mistral");
        var modelId = "mistralai/Mistral-7B-Instruct-v0.2";
        var expectedResponse = CreateSuccessHuggingFaceResponse(modelId);
        
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                MoqIt.IsAny<HttpRequestMessage>(), 
                MoqIt.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(expectedResponse)
            });

        var client = new HuggingFaceClient(_credentials, modelId, _loggerMock.Object, _httpClient);

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
        var modelId = "mistralai/Mistral-7B-Instruct-v0.2";
        var client = new HuggingFaceClient(_credentials, modelId, _loggerMock.Object, _httpClient);

        // Act
        var models = await client.ListModelsAsync();

        // Assert
        Assert.NotNull(models);
        Assert.NotEmpty(models);
        Assert.Contains(models, m => m.Contains("mistral"));
    }
}

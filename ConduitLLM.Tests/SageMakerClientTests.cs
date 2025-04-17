using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers;
using ConduitLLM.Providers.InternalModels;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
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

public class SageMakerClientTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<SageMakerClient>> _loggerMock;
    private readonly ProviderCredentials _credentials;
    private readonly string _endpointName;

    public SageMakerClientTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_handlerMock.Object);
        _loggerMock = new Mock<ILogger<SageMakerClient>>();
        _endpointName = "my-llama2-endpoint";

        _credentials = new ProviderCredentials
        {
            ProviderName = "AWSSageMaker",
            ApiKey = "aws-access-key-test",
            ApiBase = "us-east-1"
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
                new Message { Role = "user", Content = "Hello, AWS SageMaker!" }
            },
            Temperature = 0.7,
            MaxTokens = 100
        };
    }

    // Helper to create a standard successful SageMaker response
    private SageMakerChatResponse CreateSuccessSageMakerResponse()
    {
        return new SageMakerChatResponse
        {
            GeneratedOutputs = new List<SageMakerChatOutput>
            {
                new SageMakerChatOutput
                {
                    Role = "assistant",
                    Content = "Hello! I'm a model deployed on SageMaker. How can I help you today?"
                }
            }
        };
    }

    [Fact]
    public async Task CreateChatCompletionAsync_Success()
    {
        // Arrange
        var request = CreateTestRequest("sagemaker-llama2");
        var expectedResponse = CreateSuccessSageMakerResponse();
        
        // The URL that would be called by the SageMakerClient
        var expectedUri = $"https://runtime.sagemaker.us-east-1.amazonaws.com/endpoints/{_endpointName}/invocations";
        
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                Moq.Protected.ItExpr.Is<HttpRequestMessage>(req => 
                    req != null &&
                    req.Method == HttpMethod.Post && 
                    req.RequestUri != null &&
                    req.RequestUri.ToString() == expectedUri), 
                Moq.Protected.ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(expectedResponse)
            })
            .Verifiable();

        var client = new SageMakerClient(_credentials, _endpointName, _loggerMock.Object, _httpClient);

        // Act
        var response = await client.CreateChatCompletionAsync(request);
        
        // Assert with defensive null checking
        Assert.NotNull(response);
        Assert.NotNull(response.Choices);
        Assert.True(response.Choices != null && response.Choices.Count > 0, "Response choices should not be empty");
        var firstChoice = response.Choices != null && response.Choices.Count > 0 ? response.Choices[0] : null;
        Assert.NotNull(firstChoice);
        var message = firstChoice?.Message;
        Assert.NotNull(message);
        if (message != null)
        {
            var msg = message;
            Assert.Equal("assistant", msg.Role);
            Assert.Equal(expectedResponse.GeneratedOutputs[0].Content, msg.Content);
        }
        Assert.Equal(request.Model, response.Model); // Should return original alias
        Assert.NotNull(response.Usage);
        _handlerMock!.Protected().Verify(
            "SendAsync",
            Times.Once(),
            Moq.Protected.ItExpr.Is<HttpRequestMessage>(req => 
                req != null &&
                req.Method == HttpMethod.Post && 
                req.RequestUri != null &&
                req.RequestUri.ToString() == expectedUri),
            Moq.Protected.ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task CreateChatCompletionAsync_ApiReturnsError_ThrowsLLMCommunicationException()
    {
        // Arrange
        var request = CreateTestRequest("sagemaker-llama2");
        var expectedUri = $"https://runtime.sagemaker.us-east-1.amazonaws.com/endpoints/{_endpointName}/invocations";
        
        var errorResponse = new { error = "Model error", message = "Failed to process the request" };
        
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                Moq.Protected.ItExpr.Is<HttpRequestMessage>(req => req != null), 
                Moq.Protected.ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = JsonContent.Create(errorResponse)
            });

        var client = new SageMakerClient(_credentials, _endpointName, _loggerMock.Object, _httpClient);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LLMCommunicationException>(
            () => client.CreateChatCompletionAsync(request));
        
        // Check for more generic error message components
        Assert.Contains("AWS SageMaker API request failed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("error", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StreamChatCompletionAsync_ReturnsChunks()
    {
        // Arrange
        var request = CreateTestRequest("sagemaker-llama2");
        var expectedResponse = CreateSuccessSageMakerResponse();
        
        var expectedUri = $"https://runtime.sagemaker.us-east-1.amazonaws.com/endpoints/{_endpointName}/invocations";
        
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(), 
                Moq.Protected.ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(expectedResponse)
            });

        var client = new SageMakerClient(_credentials, _endpointName, _loggerMock.Object, _httpClient);

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
    public async Task ListModelsAsync_ReturnsEndpointName()
    {
        // Arrange
        var client = new SageMakerClient(_credentials, _endpointName, _loggerMock.Object, _httpClient);

        // Act
        var models = await client.ListModelsAsync();

        // Assert
        Assert.NotNull(models);
        Assert.Single(models);
        Assert.Equal(_endpointName, models[0]);
    }
}

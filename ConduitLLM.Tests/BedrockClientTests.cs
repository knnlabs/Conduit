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

public class BedrockClientTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<BedrockClient>> _loggerMock;
    private readonly ProviderCredentials _credentials;

    public BedrockClientTests()
    {
        _credentials = new ProviderCredentials
        {
            ApiKey = "test-key",
            ApiBase = "us-east-1",
            ProviderName = "AWSBedrock"
        };
        
        _loggerMock = new Mock<ILogger<BedrockClient>>();
        _handlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/")
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
                new Message { Role = "user", Content = "Hello, AWS Bedrock!" }
            },
            Temperature = 0.7,
            MaxTokens = 100
        };
    }

    // Helper to create a standard successful Claude response DTO
    private BedrockClaudeChatResponse CreateSuccessClaudeResponse()
    {
        return new BedrockClaudeChatResponse
        {
            Id = "resp-12345",
            Role = "assistant",
            Content = new List<BedrockClaudeResponseContent>
            {
                new BedrockClaudeResponseContent
                {
                    Type = "text",
                    Text = "Hello! I'm Claude on AWS Bedrock. How can I assist you today?"
                }
            },
            Model = "anthropic.claude-3-sonnet-20240229-v1:0",
            StopReason = "stop_sequence",
            Usage = new BedrockClaudeUsage
            {
                InputTokens = 24,
                OutputTokens = 15
            }
        };
    }

    [Fact]
    public async Task CreateChatCompletionAsync_Success()
    {
        // Arrange
        var request = CreateTestRequest("bedrock-claude");
        var modelId = "anthropic.claude-3-sonnet-20240229-v1:0";
        var expectedResponse = CreateSuccessClaudeResponse();
        
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

        var client = new BedrockClient(_credentials, modelId, _loggerMock.Object, _httpClient);

        // Act
        var response = await client.CreateChatCompletionAsync(request);

        // Assert with defensive null checking
        Assert.NotNull(response);
        Assert.NotNull(response.Choices);
        Assert.True(response.Choices?.Count > 0, "Response choices should not be empty");
        
        var firstChoice = response.Choices?[0];
        Assert.NotNull(firstChoice);
        
        var message = firstChoice?.Message;
        Assert.NotNull(message);
        Assert.Equal("assistant", message?.Role);
        Assert.Equal(expectedResponse.Content[0].Text, message?.Content);
        Assert.NotNull(response.Usage);
        var usage = response.Usage;
        Assert.NotNull(usage);
        Assert.Equal(expectedResponse.Usage.InputTokens + expectedResponse.Usage.OutputTokens, usage.TotalTokens);
        
        Assert.NotNull(_handlerMock);
        var handlerMock = _handlerMock;
        Assert.NotNull(handlerMock);
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.AtLeastOnce(),
            Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
            Moq.Protected.ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task CreateChatCompletionAsync_ApiReturnsError_ThrowsLLMCommunicationException()
    {
        // Arrange
        var request = CreateTestRequest("bedrock-claude");
        var modelId = "anthropic.claude-3-sonnet-20240229-v1:0";
        
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                Moq.Protected.ItExpr.Is<HttpRequestMessage>(req => req != null), 
                Moq.Protected.ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable,
                Content = new StringContent("Service Unavailable")
            });

        var client = new BedrockClient(_credentials, modelId, _loggerMock.Object, _httpClient);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LLMCommunicationException>(
            () => client.CreateChatCompletionAsync(request));
        
        // Check for actual error message pattern that's returned
        Assert.Contains("AWS Bedrock API request failed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateChatCompletionAsync_HttpRequestException_ThrowsLLMCommunicationException()
    {
        // Arrange
        var request = CreateTestRequest("bedrock-claude");
        var modelId = "anthropic.claude-3-sonnet-20240229-v1:0";
        
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                Moq.Protected.ItExpr.Is<HttpRequestMessage>(req => req != null), 
                Moq.Protected.ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var client = new BedrockClient(_credentials, modelId, _loggerMock.Object, _httpClient);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LLMCommunicationException>(
            () => client.CreateChatCompletionAsync(request));
        
        // Check for actual error message pattern that's returned
        Assert.Contains("HTTP request error", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StreamChatCompletionAsync_ReturnsChunk()
    {
        // Arrange
        var request = CreateTestRequest("bedrock-claude");
        var modelId = "anthropic.claude-3-sonnet-20240229-v1:0";
        var client = new BedrockClient(_credentials, modelId, _loggerMock.Object, _httpClient);

        // Act
        int chunkCount = 0;
        await foreach (var chunk in client.StreamChatCompletionAsync(request, cancellationToken: CancellationToken.None))
        {
            // Assert
            Assert.NotNull(chunk);
            Assert.Equal("chat.completion.chunk", chunk.Object);
            Assert.NotNull(chunk.Choices);
            Assert.True(chunk.Choices?.Count > 0, "Chunk choices should not be empty");
            chunkCount++;
            
            // We only expect one chunk in our implementation
            if (chunkCount > 0)
                break;
        }

        // Assert
        Assert.Equal(1, chunkCount);
    }

    [Fact]
    public async Task ListModelsAsync_ReturnsModels()
    {
        // Arrange
        var modelId = "anthropic.claude-3-sonnet-20240229-v1:0";
        var client = new BedrockClient(_credentials, modelId, _loggerMock.Object, _httpClient);

        // Act
        var models = await client.ListModelsAsync();

        // Assert
        Assert.NotNull(models);
        Assert.NotEmpty(models);
        Assert.Contains(models, m => m.Contains("claude"));
    }
}

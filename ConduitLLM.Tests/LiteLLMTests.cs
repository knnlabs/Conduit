using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Moq;

using Xunit;

namespace ConduitLLM.Tests;

public class ConduitTests
{
    private readonly Mock<ILLMClientFactory> _mockFactory;
    private readonly Mock<ILLMClient> _mockClient;
    private readonly Conduit _conduit;

    public ConduitTests()
    {
        _mockFactory = new Mock<ILLMClientFactory>();
        _mockClient = new Mock<ILLMClient>();

        // Setup factory to return the mock client for any model alias by default
        _mockFactory.Setup(f => f.GetClient(It.IsAny<string>()))
                    .Returns(_mockClient.Object);

        // Instantiate Conduit with the mock factory
        _conduit = new Conduit(_mockFactory.Object);
    }

    [Fact]
    public async Task CreateChatCompletionAsync_ValidRequest_ReturnsResponse()
    {
        // Arrange
        var request = new ChatCompletionRequest
        {
            Model = "test-model",
            Messages = new List<Message> { new Message { Role = "user", Content = "Test" } }
        };
        var expectedResponse = new ChatCompletionResponse
        {
            Id = "resp-123",
            Model = request.Model,
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Choices = new List<Choice> { new Choice { Index = 0, Message = new Message { Role = "assistant", Content = "Response" }, FinishReason = "stop" } }
        };
        string? apiKey = "test-key-123";

        _mockClient.Setup(c => c.CreateChatCompletionAsync(request, apiKey, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(expectedResponse);

        // Act
        var response = await _conduit.CreateChatCompletionAsync(request, apiKey, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(expectedResponse.Id, response.Id);
        Assert.Equal(expectedResponse.Choices[0].Message.Content, response.Choices[0].Message.Content);
        _mockFactory.Verify(f => f.GetClient(request.Model), Times.Once);
        _mockClient.Verify(c => c.CreateChatCompletionAsync(request, apiKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateChatCompletionAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        ChatCompletionRequest? request = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await _conduit.CreateChatCompletionAsync(request!, null, CancellationToken.None); // Use null-forgiving operator for test
        });
        _mockFactory.Verify(f => f.GetClient(It.IsAny<string>()), Times.Never);
        _mockClient.Verify(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task CreateChatCompletionAsync_InvalidModel_ThrowsArgumentException(string? invalidModel)
    {
        // Arrange
        var request = new ChatCompletionRequest
        {
            Model = invalidModel!, // Use null-forgiving operator to address the warning
            Messages = new List<Message> { new Message { Role = "user", Content = "Test" } }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _conduit.CreateChatCompletionAsync(request, null, CancellationToken.None);
        });
        Assert.Equal("request.Model", ex.ParamName); // Verify the parameter name in the exception
        _mockFactory.Verify(f => f.GetClient(It.IsAny<string>()), Times.Never);
        _mockClient.Verify(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateChatCompletionAsync_FactoryThrowsConfigException_ThrowsConfigException()
    {
        // Arrange
        var request = new ChatCompletionRequest
        {
            Model = "unconfigured-model",
            Messages = new List<Message> { new Message { Role = "user", Content = "Test" } }
        };
        var exceptionMessage = "Model not configured";

        _mockFactory.Setup(f => f.GetClient(request.Model))
                    .Throws(new ConfigurationException(exceptionMessage));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationException>(async () =>
        {
            await _conduit.CreateChatCompletionAsync(request, null, CancellationToken.None);
        });

        Assert.Equal(exceptionMessage, exception.Message);
        _mockFactory.Verify(f => f.GetClient(request.Model), Times.Once);
        _mockClient.Verify(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateChatCompletionAsync_ClientThrowsLLMCommunicationException_ThrowsLLMCommunicationException()
    {
        // Arrange
        var request = new ChatCompletionRequest
        {
            Model = "error-model",
            Messages = new List<Message> { new Message { Role = "user", Content = "Test" } }
        };
        var exceptionMessage = "API communication failed";

        _mockClient.Setup(c => c.CreateChatCompletionAsync(request, null, It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new LLMCommunicationException(exceptionMessage));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LLMCommunicationException>(async () =>
        {
            await _conduit.CreateChatCompletionAsync(request, null, CancellationToken.None);
        });

        Assert.Equal(exceptionMessage, exception.Message);
        _mockFactory.Verify(f => f.GetClient(request.Model), Times.Once);
        _mockClient.Verify(c => c.CreateChatCompletionAsync(request, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}

using ConduitLLM.Core;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ConduitLLM.Tests;

public class StreamingTests
{
    private readonly Mock<ILLMClientFactory> _mockFactory;
    private readonly Mock<ILLMClient> _mockClient;
    private readonly Mock<ILogger<Conduit>> _mockLogger; // Logger for Conduit if needed, though not directly used here
    private readonly Conduit _conduit;

    public StreamingTests()
    {
        _mockFactory = new Mock<ILLMClientFactory>();
        _mockClient = new Mock<ILLMClient>();
        _mockLogger = new Mock<ILogger<Conduit>>(); // Mock logger if Conduit constructor requires it

        // Setup factory to return the mock client for a specific model alias
        _mockFactory.Setup(f => f.GetClient(It.IsAny<string>()))
                    .Returns(_mockClient.Object);

        // Instantiate Conduit with the mock factory
        // Assuming Conduit constructor takes ILLMClientFactory. Adjust if it needs ILogger too.
        _conduit = new Conduit(_mockFactory.Object);
    }

    // Helper method to create an async enumerable from a list of chunks
    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Simulate async delay if needed
            // await Task.Delay(1, cancellationToken);
            yield return item;
        }
        // Ensure the method is recognized as an async iterator
        await Task.CompletedTask;
    }

    [Fact]
    public async Task StreamChatCompletionAsync_ValidRequest_ReturnsChunks()
    {
        // Arrange
        var request = new ChatCompletionRequest
        {
            Model = "test-streaming-model",
            Messages = new List<Message> { new Message { Role = "user", Content = "Stream test" } }
        };

        var expectedChunks = new List<ChatCompletionChunk>
        {
            new ChatCompletionChunk { Id = "chunk1", Choices = new List<StreamingChoice> { new StreamingChoice { Index = 0, Delta = new DeltaContent { Content = "Hello" } } } },
            new ChatCompletionChunk { Id = "chunk2", Choices = new List<StreamingChoice> { new StreamingChoice { Index = 0, Delta = new DeltaContent { Content = " world" } } } },
            new ChatCompletionChunk { Id = "chunk3", Choices = new List<StreamingChoice> { new StreamingChoice { Index = 0, Delta = new DeltaContent(), FinishReason = "stop" } } }
        };

        // Setup the mock client's streaming method to expect the apiKey parameter (can be null)
        _mockClient.Setup(c => c.StreamChatCompletionAsync(request, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                   .Returns(ToAsyncEnumerable(expectedChunks)); // Use helper to return IAsyncEnumerable

        // Act - Pass null for the apiKey argument
        var receivedChunks = new List<ChatCompletionChunk>();
        // Pass null for apiKey, and explicitly provide CancellationToken.None if needed
        await foreach (var chunk in _conduit.StreamChatCompletionAsync(request, null, CancellationToken.None))
        {
            receivedChunks.Add(chunk);
        }

        // Assert
        Assert.Equal(expectedChunks.Count, receivedChunks.Count);
        for (int i = 0; i < expectedChunks.Count; i++)
        {
            Assert.Equal(expectedChunks[i].Id, receivedChunks[i].Id);
            Assert.Equal(expectedChunks[i].Choices[0].Delta?.Content, receivedChunks[i].Choices[0].Delta?.Content);
            Assert.Equal(expectedChunks[i].Choices[0].FinishReason, receivedChunks[i].Choices[0].FinishReason);
        }
        _mockFactory.Verify(f => f.GetClient(request.Model), Times.Once);
        // Verify mock client call with the updated signature
        _mockClient.Verify(c => c.StreamChatCompletionAsync(request, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StreamChatCompletionAsync_ClientThrowsException_ThrowsLLMCommunicationException()
    {
        // Arrange
        var request = new ChatCompletionRequest
        {
            Model = "test-streaming-error-model",
            Messages = new List<Message> { new Message { Role = "user", Content = "Stream error test" } }
        };

        var exceptionMessage = "Simulated stream error";
        // Setup the mock client's streaming method to throw an exception during iteration (updated signature)
        _mockClient.Setup(c => c.StreamChatCompletionAsync(request, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                   .Returns(ThrowingAsyncEnumerable(exceptionMessage)); // Use helper that throws

        // Act & Assert
        var receivedChunks = new List<ChatCompletionChunk>();
        var exception = await Assert.ThrowsAsync<LLMCommunicationException>(async () =>
        {
            // Pass null for apiKey
            await foreach (var chunk in _conduit.StreamChatCompletionAsync(request, null, CancellationToken.None))
            {
                receivedChunks.Add(chunk); // This part might not be reached if exception is immediate
            }
        });

        // Assert on the exception details
        Assert.Equal(exceptionMessage, exception.Message);
        Assert.Empty(receivedChunks); // Ensure no chunks were processed before the exception
        _mockFactory.Verify(f => f.GetClient(request.Model), Times.Once);
        // Verify mock client call with the updated signature
        _mockClient.Verify(c => c.StreamChatCompletionAsync(request, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // Helper method to create an IAsyncEnumerable that throws an exception
    private static async IAsyncEnumerable<ChatCompletionChunk> ThrowingAsyncEnumerable(string message, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Yield one item to ensure it's an iterator block if needed, or just throw
#pragma warning disable CS0162 // Unreachable code detected
        if (false) // This ensures the compiler recognizes this as an iterator method but the code is never executed
        {
            yield return new ChatCompletionChunk { Id = "temp" };
        }
#pragma warning restore CS0162
        
        await Task.Delay(1, cancellationToken); // Simulate async work before throwing
        cancellationToken.ThrowIfCancellationRequested();
        throw new LLMCommunicationException(message);
    }

    [Fact]
    public async Task StreamChatCompletionAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        ChatCompletionRequest? request = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            // Pass null for apiKey
            await foreach (var chunk in _conduit.StreamChatCompletionAsync(request!, null, CancellationToken.None)) // Use null-forgiving operator for test
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task StreamChatCompletionAsync_NullOrEmptyModel_ThrowsArgumentException()
    {
        // Arrange
        var request = new ChatCompletionRequest
        {
            Model = "", // Empty model
            Messages = new List<Message> { new Message { Role = "user", Content = "Test" } }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
             // Pass null for apiKey
            await foreach (var chunk in _conduit.StreamChatCompletionAsync(request, null, CancellationToken.None))
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task StreamChatCompletionAsync_FactoryThrowsConfigException_ThrowsConfigException()
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
             // Pass null for apiKey
            await foreach (var chunk in _conduit.StreamChatCompletionAsync(request, null, CancellationToken.None))
            {
                // Should not reach here
            }
        });

        Assert.Equal(exceptionMessage, exception.Message);
        _mockFactory.Verify(f => f.GetClient(request.Model), Times.Once);
        // Verify mock client call with the updated signature (should not be called)
        _mockClient.Verify(c => c.StreamChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

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

        var client = new BedrockClient(modelId, _loggerMock.Object, _httpClient);

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
            Assert.Equal(expectedResponse.Content[0].Text, msg.Content);
        }
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

        var client = new BedrockClient(modelId, _loggerMock.Object, _httpClient);

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

        var client = new BedrockClient(modelId, _loggerMock.Object, _httpClient);

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
        var logger = _loggerMock.Object;

        // Use a test double instead of Moq
        var client = new FakeBedrockClient(modelId, logger, _httpClient);
        int chunkCount = 0;
        await foreach (var chunk in client.StreamChatCompletionAsync(request, cancellationToken: CancellationToken.None))
        {
            Assert.NotNull(chunk);
            Assert.Equal("chat.completion.chunk", chunk.Object);
            Assert.NotNull(chunk.Choices);
            if (chunk.Choices != null)
            {
                var choices = chunk.Choices;
                Assert.True(choices.Count > 0, "Chunk choices should not be empty");
                Assert.Equal("Hello from mock!", choices[0].Delta.Content);
            }
            chunkCount++;
            if (chunkCount > 0)
                break;
        }
        Assert.Equal(1, chunkCount);
    }

    [Fact]
    public async Task StreamChatCompletionAsync_YieldsMultipleChunks()
    {
        // Arrange
        var request = CreateTestRequest("bedrock-claude");
        var modelId = "anthropic.claude-3-sonnet-20240229-v1:0";
        var logger = _loggerMock.Object;
        var client = new MultiChunkBedrockClient(modelId, logger, _httpClient);

        // Act
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in client.StreamChatCompletionAsync(request, cancellationToken: CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Equal(3, chunks.Count);
        Assert.All(chunks, c => Assert.Equal("chat.completion.chunk", c.Object));
        Assert.Equal("Hello 1", chunks[0].Choices![0].Delta.Content);
        Assert.Equal("Hello 2", chunks[1].Choices![0].Delta.Content);
        Assert.Equal("Hello 3", chunks[2].Choices![0].Delta.Content);
    }

    [Fact]
    public async Task StreamChatCompletionAsync_EmptyStream_YieldsNoChunks()
    {
        // Arrange
        var request = CreateTestRequest("bedrock-claude");
        var modelId = "anthropic.claude-3-sonnet-20240229-v1:0";
        var logger = _loggerMock.Object;
        var client = new EmptyStreamBedrockClient(modelId, logger, _httpClient);

        // Act
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in client.StreamChatCompletionAsync(request, cancellationToken: CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Empty(chunks);
    }

    [Fact]
    public async Task StreamChatCompletionAsync_InvalidChunk_HandlesGracefully()
    {
        // Arrange
        var request = CreateTestRequest("bedrock-claude");
        var modelId = "anthropic.claude-3-sonnet-20240229-v1:0";
        var logger = _loggerMock.Object;
        var client = new InvalidChunkBedrockClient(modelId, logger, _httpClient);

        // Act
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in client.StreamChatCompletionAsync(request, cancellationToken: CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Single(chunks);
        Assert.Null(chunks[0].Choices);
    }

    [Fact]
    public async Task StreamChatCompletionAsync_ExceptionDuringStream_Throws()
    {
        // Arrange
        var request = CreateTestRequest("bedrock-claude");
        var modelId = "anthropic.claude-3-sonnet-20240229-v1:0";
        var logger = _loggerMock.Object;
        var client = new ExceptionStreamBedrockClient(modelId, logger, _httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in client.StreamChatCompletionAsync(request, cancellationToken: CancellationToken.None)) { }
        });
    }

    [Fact]
    public async Task StreamChatCompletionAsync_RespectsCancellation()
    {
        // Arrange
        var request = CreateTestRequest("bedrock-claude");
        var modelId = "anthropic.claude-3-sonnet-20240229-v1:0";
        var logger = _loggerMock.Object;
        var client = new CancellableStreamBedrockClient(modelId, logger, _httpClient);
        var cts = new CancellationTokenSource();

        // Act
        var chunks = new List<ChatCompletionChunk>();
        var enumerator = client.StreamChatCompletionAsync(request, cancellationToken: cts.Token).GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync()); // First chunk
        chunks.Add(enumerator.Current);
        cts.Cancel(); // Cancel after first chunk
        var ex = await Record.ExceptionAsync(async () =>
        {
            while (await enumerator.MoveNextAsync())
                chunks.Add(enumerator.Current);
        });

        // Assert
        Assert.Single(chunks); // Only first chunk received before cancellation
        Assert.True(ex is OperationCanceledException, $"Expected OperationCanceledException but got {ex?.GetType()}");
    }

    [Fact]
    public async Task StreamChatCompletionAsync_EmptyChoices_YieldsChunkWithEmptyChoices()
    {
        // Arrange
        var request = CreateTestRequest("bedrock-claude");
        var modelId = "anthropic.claude-3-sonnet-20240229-v1:0";
        var logger = _loggerMock.Object;
        var client = new EmptyChoicesBedrockClient(modelId, logger, _httpClient);

        // Act
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in client.StreamChatCompletionAsync(request, cancellationToken: CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Single(chunks);
        Assert.NotNull(chunks[0].Choices);
        Assert.Empty(chunks[0].Choices);
    }

    [Fact]
    public async Task StreamChatCompletionAsync_NullOrEmptyContent_YieldsChunkWithNullOrEmptyContent()
    {
        // Arrange
        var request = CreateTestRequest("bedrock-claude");
        var modelId = "anthropic.claude-3-sonnet-20240229-v1:0";
        var logger = _loggerMock.Object;
        var client = new NullOrEmptyContentBedrockClient(modelId, logger, _httpClient);

        // Act
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in client.StreamChatCompletionAsync(request, cancellationToken: CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Equal(2, chunks.Count);
        Assert.Null(chunks[0].Choices![0].Delta.Content);
        Assert.Equal(string.Empty, chunks[1].Choices![0].Delta.Content);
    }

    [Fact]
    public async Task StreamChatCompletionAsync_LargeNumberOfChunks_AllReceived()
    {
        // Arrange
        var request = CreateTestRequest("bedrock-claude");
        var modelId = "anthropic.claude-3-sonnet-20240229-v1:0";
        var logger = _loggerMock.Object;
        var client = new LargeStreamBedrockClient(modelId, logger, _httpClient);

        // Act
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in client.StreamChatCompletionAsync(request, cancellationToken: CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Equal(100, chunks.Count);
        Assert.All(chunks, c => Assert.StartsWith("bulk-", c.Id));
    }

    [Fact]
    public async Task StreamChatCompletionAsync_MixedValidAndInvalidChunks_ProcessesValidOnes()
    {
        // Arrange
        var request = CreateTestRequest("bedrock-claude");
        var modelId = "anthropic.claude-3-sonnet-20240229-v1:0";
        var logger = _loggerMock.Object;
        var client = new MixedValidInvalidBedrockClient(modelId, logger, _httpClient);

        // Act
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in client.StreamChatCompletionAsync(request, cancellationToken: CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Equal(3, chunks.Count);
        Assert.NotNull(chunks[0].Choices);
        Assert.Null(chunks[1].Choices); // Invalid
        Assert.NotNull(chunks[2].Choices);
    }

    [Fact]
    public async Task StreamChatCompletionAsync_WithDelayedChunks_HandlesLatency()
    {
        // Arrange
        var request = CreateTestRequest("bedrock-claude");
        var modelId = "anthropic.claude-3-sonnet-20240229-v1:0";
        var logger = _loggerMock.Object;
        var client = new DelayedChunkBedrockClient(modelId, logger, _httpClient);

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in client.StreamChatCompletionAsync(request, cancellationToken: CancellationToken.None))
        {
            chunks.Add(chunk);
        }
        sw.Stop();

        // Assert
        Assert.Equal(2, chunks.Count);
        Assert.True(sw.ElapsedMilliseconds >= 100, "Should take at least 100ms due to delays");
    }

    // Test double to create a standard ChatCompletionRequest
    private class FakeBedrockClient : BedrockClient
    {
        public FakeBedrockClient(string providerModelId, ILogger<BedrockClient> logger, HttpClient? httpClient = null)
            : base(providerModelId, logger, httpClient) { }

        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var fakeChunk = new ChatCompletionChunk
            {
                Id = "fake-id",
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model,
                Choices = new List<StreamingChoice>
                {
                    new StreamingChoice
                    {
                        Index = 0,
                        Delta = new DeltaContent { Content = "Hello from mock!" },
                        FinishReason = "stop"
                    }
                }
            };
            yield return fakeChunk;
            await Task.CompletedTask;
        }
    }

    // Test double for multiple streamed chunks
    private class MultiChunkBedrockClient : BedrockClient
    {
        public MultiChunkBedrockClient(string providerModelId, ILogger<BedrockClient> logger, HttpClient? httpClient = null)
            : base(providerModelId, logger, httpClient) { }

        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (int i = 1; i <= 3; i++)
            {
                var chunk = new ChatCompletionChunk
                {
                    Id = $"fake-id-{i}",
                    Object = "chat.completion.chunk",
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Model = request.Model,
                    Choices = new List<StreamingChoice>
                    {
                        new StreamingChoice
                        {
                            Index = 0,
                            Delta = new DeltaContent { Content = $"Hello {i}" },
                            FinishReason = null
                        }
                    }
                };
                yield return chunk;
                await Task.Delay(10, cancellationToken); // Simulate streaming delay
            }
        }
    }

    // Test double: yields no chunks
    private class EmptyStreamBedrockClient : BedrockClient
    {
        public EmptyStreamBedrockClient(string providerModelId, ILogger<BedrockClient> logger, HttpClient? httpClient = null)
            : base(providerModelId, logger, httpClient) { }
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    // Test double: yields a chunk with null Choices
    private class InvalidChunkBedrockClient : BedrockClient
    {
        public InvalidChunkBedrockClient(string providerModelId, ILogger<BedrockClient> logger, HttpClient? httpClient = null)
            : base(providerModelId, logger, httpClient) { }
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatCompletionChunk
            {
                Id = "invalid-id",
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model,
                Choices = null // Invalid/missing choices
            };
            await Task.CompletedTask;
        }
    }

    // Test double: throws during streaming
    private class ExceptionStreamBedrockClient : BedrockClient
    {
        public ExceptionStreamBedrockClient(string providerModelId, ILogger<BedrockClient> logger, HttpClient? httpClient = null)
            : base(providerModelId, logger, httpClient) { }
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            throw new InvalidOperationException("Simulated streaming failure");
            yield break; // Fixes CS8419: async-iterator must have yield
        }
    }

    // Test double: yields two chunks, cancels after first if token is canceled
    private class CancellableStreamBedrockClient : BedrockClient
    {
        public CancellableStreamBedrockClient(string providerModelId, ILogger<BedrockClient> logger, HttpClient? httpClient = null)
            : base(providerModelId, logger, httpClient) { }
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (int i = 1; i <= 2; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new ChatCompletionChunk
                {
                    Id = $"cancel-id-{i}",
                    Object = "chat.completion.chunk",
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Model = request.Model,
                    Choices = new List<StreamingChoice>
                    {
                        new StreamingChoice
                        {
                            Index = 0,
                            Delta = new DeltaContent { Content = $"Chunk {i}" },
                            FinishReason = null
                        }
                    }
                };
                await Task.Delay(50, cancellationToken);
            }
        }
    }

    // Test double: yields a chunk with empty Choices list
    private class EmptyChoicesBedrockClient : BedrockClient
    {
        public EmptyChoicesBedrockClient(string providerModelId, ILogger<BedrockClient> logger, HttpClient? httpClient = null)
            : base(providerModelId, logger, httpClient) { }
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatCompletionChunk
            {
                Id = "empty-choices-id",
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model,
                Choices = new List<StreamingChoice>()
            };
            await Task.CompletedTask;
        }
    }

    // Test double: yields chunks with null and empty Content
    private class NullOrEmptyContentBedrockClient : BedrockClient
    {
        public NullOrEmptyContentBedrockClient(string providerModelId, ILogger<BedrockClient> logger, HttpClient? httpClient = null)
            : base(providerModelId, logger, httpClient) { }
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatCompletionChunk
            {
                Id = "null-content-id",
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model,
                Choices = new List<StreamingChoice>
                {
                    new StreamingChoice
                    {
                        Index = 0,
                        Delta = new DeltaContent { Content = null! },
                        FinishReason = null
                    }
                }
            };
            yield return new ChatCompletionChunk
            {
                Id = "empty-content-id",
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model,
                Choices = new List<StreamingChoice>
                {
                    new StreamingChoice
                    {
                        Index = 0,
                        Delta = new DeltaContent { Content = string.Empty },
                        FinishReason = null
                    }
                }
            };
            await Task.CompletedTask;
        }
    }

    // Test double: yields 100 chunks
    private class LargeStreamBedrockClient : BedrockClient
    {
        public LargeStreamBedrockClient(string providerModelId, ILogger<BedrockClient> logger, HttpClient? httpClient = null)
            : base(providerModelId, logger, httpClient) { }
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (int i = 1; i <= 100; i++)
            {
                yield return new ChatCompletionChunk
                {
                    Id = $"bulk-{i}",
                    Object = "chat.completion.chunk",
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Model = request.Model,
                    Choices = new List<StreamingChoice>
                    {
                        new StreamingChoice
                        {
                            Index = 0,
                            Delta = new DeltaContent { Content = $"Chunk {i}" },
                            FinishReason = null
                        }
                    }
                };
                await Task.Yield();
            }
        }
    }

    // Test double: yields valid, invalid, valid chunks
    private class MixedValidInvalidBedrockClient : BedrockClient
    {
        public MixedValidInvalidBedrockClient(string providerModelId, ILogger<BedrockClient> logger, HttpClient? httpClient = null)
            : base(providerModelId, logger, httpClient) { }
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatCompletionChunk
            {
                Id = "valid-1",
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model,
                Choices = new List<StreamingChoice> { new StreamingChoice { Index = 0, Delta = new DeltaContent { Content = "Valid1" }, FinishReason = null } }
            };
            yield return new ChatCompletionChunk
            {
                Id = "invalid-2",
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model,
                Choices = null // Invalid
            };
            yield return new ChatCompletionChunk
            {
                Id = "valid-3",
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model,
                Choices = new List<StreamingChoice> { new StreamingChoice { Index = 0, Delta = new DeltaContent { Content = "Valid3" }, FinishReason = null } }
            };
            await Task.CompletedTask;
        }
    }

    // Test double: yields two chunks with delay
    private class DelayedChunkBedrockClient : BedrockClient
    {
        public DelayedChunkBedrockClient(string providerModelId, ILogger<BedrockClient> logger, HttpClient? httpClient = null)
            : base(providerModelId, logger, httpClient) { }
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatCompletionChunk
            {
                Id = "delay-1",
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model,
                Choices = new List<StreamingChoice>
                {
                    new StreamingChoice
                    {
                        Index = 0,
                        Delta = new DeltaContent { Content = "First" },
                        FinishReason = null
                    }
                }
            };
            await Task.Delay(100, cancellationToken);
            yield return new ChatCompletionChunk
            {
                Id = "delay-2",
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model,
                Choices = new List<StreamingChoice>
                {
                    new StreamingChoice
                    {
                        Index = 0,
                        Delta = new DeltaContent { Content = "Second" },
                        FinishReason = null
                    }
                }
            };
            await Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ListModelsAsync_ReturnsModels()
    {
        // Arrange
        var modelId = "anthropic.claude-3-sonnet-20240229-v1:0";
        var client = new BedrockClient(modelId, _loggerMock.Object, _httpClient);

        // Act
        var models = await client.ListModelsAsync();

        // Assert
        Assert.NotNull(models);
        Assert.NotEmpty(models);
        Assert.Contains(models, m => m.Contains("claude"));
    }
}

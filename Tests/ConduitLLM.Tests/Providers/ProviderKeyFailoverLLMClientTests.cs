using System.Net;
using System.Runtime.CompilerServices;

using ConduitLLM.Core.Decorators;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers;

using Moq;

namespace ConduitLLM.Tests.Providers;

public sealed class ProviderKeyFailoverLLMClientTests
{
    [Fact]
    public async Task FatalError_RetriesNextKeyAfterContextTracksFailure()
    {
        var fatalError = Fatal(HttpStatusCode.Unauthorized);
        var primary = new ScriptedClient
        {
            ChatHandler = () => Task.FromException<ChatCompletionResponse>(fatalError)
        };
        var expected = Response("fallback");
        var fallback = new ScriptedClient
        {
            ChatHandler = () => Task.FromResult(expected)
        };
        var tracker = new Mock<IProviderErrorTrackingService>();
        tracker
            .Setup(service => service.TrackErrorAsync(It.IsAny<ProviderErrorInfo>()))
            .Returns(Task.CompletedTask);
        var services = new Mock<IServiceProvider>();
        services
            .Setup(provider => provider.GetService(typeof(IProviderErrorTrackingService)))
            .Returns(tracker.Object);
        var trackedPrimary = new ContextAwareLLMClient(primary, 11, 7, services.Object);
        var client = CreateClient((11, 0, trackedPrimary), (12, 0, fallback));

        var response = await client.CreateChatCompletionAsync(Request());

        Assert.Same(expected, response);
        Assert.Equal(1, primary.ChatAttempts);
        Assert.Equal(1, fallback.ChatAttempts);
        tracker.Verify(service => service.TrackErrorAsync(
            It.Is<ProviderErrorInfo>(error =>
                error.KeyCredentialId == 11 &&
                error.ProviderId == 7 &&
                error.ErrorType == ProviderErrorType.InvalidApiKey)), Times.Once);
    }

    [Fact]
    public async Task RateLimitError_DoesNotFailOver()
    {
        var rateLimitError = Fatal(HttpStatusCode.TooManyRequests);
        var primary = new ScriptedClient
        {
            ChatHandler = () => Task.FromException<ChatCompletionResponse>(rateLimitError)
        };
        var fallback = SuccessfulClient("fallback");
        var client = CreateClient((1, 0, primary), (2, 0, fallback));

        var thrown = await Assert.ThrowsAsync<LLMCommunicationException>(
            () => client.CreateChatCompletionAsync(Request()));

        Assert.Same(rateLimitError, thrown);
        Assert.Equal(1, primary.ChatAttempts);
        Assert.Equal(0, fallback.ChatAttempts);
    }

    [Fact]
    public async Task FatalErrors_AreCappedAtTwoAdditionalAttemptsAndSurfaceOriginal()
    {
        var original = Fatal(HttpStatusCode.Unauthorized, "primary");
        var first = FailingClient(original);
        var second = FailingClient(Fatal(HttpStatusCode.Forbidden, "second"));
        var third = FailingClient(Fatal(HttpStatusCode.PaymentRequired, "third"));
        var fourth = SuccessfulClient("must-not-run");
        var client = CreateClient(
            (1, 0, first),
            (2, 0, second),
            (3, 0, third),
            (4, 0, fourth));

        var thrown = await Assert.ThrowsAsync<LLMCommunicationException>(
            () => client.CreateChatCompletionAsync(Request()));

        Assert.Same(original, thrown);
        Assert.Equal(1, first.ChatAttempts);
        Assert.Equal(1, second.ChatAttempts);
        Assert.Equal(1, third.ChatAttempts);
        Assert.Equal(0, fourth.ChatAttempts);
    }

    [Fact]
    public async Task InsufficientBalance_SkipsOtherKeysInSameAccountGroup()
    {
        var exhausted = FailingClient(Fatal(HttpStatusCode.PaymentRequired));
        var sameAccount = SuccessfulClient("same-account");
        var separateAccount = SuccessfulClient("separate-account");
        var client = CreateClient(
            (1, 4, exhausted),
            (2, 4, sameAccount),
            (3, 5, separateAccount));

        var response = await client.CreateChatCompletionAsync(Request());

        Assert.Equal("separate-account", response.Id);
        Assert.Equal(0, sameAccount.ChatAttempts);
        Assert.Equal(1, separateAccount.ChatAttempts);
    }

    [Fact]
    public async Task QuotaClassifiedForbidden_SkipsOtherKeysInSameAccountGroup()
    {
        var exhausted = FailingClient(new LLMCommunicationException(
            "quota exhausted",
            HttpStatusCode.Forbidden,
            """{"code":"insufficient_quota"}"""));
        var sameAccount = SuccessfulClient("same-account");
        var separateAccount = SuccessfulClient("separate-account");
        var client = CreateClient(
            (1, 4, exhausted),
            (2, 4, sameAccount),
            (3, 5, separateAccount));

        var response = await client.CreateChatCompletionAsync(Request());

        Assert.Equal("separate-account", response.Id);
        Assert.Equal(0, sameAccount.ChatAttempts);
    }

    [Fact]
    public async Task UngroupedBalanceFailure_AllowsNextUngroupedKey()
    {
        var exhausted = FailingClient(Fatal(HttpStatusCode.PaymentRequired));
        var fallback = SuccessfulClient("ungrouped-fallback");
        var client = CreateClient((1, 0, exhausted), (2, 0, fallback));

        var response = await client.CreateChatCompletionAsync(Request());

        Assert.Equal("ungrouped-fallback", response.Id);
        Assert.Equal(1, fallback.ChatAttempts);
    }

    [Fact]
    public async Task StreamingFatalBeforeFirstChunk_FailsOver()
    {
        var primary = new ScriptedClient
        {
            StreamHandler = () => FailBeforeFirstChunk(Fatal(HttpStatusCode.Unauthorized))
        };
        var fallback = new ScriptedClient
        {
            StreamHandler = () => CompletedStream("fallback")
        };
        var client = CreateClient((1, 0, primary), (2, 0, fallback));
        var chunks = new List<ChatCompletionChunk>();

        await foreach (var chunk in client.StreamChatCompletionAsync(Request()))
        {
            chunks.Add(chunk);
        }

        Assert.Single(chunks);
        Assert.Equal("fallback", chunks[0].Id);
        Assert.Equal(1, primary.StreamAttempts);
        Assert.Equal(1, fallback.StreamAttempts);
    }

    [Fact]
    public async Task StreamingFatalAfterFirstChunk_DoesNotFailOver()
    {
        var streamError = Fatal(HttpStatusCode.Forbidden);
        var primary = new ScriptedClient
        {
            StreamHandler = () => FailAfterFirstChunk(streamError)
        };
        var fallback = new ScriptedClient
        {
            StreamHandler = () => CompletedStream("fallback")
        };
        var client = CreateClient((1, 0, primary), (2, 0, fallback));
        var chunks = new List<ChatCompletionChunk>();

        var thrown = await Assert.ThrowsAsync<LLMCommunicationException>(async () =>
        {
            await foreach (var chunk in client.StreamChatCompletionAsync(Request()))
            {
                chunks.Add(chunk);
            }
        });

        Assert.Same(streamError, thrown);
        Assert.Single(chunks);
        Assert.Equal("visible", chunks[0].Id);
        Assert.Equal(0, fallback.StreamAttempts);
    }

    private static ProviderKeyFailoverLLMClient CreateClient(
        params (int KeyId, int Group, ILLMClient Client)[] targets)
    {
        return new ProviderKeyFailoverLLMClient(
            targets
                .Select(target => new ProviderKeyFailoverTarget(
                    target.KeyId,
                    target.Group,
                    () => target.Client))
                .ToArray());
    }

    private static ScriptedClient FailingClient(LLMCommunicationException exception)
        => new()
        {
            ChatHandler = () => Task.FromException<ChatCompletionResponse>(exception)
        };

    private static ScriptedClient SuccessfulClient(string id)
        => new()
        {
            ChatHandler = () => Task.FromResult(Response(id))
        };

    private static ChatCompletionRequest Request()
        => new()
        {
            Model = "test-model",
            Messages = [new Message { Role = "user", Content = "hello" }]
        };

    private static ChatCompletionResponse Response(string id = "response")
        => new()
        {
            Id = id,
            Choices = [],
            Created = 0,
            Model = "test-model",
            Object = "chat.completion"
        };

    private static LLMCommunicationException Fatal(
        HttpStatusCode statusCode,
        string message = "provider rejected credential")
        => new(message, statusCode, null);

    private static async IAsyncEnumerable<ChatCompletionChunk> FailBeforeFirstChunk(
        Exception exception,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        if (exception is not null)
        {
            throw exception;
        }

        yield break;
    }

    private static async IAsyncEnumerable<ChatCompletionChunk> CompletedStream(
        string id,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return new ChatCompletionChunk { Id = id };
    }

    private static async IAsyncEnumerable<ChatCompletionChunk> FailAfterFirstChunk(
        Exception exception,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new ChatCompletionChunk { Id = "visible" };
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        throw exception;
    }

    private sealed class ScriptedClient : ILLMClient
    {
        public Func<Task<ChatCompletionResponse>> ChatHandler { get; init; }
            = () => Task.FromResult(Response());

        public Func<IAsyncEnumerable<ChatCompletionChunk>> StreamHandler { get; init; }
            = () => CompletedStream("default");

        public int ChatAttempts { get; private set; }
        public int StreamAttempts { get; private set; }

        public async Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ChatAttempts++;
            return await ChatHandler();
        }

        public IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            StreamAttempts++;
            return StreamHandler();
        }

        public Task<List<string>> ListModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new List<string>());

        public Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new EmbeddingResponse
            {
                Object = "list",
                Data = [],
                Model = "test-model",
                Usage = new Usage()
            });

        public Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ImageGenerationResponse
            {
                Created = 0,
                Data = []
            });

        public Task<ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null)
            => Task.FromResult(new ProviderCapabilities());
    }
}

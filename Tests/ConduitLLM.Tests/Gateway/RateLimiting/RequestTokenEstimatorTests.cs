using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.RateLimiting;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace ConduitLLM.Tests.Gateway.RateLimiting;

[Trait("Category", "Unit")]
[Trait("Component", "RequestTokenEstimator")]
public class RequestTokenEstimatorTests
{
    private readonly Mock<ITokenCounter> _counter = new();
    private readonly RateLimitOptions _options = new()
    {
        DefaultCompletionTokenBudget = 1_024,
        MaxCompletionTokenReservation = 32_768
    };

    private RequestTokenEstimator Estimator() =>
        new(_counter.Object, _options, NullLogger<RequestTokenEstimator>.Instance);

    [Fact]
    public async Task EstimateAsync_Chat_AddsThePromptToTheDeclaredCompletionCeiling()
    {
        _counter.Setup(x => x.EstimateTokenCountAsync("gpt-5", It.IsAny<List<Message>>())).ReturnsAsync(3_400);

        var estimate = await Estimator().EstimateAsync(Chat(maxTokens: 600));

        estimate!.Value.PromptTokens.Should().Be(3_400);
        estimate.Value.CompletionBudget.Should().Be(600);
        estimate.Value.Total.Should().Be(4_000);
    }

    [Fact]
    public async Task EstimateAsync_Chat_WithoutADeclaredCeiling_UsesTheConfiguredDefault()
    {
        // An uncapped request could otherwise reserve — and block — the entire window.
        _counter.Setup(x => x.EstimateTokenCountAsync("gpt-5", It.IsAny<List<Message>>())).ReturnsAsync(100);

        var estimate = await Estimator().EstimateAsync(Chat(maxTokens: null));

        estimate!.Value.CompletionBudget.Should().Be(1_024);
    }

    [Fact]
    public async Task EstimateAsync_Chat_PrefersMaxCompletionTokensOverLegacyMaxTokens()
    {
        _counter.Setup(x => x.EstimateTokenCountAsync("gpt-5", It.IsAny<List<Message>>())).ReturnsAsync(50);

        var request = Chat(maxTokens: 100);
        request.MaxCompletionTokens = 900;

        var estimate = await Estimator().EstimateAsync(request);

        estimate!.Value.CompletionBudget.Should().Be(900);
    }

    [Fact]
    public async Task EstimateAsync_Chat_ClampsAnOutsizedDeclaredCeiling()
    {
        _counter.Setup(x => x.EstimateTokenCountAsync("gpt-5", It.IsAny<List<Message>>())).ReturnsAsync(10);

        var estimate = await Estimator().EstimateAsync(Chat(maxTokens: 5_000_000));

        estimate!.Value.CompletionBudget.Should().Be(32_768,
            "one request declaring an enormous max_tokens must not park the whole window");
    }

    [Fact]
    public async Task EstimateAsync_Embedding_CountsInputOnly()
    {
        _counter.Setup(x => x.EstimateTokenCountAsync("text-embedding-3", It.IsAny<string>())).ReturnsAsync(250);

        var estimate = await Estimator().EstimateAsync(new EmbeddingRequest
        {
            Model = "text-embedding-3",
            Input = "some text to embed"
        });

        estimate!.Value.PromptTokens.Should().Be(250);
        estimate.Value.CompletionBudget.Should().Be(0, "embeddings return vectors, not tokens");
    }

    [Fact]
    public async Task EstimateAsync_EmbeddingWithArrayInput_FlattensBeforeCounting()
    {
        string? counted = null;
        _counter.Setup(x => x.EstimateTokenCountAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback((string _, string text) => counted = text)
            .ReturnsAsync(12);

        await Estimator().EstimateAsync(new EmbeddingRequest
        {
            Model = "text-embedding-3",
            Input = new[] { "first", "second" }
        });

        counted.Should().Be("first second");
    }

    [Fact]
    public async Task EstimateAsync_RequestWithoutTokenSemantics_ReturnsNull()
    {
        // Image and audio requests are not measured in tokens, so they carry no token window.
        (await Estimator().EstimateAsync(new ImageGenerationRequest { Model = "dall-e-3", Prompt = "a cat" }))
            .Should().BeNull();
        (await Estimator().EstimateAsync(null)).Should().BeNull();
    }

    [Fact]
    public async Task EstimateAsync_WhenCountingThrows_FallsBackToNoTokenWindow()
    {
        // A tokeniser failure must not turn into a failed request.
        _counter.Setup(x => x.EstimateTokenCountAsync(It.IsAny<string>(), It.IsAny<List<Message>>()))
            .ThrowsAsync(new InvalidOperationException("tokeniser unavailable"));

        (await Estimator().EstimateAsync(Chat(maxTokens: 10))).Should().BeNull();
    }

    private static ChatCompletionRequest Chat(int? maxTokens) => new()
    {
        Model = "gpt-5",
        Messages = new List<Message>
        {
            new() { Role = MessageRole.User, Content = "hello" }
        },
        MaxTokens = maxTokens
    };
}

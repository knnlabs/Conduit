using System.Text;
using System.Text.Json;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Pricing;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Middleware;
using ConduitLLM.Gateway.Services;
using ConduitLLM.Gateway.UsageTracking;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;

namespace ConduitLLM.BillingInvariantTests;

public sealed class ProviderUsageGoldenCorpusTests
{
    private const int VirtualKeyId = 1035;

    private static readonly IReadOnlyDictionary<string, GoldenCase> Cases =
        new Dictionary<string, GoldenCase>(StringComparer.Ordinal)
        {
            ["openai-top-level"] = new(
                Fixture: "openai-top-level.json",
                Path: "/v1/chat/completions",
                ProviderType: "OpenAI",
                Model: "golden-openai-top-level",
                ModelCost: StandardCost(
                    "golden-openai-top-level",
                    inputRate: 2m,
                    outputRate: 8m),
                ExpectedUsage: new Usage
                {
                    PromptTokens = 1000,
                    CompletionTokens = 250,
                    TotalTokens = 1250
                },
                ExpectedCost: 0.004m),

            ["openai-nested-details"] = new(
                Fixture: "openai-nested-details.json",
                Path: "/v1/chat/completions",
                ProviderType: "OpenAI",
                Model: "golden-openai-nested-details",
                ModelCost: StandardCost(
                    "golden-openai-nested-details",
                    inputRate: 2m,
                    outputRate: 8m,
                    cachedInputRate: 0.5m,
                    reasoningRate: 12m),
                ExpectedUsage: new Usage
                {
                    PromptTokens = 1200,
                    CompletionTokens = 300,
                    TotalTokens = 1500,
                    CachedInputTokens = 800,
                    ReasoningTokens = 200,
                    CachedInputTokensIncludedInPrompt = true
                },
                ExpectedCost: 0.0044m),

            ["anthropic-cache"] = new(
                Fixture: "anthropic-cache.json",
                Path: "/v1/chat/completions",
                ProviderType: "Anthropic",
                Model: "golden-anthropic-cache",
                ModelCost: StandardCost(
                    "golden-anthropic-cache",
                    inputRate: 3m,
                    outputRate: 15m,
                    cachedInputRate: 0.3m,
                    cachedWriteRate: 3.75m),
                ExpectedUsage: new Usage
                {
                    PromptTokens = 500,
                    CompletionTokens = 25,
                    CachedInputTokens = 10000,
                    CachedWriteTokens = 2000,
                    CachedInputTokensIncludedInPrompt = false
                },
                ExpectedCost: 0.012375m),

            ["image-completion"] = new(
                Fixture: "image-completion.json",
                Path: "/v1/images/generations",
                ProviderType: "OpenAI",
                Model: "golden-image-model",
                ModelCost: new ModelCost
                {
                    CostName = "golden-image-model",
                    PricingModel = PricingModel.PerImage,
                    PricingConfiguration = JsonSerializer.Serialize(new PerImagePricingConfig
                    {
                        BaseRate = 0.04m,
                        QualityMultipliers = new Dictionary<string, decimal> { ["hd"] = 2m },
                        ResolutionMultipliers = new Dictionary<string, decimal> { ["1792x1024"] = 1.5m }
                    })
                },
                ExpectedUsage: new Usage
                {
                    ImageCount = 2,
                    ImageQuality = "hd",
                    ImageResolution = "1792x1024"
                },
                ExpectedCost: 0.24m,
                UsageContext: new ImageUsageContext
                {
                    Model = "golden-image-model",
                    Quality = "hd",
                    Size = "1792x1024",
                    N = 2
                }),

            ["video-completion"] = new(
                Fixture: "video-completion.json",
                Path: "/v1/videos/generations",
                ProviderType: "OpenAI",
                Model: "golden-video-model",
                ModelCost: new ModelCost
                {
                    CostName = "golden-video-model",
                    PricingModel = PricingModel.PerSecondVideo,
                    PricingConfiguration = JsonSerializer.Serialize(new PerSecondVideoPricingConfig
                    {
                        BaseRate = 0.09m,
                        ResolutionMultipliers = new Dictionary<string, decimal> { ["1920x1080"] = 1.5m }
                    })
                },
                ExpectedUsage: new Usage
                {
                    VideoDurationSeconds = 8,
                    VideoResolution = "1920x1080"
                },
                ExpectedCost: 1.08m,
                UsageContext: new VideoUsageContext
                {
                    Model = "golden-video-model",
                    Duration = 12,
                    Size = "1280x720",
                    N = 1
                })
        };

    [Theory]
    [InlineData("openai-top-level")]
    [InlineData("openai-nested-details")]
    [InlineData("anthropic-cache")]
    [InlineData("image-completion")]
    [InlineData("video-completion")]
    public async Task Frozen_provider_response_extracts_expected_usage_and_cost(string caseName)
    {
        var goldenCase = Cases[caseName];
        var responseJson = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "GoldenCorpus", goldenCase.Fixture));

        var modelCosts = new Mock<IModelCostService>();
        modelCosts
            .Setup(service => service.GetCostForModelAsync(
                goldenCase.Model,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(goldenCase.ModelCost);

        var calculator = new CostCalculationService(
            modelCosts.Object,
            Mock.Of<ILogger<CostCalculationService>>());

        Usage? capturedUsage = null;
        var recordingCalculator = new Mock<ICostCalculationService>();
        recordingCalculator
            .Setup(service => service.CalculateCostAsync(
                goldenCase.Model,
                It.IsAny<Usage>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Usage, CancellationToken>((_, usage, _) => capturedUsage = usage)
            .Returns<string, Usage, CancellationToken>(calculator.CalculateCostAsync);
        recordingCalculator
            .Setup(service => service.CalculateCacheSavingsAsync(
                goldenCase.Model,
                It.IsAny<Usage>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Usage, CancellationToken>(calculator.CalculateCacheSavingsAsync);

        var queuedSpend = new Mock<IBatchSpendUpdateService>();
        queuedSpend.SetupGet(service => service.IsHealthy).Returns(true);
        queuedSpend
            .Setup(service => service.QueueSpendUpdateAsync(
                VirtualKeyId,
                It.IsAny<decimal>(),
                It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);

        LogRequestDto? requestLog = null;
        var requestLogs = new Mock<IRequestLogService>();
        requestLogs
            .Setup(service => service.LogRequestAsync(It.IsAny<LogRequestDto>()))
            .Callback<LogRequestDto>(log => requestLog = log)
            .Returns(Task.CompletedTask);

        var billingAudit = new Mock<IBillingAuditService>();
        billingAudit
            .Setup(service => service.LogBillingEventAsync(It.IsAny<BillingAuditEvent>()))
            .Returns(Task.CompletedTask);

        var toolCosts = new Mock<IToolCostCalculationService>();
        toolCosts
            .Setup(service => service.CalculateToolCostsAsync(
                It.IsAny<ToolUsageData>(),
                It.IsAny<ProviderType>()))
            .ReturnsAsync(new ToolCostResult { TotalCost = 0m });

        var context = CreateContext(goldenCase);
        var middleware = new UsageTrackingMiddleware(
            async httpContext =>
            {
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(responseJson));
            },
            Mock.Of<ILogger<UsageTrackingMiddleware>>());

        await middleware.InvokeAsync(
            context,
            recordingCalculator.Object,
            queuedSpend.Object,
            requestLogs.Object,
            Mock.Of<IVirtualKeyService>(),
            billingAudit.Object,
            toolCosts.Object);

        Assert.NotNull(capturedUsage);
        AssertUsage(goldenCase.ExpectedUsage, capturedUsage);

        Assert.NotNull(requestLog);
        Assert.Equal(goldenCase.ExpectedCost, requestLog.Cost);
        Assert.Equal(goldenCase.ExpectedUsage.PromptTokens ?? 0, requestLog.InputTokens);
        Assert.Equal(goldenCase.ExpectedUsage.CompletionTokens ?? 0, requestLog.OutputTokens);
        Assert.Equal(goldenCase.ExpectedUsage.CachedInputTokens, requestLog.CachedInputTokens);
        Assert.Equal(goldenCase.ExpectedUsage.CachedWriteTokens, requestLog.CachedWriteTokens);

        queuedSpend.Verify(service => service.QueueSpendUpdateAsync(
            VirtualKeyId,
            goldenCase.ExpectedCost,
            It.IsAny<DateTime?>()), Times.Once);
    }

    private static DefaultHttpContext CreateContext(GoldenCase goldenCase)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = goldenCase.Path;
        context.Request.Method = HttpMethods.Post;
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.Body = new MemoryStream();
        context.Items["VirtualKeyId"] = VirtualKeyId;
        context.Items["VirtualKey"] = "golden-corpus-key";
        context.Items["ProviderType"] = goldenCase.ProviderType;

        if (goldenCase.UsageContext != null)
            context.SetUsageContext(goldenCase.UsageContext);

        return context;
    }

    private static void AssertUsage(Usage expected, Usage actual)
    {
        Assert.Equal(expected.PromptTokens, actual.PromptTokens);
        Assert.Equal(expected.CompletionTokens, actual.CompletionTokens);
        Assert.Equal(expected.TotalTokens, actual.TotalTokens);
        Assert.Equal(expected.CachedInputTokens, actual.CachedInputTokens);
        Assert.Equal(expected.CachedWriteTokens, actual.CachedWriteTokens);
        Assert.Equal(expected.CachedInputTokensIncludedInPrompt, actual.CachedInputTokensIncludedInPrompt);
        Assert.Equal(expected.ReasoningTokens, actual.ReasoningTokens);
        Assert.Equal(expected.ImageCount, actual.ImageCount);
        Assert.Equal(expected.ImageQuality, actual.ImageQuality);
        Assert.Equal(expected.ImageResolution, actual.ImageResolution);
        Assert.Equal(expected.VideoDurationSeconds, actual.VideoDurationSeconds);
        Assert.Equal(expected.VideoResolution, actual.VideoResolution);
    }

    private static ModelCost StandardCost(
        string model,
        decimal inputRate,
        decimal outputRate,
        decimal? cachedInputRate = null,
        decimal? cachedWriteRate = null,
        decimal? reasoningRate = null) => new()
    {
        CostName = model,
        PricingModel = PricingModel.Standard,
        InputCostPerMillionTokens = inputRate,
        OutputCostPerMillionTokens = outputRate,
        CachedInputCostPerMillionTokens = cachedInputRate,
        CachedInputWriteCostPerMillionTokens = cachedWriteRate,
        ReasoningCostPerMillionTokens = reasoningRate
    };

    private sealed record GoldenCase(
        string Fixture,
        string Path,
        string ProviderType,
        string Model,
        ModelCost ModelCost,
        Usage ExpectedUsage,
        decimal ExpectedCost,
        IUsageContext? UsageContext = null);
}

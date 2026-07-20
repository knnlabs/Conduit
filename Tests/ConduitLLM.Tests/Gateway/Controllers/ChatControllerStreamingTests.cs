using System.Runtime.CompilerServices;
using System.Text.Json;

using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Controllers;
using ConduitLLM.Gateway.Options;
using ConduitLLM.Gateway.UsageTracking;
using ConduitLLM.Gateway.Billing;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

namespace ConduitLLM.Tests.Http.Controllers;

public class ChatControllerStreamingTests
{
    [Fact]
    public void Constructor_Requires_Usage_Estimator()
    {
        var conduit = new Conduit(Mock.Of<ILLMClientFactory>(), Mock.Of<ILogger<Conduit>>());

        Assert.Throws<ArgumentNullException>(() => new ChatController(
            conduit,
            Mock.Of<ILogger<ChatController>>(),
            Mock.Of<IModelProviderMappingService>(),
            new JsonSerializerOptions(),
            Mock.Of<IEventBus>(),
            Mock.Of<IGlobalSettingsCacheService>(),
            null!));
    }

    [Fact]
    public async Task MidStream_Exception_Estimates_Accumulated_Content_And_Reasoning()
    {
        var client = new Mock<ILLMClient>();
        client.Setup(x => x.StreamChatCompletionAsync(
                It.IsAny<ChatCompletionRequest>(), null, It.IsAny<CancellationToken>()))
            .Returns(StreamThenThrow());

        var clientFactory = new Mock<ILLMClientFactory>();
        clientFactory.Setup(x => x.GetClientAsync("test-model", It.IsAny<CancellationToken>()))
            .ReturnsAsync(client.Object);

        var conduit = new Conduit(clientFactory.Object, Mock.Of<ILogger<Conduit>>());
        var estimator = new Mock<IUsageEstimationService>();
        estimator.Setup(x => x.EstimateUsageFromStreamingResponseAsync(
                "test-model",
                It.IsAny<List<Message>>(),
                "visiblehidden",
                It.Is<CancellationToken>(ct => ct.CanBeCanceled)))
            .ReturnsAsync(new Usage { PromptTokens = 3, CompletionTokens = 5, TotalTokens = 8 });

        var controller = new ChatController(
            conduit,
            Mock.Of<ILogger<ChatController>>(),
            Mock.Of<IModelProviderMappingService>(),
            new JsonSerializerOptions(),
            Mock.Of<IEventBus>(),
            Mock.Of<IGlobalSettingsCacheService>(),
            estimator.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.Response.Body = new MemoryStream();

        var result = await controller.CreateChatCompletion(new ChatCompletionRequest
        {
            Model = "test-model",
            Stream = true,
            MaxAgenticIterations = 1,
            EnableAgenticMode = false,
            Messages = new List<Message> { new() { Role = "user", Content = "hello" } }
        });

        Assert.IsType<EmptyResult>(result);
        var snapshot = controller.HttpContext.GetRequestAccountingSnapshot();
        var providerUsage = Assert.IsType<ProviderUsageEvidence>(snapshot!.ProviderUsage);
        Assert.Equal(8, providerUsage.Usage.TotalTokens);
        Assert.Equal("test-model", providerUsage.Model);
        Assert.Equal(UsageEvidenceSource.Estimated, providerUsage.Source);
        var responseText = System.Text.Encoding.UTF8.GetString(
            ((MemoryStream)controller.HttpContext.Response.Body).ToArray());
        Assert.Contains("event: error", responseText);
        Assert.DoesNotContain("data: [DONE]", responseText);
        Assert.Equal(StreamTransportOutcome.ProviderFailed, snapshot.Transport!.Outcome);
        Assert.True(snapshot.Transport.BytesWritten > 0);
        estimator.VerifyAll();
    }

    [Fact]
    public async Task ToolCallOnly_Stream_Estimates_Serialized_Tool_Call()
    {
        var client = new Mock<ILLMClient>();
        client.Setup(x => x.StreamChatCompletionAsync(
                It.IsAny<ChatCompletionRequest>(), null, It.IsAny<CancellationToken>()))
            .Returns(ToolCallOnlyStream());

        var clientFactory = new Mock<ILLMClientFactory>();
        clientFactory.Setup(x => x.GetClientAsync("test-model", It.IsAny<CancellationToken>()))
            .ReturnsAsync(client.Object);

        var estimator = new Mock<IUsageEstimationService>();
        estimator.Setup(x => x.EstimateUsageFromStreamingResponseAsync(
                "test-model",
                It.IsAny<List<Message>>(),
                It.Is<string>(output => output.Contains("get_weather") && output.Contains("Seattle")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Usage { PromptTokens = 3, CompletionTokens = 4, TotalTokens = 7 });

        var controller = CreateController(new Conduit(clientFactory.Object, Mock.Of<ILogger<Conduit>>()), estimator.Object);
        await controller.CreateChatCompletion(CreateRequest());

        var snapshot = controller.HttpContext.GetRequestAccountingSnapshot();
        var providerUsage = Assert.IsType<ProviderUsageEvidence>(snapshot!.ProviderUsage);
        Assert.Equal(UsageEvidenceSource.Estimated, providerUsage.Source);
        Assert.Equal(7, providerUsage.Usage.TotalTokens);
        estimator.VerifyAll();
    }

    [Fact]
    public async Task ProviderFailureBeforeFirstChunk_ReturnsJsonErrorWithoutStartingSse()
    {
        var client = new Mock<ILLMClient>();
        client.Setup(x => x.StreamChatCompletionAsync(
                It.IsAny<ChatCompletionRequest>(), null, It.IsAny<CancellationToken>()))
            .Returns(ThrowBeforeFirstChunk());
        var clientFactory = new Mock<ILLMClientFactory>();
        clientFactory.Setup(x => x.GetClientAsync("test-model", It.IsAny<CancellationToken>()))
            .ReturnsAsync(client.Object);
        var estimator = new Mock<IUsageEstimationService>(MockBehavior.Strict);
        var controller = CreateController(
            new Conduit(clientFactory.Object, Mock.Of<ILogger<Conduit>>()),
            estimator.Object);

        var result = await controller.CreateChatCompletion(CreateRequest());

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, error.StatusCode);
        Assert.Null(controller.Response.ContentType);
        Assert.Equal(0, controller.Response.Body.Length);
        estimator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MissingProviderUsageBeyondAccumulatorLimit_IsMarkedIndeterminate()
    {
        var client = new Mock<ILLMClient>();
        client.Setup(x => x.StreamChatCompletionAsync(
                It.IsAny<ChatCompletionRequest>(), null, It.IsAny<CancellationToken>()))
            .Returns(ContentOnlyStream("content beyond the configured limit"));
        var clientFactory = new Mock<ILLMClientFactory>();
        clientFactory.Setup(x => x.GetClientAsync("test-model", It.IsAny<CancellationToken>()))
            .ReturnsAsync(client.Object);
        var estimator = new Mock<IUsageEstimationService>(MockBehavior.Strict);
        var controller = CreateController(
            new Conduit(clientFactory.Object, Mock.Of<ILogger<Conduit>>()),
            estimator.Object,
            Options.Create(new UsageTrackingOptions
            {
                MaximumStreamingCompletionCharacters = 5,
                MaximumStreamingToolCallCharacters = 5,
                MaximumStreamingToolCalls = 2
            }));

        var result = await controller.CreateChatCompletion(CreateRequest());

        Assert.IsType<EmptyResult>(result);
        Assert.False(controller.HttpContext.Items.ContainsKey("StreamingUsage"));
        var snapshot = controller.HttpContext.GetRequestAccountingSnapshot();
        Assert.True(snapshot!.IsIndeterminate);
        Assert.Equal(StreamTransportOutcome.AccountingIndeterminate, snapshot.Transport!.Outcome);
        Assert.True(snapshot.Transport.EvidenceTruncated);
        estimator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnforcedAdmissionRejectsInsufficientBalanceBeforeProviderInvocation()
    {
        var clientFactory = new Mock<ILLMClientFactory>(MockBehavior.Strict);
        var spendEstimator = new Mock<IChatSpendEstimator>();
        spendEstimator.Setup(x => x.EstimateMaximumCostAsync(
                It.IsAny<ChatCompletionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatSpendEstimate(true, 1m, 10, 100, 5));
        var reservationService = new Mock<ISpendReservationService>();
        reservationService.Setup(x => x.ReserveAsync(
                77,
                It.IsAny<string>(),
                1m))
            .ReturnsAsync(new SpendReservationResult(
                SpendReservationOutcome.InsufficientBalance,
                1m));
        var controller = new ChatController(
            new Conduit(clientFactory.Object, Mock.Of<ILogger<Conduit>>()),
            Mock.Of<ILogger<ChatController>>(),
            Mock.Of<IModelProviderMappingService>(),
            new JsonSerializerOptions(),
            Mock.Of<IEventBus>(),
            Mock.Of<IGlobalSettingsCacheService>(),
            Mock.Of<IUsageEstimationService>(),
            chatSpendEstimator: spendEstimator.Object,
            spendReservationService: reservationService.Object,
            billingAdmissionOptions: Options.Create(new BillingAdmissionOptions
            {
                Mode = BillingAdmissionMode.Enforce
            }))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.Items["VirtualKeyId"] = 77;
        controller.HttpContext.Response.Body = new MemoryStream();

        var result = await controller.CreateChatCompletion(CreateRequest());

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status402PaymentRequired, error.StatusCode);
        spendEstimator.VerifyAll();
        reservationService.VerifyAll();
        clientFactory.VerifyNoOtherCalls();
    }

    private static ChatController CreateController(
        Conduit conduit,
        IUsageEstimationService estimator,
        IOptions<UsageTrackingOptions>? usageOptions = null)
    {
        var controller = new ChatController(
            conduit,
            Mock.Of<ILogger<ChatController>>(),
            Mock.Of<IModelProviderMappingService>(),
            new JsonSerializerOptions(),
            Mock.Of<IEventBus>(),
            Mock.Of<IGlobalSettingsCacheService>(),
            estimator,
            usageTrackingOptions: usageOptions)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.Response.Body = new MemoryStream();
        return controller;
    }

    private static ChatCompletionRequest CreateRequest() => new()
    {
        Model = "test-model",
        Stream = true,
        MaxAgenticIterations = 1,
        EnableAgenticMode = false,
        Messages = new List<Message> { new() { Role = "user", Content = "hello" } }
    };

    private static async IAsyncEnumerable<ChatCompletionChunk> ToolCallOnlyStream()
    {
        yield return new ChatCompletionChunk
        {
            Model = "test-model",
            Choices = new List<StreamingChoice>
            {
                new()
                {
                    Index = 0,
                    Delta = new DeltaContent
                    {
                        ToolCalls = new List<ToolCallChunk>
                        {
                            new()
                            {
                                Index = 0,
                                Id = "call-1",
                                Type = "function",
                                Function = new FunctionCallChunk { Name = "get_weather", Arguments = "{\"city\":\"Seattle\"}" }
                            }
                        }
                    }
                }
            }
        };
        await Task.Yield();
    }

    private static async IAsyncEnumerable<ChatCompletionChunk> StreamThenThrow(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return new ChatCompletionChunk
        {
            Model = "test-model",
            Choices = new List<StreamingChoice>
            {
                new()
                {
                    Index = 0,
                    Delta = new DeltaContent { Content = "visible", Reasoning = "hidden" }
                }
            }
        };

        await Task.Yield();
        throw new InvalidOperationException("Provider stream failed");
    }

    private static async IAsyncEnumerable<ChatCompletionChunk> ThrowBeforeFirstChunk()
    {
        await Task.Yield();
        if (Environment.TickCount == int.MinValue)
        {
            yield return new ChatCompletionChunk();
        }

        throw new InvalidOperationException("Provider failed before streaming");
    }

    private static async IAsyncEnumerable<ChatCompletionChunk> ContentOnlyStream(string content)
    {
        yield return new ChatCompletionChunk
        {
            Model = "test-model",
            Choices =
            [
                new StreamingChoice
                {
                    Index = 0,
                    Delta = new DeltaContent { Content = content }
                }
            ]
        };
        await Task.Yield();
    }
}

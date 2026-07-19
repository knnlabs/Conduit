using System.Runtime.CompilerServices;
using System.Text.Json;

using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Http.Controllers;

public class ChatControllerStreamingTests
{
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
                It.Is<CancellationToken>(ct => !ct.CanBeCanceled)))
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
        var usage = Assert.IsType<Usage>(controller.HttpContext.Items["StreamingUsage"]);
        Assert.Equal(8, usage.TotalTokens);
        Assert.Equal("test-model", controller.HttpContext.Items["StreamingModel"]);
        Assert.Equal(true, controller.HttpContext.Items["UsageIsEstimated"]);
        estimator.VerifyAll();
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
}

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.EventHandlers;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Gateway.EventHandlers;

public sealed class MediaLifecycleHandlerTests
{
    [Fact]
    public async Task HandleAsync_MapsProviderAndModelToTheirCorrectFields()
    {
        var lifecycle = new Mock<IMediaLifecycleService>();
        MediaLifecycleMetadata? capturedMetadata = null;
        lifecycle
            .Setup(service => service.TrackMediaAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<MediaLifecycleMetadata>()))
            .Callback<int, string, string, MediaLifecycleMetadata>((_, _, _, metadata) => capturedMetadata = metadata)
            .ReturnsAsync(new MediaRecord());
        var handler = new MediaLifecycleHandler(
            lifecycle.Object,
            Mock.Of<ILogger<MediaLifecycleHandler>>());
        var message = new MediaGenerationCompleted
        {
            MediaType = MediaType.Video,
            VirtualKeyId = 42,
            StorageKey = "videos/generated.mp4",
            MediaUrl = "https://cdn.example/generated.mp4",
            GeneratedByModel = "veo-3",
            Provider = "Google"
        };

        await handler.HandleAsync(message, Mock.Of<IEventContext>());

        Assert.NotNull(capturedMetadata);
        Assert.Equal("Google", capturedMetadata.Provider);
        Assert.Equal("veo-3", capturedMetadata.Model);
    }

    [Fact]
    public async Task HandleAsync_LegacyEvent_UsesProviderMetadataFallback()
    {
        var lifecycle = new Mock<IMediaLifecycleService>();
        MediaLifecycleMetadata? capturedMetadata = null;
        lifecycle
            .Setup(service => service.TrackMediaAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<MediaLifecycleMetadata>()))
            .Callback<int, string, string, MediaLifecycleMetadata>((_, _, _, metadata) => capturedMetadata = metadata)
            .ReturnsAsync(new MediaRecord());
        var handler = new MediaLifecycleHandler(
            lifecycle.Object,
            Mock.Of<ILogger<MediaLifecycleHandler>>());
        var message = new MediaGenerationCompleted
        {
            GeneratedByModel = "imagen-4",
            Metadata = new Dictionary<string, object> { ["provider"] = "Google" }
        };

        await handler.HandleAsync(message, Mock.Of<IEventContext>());

        Assert.NotNull(capturedMetadata);
        Assert.Equal("Google", capturedMetadata.Provider);
        Assert.Equal("imagen-4", capturedMetadata.Model);
    }
}

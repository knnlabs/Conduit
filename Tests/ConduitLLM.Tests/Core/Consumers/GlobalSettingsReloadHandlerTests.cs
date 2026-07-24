using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Consumers;
using ConduitLLM.Core.Events;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Core.Consumers;

public class GlobalSettingsReloadHandlerTests
{
    [Fact]
    public async Task HandleAsync_BroadcastsRequestToProcessCaches()
    {
        var cache = new Mock<IGlobalSettingsCacheService>();
        var handler = new GlobalSettingsReloadHandler(
            cache.Object,
            Mock.Of<ILogger<GlobalSettingsReloadHandler>>());
        var message = new GlobalSettingsReloadRequested { CorrelationId = "reload-123" };

        await handler.HandleAsync(message, Mock.Of<IEventContext>());

        cache.Verify(service => service.PublishReloadAsync("reload-123"), Times.Once);
    }
}

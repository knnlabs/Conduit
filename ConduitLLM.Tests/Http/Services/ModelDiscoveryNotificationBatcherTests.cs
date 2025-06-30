using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Http.Hubs;
using ConduitLLM.Http.Services;
using ConduitLLM.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Services
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    public class ModelDiscoveryNotificationBatcherTests : TestBase
    {
        private readonly Mock<IHubContext<ModelDiscoveryHub>> _hubContextMock;
        private readonly Mock<IClientProxy> _clientProxyMock;
        private readonly Mock<ILogger<ModelDiscoveryNotificationBatcher>> _loggerMock;
        private readonly IOptions<NotificationBatchingOptions> _options;
        private ModelDiscoveryNotificationBatcher _batcher;

        public ModelDiscoveryNotificationBatcherTests(ITestOutputHelper output) : base(output)
        {
            _hubContextMock = new Mock<IHubContext<ModelDiscoveryHub>>();
            _clientProxyMock = new Mock<IClientProxy>();
            _loggerMock = CreateLogger<ModelDiscoveryNotificationBatcher>();
            
            var hubClientsMock = new Mock<IHubClients>();
            hubClientsMock.Setup(x => x.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);
            _hubContextMock.Setup(x => x.Clients).Returns(hubClientsMock.Object);

            _options = Options.Create(new NotificationBatchingOptions
            {
                EnableBatching = true,
                DefaultBatchingWindowSeconds = 1,
                MaxBatchingDelaySeconds = 2,
                MaxBatchSize = 10,
                ImmediateSeverityLevels = new List<NotificationSeverity> { NotificationSeverity.Critical }
            });

            _batcher = new ModelDiscoveryNotificationBatcher(_hubContextMock.Object, _options, _loggerMock.Object);
        }

        [Fact]
        public async Task QueueNotificationAsync_WithCriticalSeverity_SendsImmediately()
        {
            // Arrange
            var notification = new NewModelsDiscoveredNotification
            {
                Provider = "openai",
                NewModels = new List<DiscoveredModelInfo> { new() { ModelId = "gpt-4" } }
            };

            // Act
            await _batcher.QueueNotificationAsync("test-group", notification, NotificationSeverity.Critical);

            // Assert
            _clientProxyMock.Verify(x => x.SendCoreAsync(
                "NewModelsDiscovered",
                It.Is<object[]>(args => args[0] == notification),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task QueueNotificationAsync_WithBatchingDisabled_SendsImmediately()
        {
            // Arrange
            var options = Options.Create(new NotificationBatchingOptions { EnableBatching = false });
            _batcher = new ModelDiscoveryNotificationBatcher(_hubContextMock.Object, options, _loggerMock.Object);
            
            var notification = new NewModelsDiscoveredNotification
            {
                Provider = "openai",
                NewModels = new List<DiscoveredModelInfo> { new() { ModelId = "gpt-4" } }
            };

            // Act
            await _batcher.QueueNotificationAsync("test-group", notification, NotificationSeverity.Low);

            // Assert
            _clientProxyMock.Verify(x => x.SendCoreAsync(
                "NewModelsDiscovered",
                It.Is<object[]>(args => args[0] == notification),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task QueueNotificationAsync_WithBatching_QueuesNotification()
        {
            // Arrange
            await _batcher.StartAsync(CancellationToken.None);
            
            var notification = new NewModelsDiscoveredNotification
            {
                Provider = "openai",
                NewModels = new List<DiscoveredModelInfo> { new() { ModelId = "gpt-4" } }
            };

            // Act
            await _batcher.QueueNotificationAsync("test-group", notification, NotificationSeverity.Low);
            
            // Assert - Should not send immediately
            _clientProxyMock.Verify(x => x.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task FlushBatchAsync_SendsBatchedNotifications()
        {
            // Arrange
            await _batcher.StartAsync(CancellationToken.None);
            
            var notifications = new[]
            {
                new NewModelsDiscoveredNotification
                {
                    Provider = "openai",
                    NewModels = new List<DiscoveredModelInfo> { new() { ModelId = "gpt-4" } }
                },
                new NewModelsDiscoveredNotification
                {
                    Provider = "anthropic",
                    NewModels = new List<DiscoveredModelInfo> { new() { ModelId = "claude-3" } }
                }
            };

            foreach (var notification in notifications)
            {
                await _batcher.QueueNotificationAsync("test-group", notification, NotificationSeverity.Low);
            }

            // Act
            await _batcher.FlushBatchAsync("test-group");

            // Assert
            _clientProxyMock.Verify(x => x.SendCoreAsync(
                "BatchedModelDiscoveryUpdate",
                It.Is<object[]>(args => args[0] is BatchedModelDiscoveryNotification),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task QueueNotificationAsync_ExceedingMaxBatchSize_AutoFlushes()
        {
            // Arrange
            await _batcher.StartAsync(CancellationToken.None);
            
            // Queue notifications up to max batch size
            for (int i = 0; i < _options.Value.MaxBatchSize; i++)
            {
                var notification = new NewModelsDiscoveredNotification
                {
                    Provider = $"provider-{i}",
                    NewModels = new List<DiscoveredModelInfo> { new() { ModelId = $"model-{i}" } }
                };
                await _batcher.QueueNotificationAsync("test-group", notification, NotificationSeverity.Low);
            }

            // Assert - Should auto-flush when max size reached
            _clientProxyMock.Verify(x => x.SendCoreAsync(
                "BatchedModelDiscoveryUpdate",
                It.Is<object[]>(args => args[0] is BatchedModelDiscoveryNotification),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Timer_FlushesExpiredBatches()
        {
            // Arrange
            await _batcher.StartAsync(CancellationToken.None);
            
            var notification = new NewModelsDiscoveredNotification
            {
                Provider = "openai",
                NewModels = new List<DiscoveredModelInfo> { new() { ModelId = "gpt-4" } }
            };
            
            await _batcher.QueueNotificationAsync("test-group", notification, NotificationSeverity.Low);

            // Act - Wait for timer to flush (add extra time for timer precision)
            await Task.Delay(TimeSpan.FromSeconds(_options.Value.DefaultBatchingWindowSeconds + 1.5));

            // Assert
            _clientProxyMock.Verify(x => x.SendCoreAsync(
                "BatchedModelDiscoveryUpdate",
                It.Is<object[]>(args => args[0] is BatchedModelDiscoveryNotification),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task BatchedNotification_ContainsCorrectSummary()
        {
            // Arrange
            await _batcher.StartAsync(CancellationToken.None);
            
            BatchedModelDiscoveryNotification? capturedBatch = null;
            _clientProxyMock.Setup(x => x.SendCoreAsync(
                "BatchedModelDiscoveryUpdate",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
                .Callback<string, object[], CancellationToken>((method, args, ct) =>
                {
                    capturedBatch = args[0] as BatchedModelDiscoveryNotification;
                });

            // Queue different types of notifications
            await _batcher.QueueNotificationAsync("test-group", new NewModelsDiscoveredNotification
            {
                Provider = "openai",
                NewModels = new List<DiscoveredModelInfo> { new() { ModelId = "gpt-4" } }
            }, NotificationSeverity.Low);

            await _batcher.QueueNotificationAsync("test-group", new ModelCapabilitiesChangedNotification
            {
                Provider = "anthropic",
                ModelId = "claude-3",
                Changes = new List<string> { "Added vision support" }
            }, NotificationSeverity.Medium);

            await _batcher.QueueNotificationAsync("test-group", new ModelPricingUpdatedNotification
            {
                Provider = "google",
                ModelId = "gemini-pro",
                PercentageChange = 10
            }, NotificationSeverity.Low);

            // Act
            await _batcher.FlushBatchAsync("test-group");

            // Assert
            capturedBatch.Should().NotBeNull();
            capturedBatch!.Summary.TotalNotifications.Should().Be(3);
            capturedBatch.Summary.NewModelsCount.Should().Be(1);
            capturedBatch.Summary.CapabilityChangesCount.Should().Be(1);
            capturedBatch.Summary.PriceUpdatesCount.Should().Be(1);
            capturedBatch.Summary.AffectedProvidersCount.Should().Be(3);
            capturedBatch.Summary.AffectedProviders.Should().Contain(new[] { "openai", "anthropic", "google" });
        }

        [Fact]
        public async Task StopAsync_FlushesAllPendingBatches()
        {
            // Arrange
            await _batcher.StartAsync(CancellationToken.None);
            
            // Queue notifications for multiple groups
            await _batcher.QueueNotificationAsync("group1", new NewModelsDiscoveredNotification
            {
                Provider = "openai",
                NewModels = new List<DiscoveredModelInfo> { new() { ModelId = "gpt-4" } }
            }, NotificationSeverity.Low);

            await _batcher.QueueNotificationAsync("group2", new NewModelsDiscoveredNotification
            {
                Provider = "anthropic",
                NewModels = new List<DiscoveredModelInfo> { new() { ModelId = "claude-3" } }
            }, NotificationSeverity.Low);

            // Act
            await _batcher.StopAsync(CancellationToken.None);

            // Assert - Both groups should be flushed
            _clientProxyMock.Verify(x => x.SendCoreAsync(
                "BatchedModelDiscoveryUpdate",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        protected new void Dispose()
        {
            // Stop the timer before disposing to prevent timer callbacks after disposal
            _batcher?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            _batcher?.Dispose();
            base.Dispose();
        }
    }
}
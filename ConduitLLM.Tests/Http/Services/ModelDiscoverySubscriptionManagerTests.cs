using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Http.Services;
using ConduitLLM.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Services
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    public class ModelDiscoverySubscriptionManagerTests : TestBase
    {
        private readonly IMemoryCache _cache;
        private readonly Mock<ILogger<ModelDiscoverySubscriptionManager>> _loggerMock;
        private readonly ModelDiscoverySubscriptionManager _manager;

        public ModelDiscoverySubscriptionManagerTests(ITestOutputHelper output) : base(output)
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
            _loggerMock = CreateLogger<ModelDiscoverySubscriptionManager>();
            _manager = new ModelDiscoverySubscriptionManager(_cache, _loggerMock.Object);
        }

        [Fact]
        public async Task AddOrUpdateSubscriptionAsync_WithValidInput_AddsSubscription()
        {
            // Arrange
            var connectionId = "test-connection-1";
            var virtualKeyId = Guid.NewGuid();
            var filter = new ModelDiscoverySubscriptionFilter
            {
                ProviderTypes = new List<ProviderType> { ProviderType.OpenAI, ProviderType.Groq },
                Capabilities = new List<string> { "vision" },
                MinSeverityLevel = NotificationSeverity.Medium
            };

            // Act
            var result = await _manager.AddOrUpdateSubscriptionAsync(connectionId, virtualKeyId, filter);

            // Assert
            result.Should().NotBeNull();
            result.ConnectionId.Should().Be(connectionId);
            result.VirtualKeyId.Should().Be(virtualKeyId);
            result.Filter.Should().BeEquivalentTo(filter);
            result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task AddOrUpdateSubscriptionAsync_UpdatesExistingSubscription()
        {
            // Arrange
            var connectionId = "test-connection-1";
            var virtualKeyId = Guid.NewGuid();
            var initialFilter = new ModelDiscoverySubscriptionFilter { MinSeverityLevel = NotificationSeverity.Low };
            var updatedFilter = new ModelDiscoverySubscriptionFilter { MinSeverityLevel = NotificationSeverity.High };

            // Act
            var initial = await _manager.AddOrUpdateSubscriptionAsync(connectionId, virtualKeyId, initialFilter);
            await Task.Delay(100); // Ensure different timestamps
            var updated = await _manager.AddOrUpdateSubscriptionAsync(connectionId, virtualKeyId, updatedFilter);

            // Assert
            updated.Filter.MinSeverityLevel.Should().Be(NotificationSeverity.High);
            updated.CreatedAt.Should().Be(initial.CreatedAt);
            updated.LastUpdatedAt.Should().BeAfter(initial.LastUpdatedAt);
        }

        [Fact]
        public async Task GetSubscriptionAsync_ReturnsExistingSubscription()
        {
            // Arrange
            var connectionId = "test-connection-1";
            var virtualKeyId = Guid.NewGuid();
            var filter = new ModelDiscoverySubscriptionFilter();
            await _manager.AddOrUpdateSubscriptionAsync(connectionId, virtualKeyId, filter);

            // Act
            var result = await _manager.GetSubscriptionAsync(connectionId);

            // Assert
            result.Should().NotBeNull();
            result!.ConnectionId.Should().Be(connectionId);
        }

        [Fact]
        public async Task GetSubscriptionAsync_ReturnsNullForNonExistent()
        {
            // Act
            var result = await _manager.GetSubscriptionAsync("non-existent");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task RemoveSubscriptionAsync_RemovesExistingSubscription()
        {
            // Arrange
            var connectionId = "test-connection-1";
            var virtualKeyId = Guid.NewGuid();
            var filter = new ModelDiscoverySubscriptionFilter();
            await _manager.AddOrUpdateSubscriptionAsync(connectionId, virtualKeyId, filter);

            // Act
            await _manager.RemoveSubscriptionAsync(connectionId);
            var result = await _manager.GetSubscriptionAsync(connectionId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task ShouldReceiveNotificationAsync_WithMatchingProvider_ReturnsTrue()
        {
            // Arrange
            var connectionId = "test-connection-1";
            var filter = new ModelDiscoverySubscriptionFilter
            {
                ProviderTypes = new List<ProviderType> { ProviderType.OpenAI, ProviderType.Groq }
            };
            await _manager.AddOrUpdateSubscriptionAsync(connectionId, Guid.NewGuid(), filter);

            // Act
            var result = await _manager.ShouldReceiveNotificationAsync(
                connectionId, "openai", null, NotificationSeverity.Low);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ShouldReceiveNotificationAsync_WithNonMatchingProvider_ReturnsFalse()
        {
            // Arrange
            var connectionId = "test-connection-1";
            var filter = new ModelDiscoverySubscriptionFilter
            {
                ProviderTypes = new List<ProviderType> { ProviderType.OpenAI, ProviderType.Groq }
            };
            await _manager.AddOrUpdateSubscriptionAsync(connectionId, Guid.NewGuid(), filter);

            // Act
            var result = await _manager.ShouldReceiveNotificationAsync(
                connectionId, "google", null, NotificationSeverity.Low);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ShouldReceiveNotificationAsync_WithSeverityBelowMinimum_ReturnsFalse()
        {
            // Arrange
            var connectionId = "test-connection-1";
            var filter = new ModelDiscoverySubscriptionFilter
            {
                MinSeverityLevel = NotificationSeverity.High
            };
            await _manager.AddOrUpdateSubscriptionAsync(connectionId, Guid.NewGuid(), filter);

            // Act
            var result = await _manager.ShouldReceiveNotificationAsync(
                connectionId, "openai", null, NotificationSeverity.Low);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ShouldReceiveNotificationAsync_WithMatchingCapability_ReturnsTrue()
        {
            // Arrange
            var connectionId = "test-connection-1";
            var filter = new ModelDiscoverySubscriptionFilter
            {
                Capabilities = new List<string> { "vision", "embeddings" }
            };
            await _manager.AddOrUpdateSubscriptionAsync(connectionId, Guid.NewGuid(), filter);

            // Act
            var result = await _manager.ShouldReceiveNotificationAsync(
                connectionId, "openai", new List<string> { "chat", "vision" }, NotificationSeverity.Low);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ShouldReceiveNotificationAsync_WithNoMatchingCapability_ReturnsFalse()
        {
            // Arrange
            var connectionId = "test-connection-1";
            var filter = new ModelDiscoverySubscriptionFilter
            {
                Capabilities = new List<string> { "vision", "embeddings" }
            };
            await _manager.AddOrUpdateSubscriptionAsync(connectionId, Guid.NewGuid(), filter);

            // Act
            var result = await _manager.ShouldReceiveNotificationAsync(
                connectionId, "openai", new List<string> { "chat", "audio" }, NotificationSeverity.Low);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ShouldReceiveNotificationAsync_WithPriceChangeThreshold_FiltersCorrectly()
        {
            // Arrange
            var connectionId = "test-connection-1";
            var filter = new ModelDiscoverySubscriptionFilter
            {
                NotifyOnPriceChanges = true,
                MinPriceChangePercentage = 10
            };
            await _manager.AddOrUpdateSubscriptionAsync(connectionId, Guid.NewGuid(), filter);

            // Act
            var smallChange = await _manager.ShouldReceiveNotificationAsync(
                connectionId, "openai", null, NotificationSeverity.Low, priceChangePercentage: 5);
            var largeChange = await _manager.ShouldReceiveNotificationAsync(
                connectionId, "openai", null, NotificationSeverity.Low, priceChangePercentage: 15);

            // Assert
            smallChange.Should().BeFalse();
            largeChange.Should().BeTrue();
        }

        [Fact]
        public async Task GetSubscriptionStatisticsAsync_ReturnsCorrectStats()
        {
            // Arrange
            await _manager.AddOrUpdateSubscriptionAsync("conn1", Guid.NewGuid(), new ModelDiscoverySubscriptionFilter
            {
                ProviderTypes = new List<ProviderType> { ProviderType.OpenAI },
                EnableBatching = true,
                MinSeverityLevel = NotificationSeverity.High
            });
            await _manager.AddOrUpdateSubscriptionAsync("conn2", Guid.NewGuid(), new ModelDiscoverySubscriptionFilter
            {
                Capabilities = new List<string> { "vision" },
                NotifyOnPriceChanges = true,
                EnableBatching = false,  // Explicitly disable batching for this subscription
                MinSeverityLevel = NotificationSeverity.Low
            });

            // Act
            var stats = await _manager.GetSubscriptionStatisticsAsync();

            // Assert
            stats["TotalSubscriptions"].Should().Be(2);
            stats["ProvidersFiltered"].Should().Be(1);
            stats["CapabilitiesFiltered"].Should().Be(1);
            stats["BatchingEnabled"].Should().Be(1);
            stats["PriceNotificationsEnabled"].Should().Be(2); // Default is true
            stats["MinSeverity_High"].Should().Be(1);
            stats["MinSeverity_Low"].Should().Be(1);
        }

        protected new void Dispose()
        {
            _cache?.Dispose();
            base.Dispose();
        }
    }
}
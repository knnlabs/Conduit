using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.DTOs.Cache;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace ConduitLLM.Tests.Admin.Services
{
    [Trait("Category", "Unit")]
    [Trait("Component", "AdminService")]
    public class LLMCacheManagementServiceTests
    {
        private readonly Mock<ILogger<LLMCacheManagementService>> _mockLogger;
        private readonly Mock<IEventBus> _mockPublishEndpoint;
        private readonly Mock<IGlobalSettingRepository> _mockGlobalSettingRepository;
        private readonly LLMCacheManagementService _service;

        private const string LLM_CACHE_SETTING_KEY = "LLM.Caching.Enabled";

        public LLMCacheManagementServiceTests()
        {
            _mockLogger = new Mock<ILogger<LLMCacheManagementService>>();
            _mockPublishEndpoint = new Mock<IEventBus>();
            _mockGlobalSettingRepository = new Mock<IGlobalSettingRepository>();

            _service = new LLMCacheManagementService(
                _mockGlobalSettingRepository.Object,
                _mockPublishEndpoint.Object,
                _mockLogger.Object);
        }

        #region GetLLMCacheStatusAsync Tests

        [Fact]
        public async Task GetLLMCacheStatusAsync_WhenNoSettingExists_ReturnsDefaultDisabled()
        {
            // Arrange
            _mockGlobalSettingRepository
                .Setup(x => x.GetByKeyAsync(
                    LLM_CACHE_SETTING_KEY,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((GlobalSetting?)null);

            // Act
            var result = await _service.GetLLMCacheStatusAsync();

            // Assert
            result.Should().NotBeNull();
            result.Enabled.Should().BeFalse();
            result.LastChangedAt.Should().BeNull();
            result.LastChangedBy.Should().BeNull();
            result.LastChangeReason.Should().BeNull();
        }

        [Fact]
        public async Task GetLLMCacheStatusAsync_WhenSettingExists_ReturnsPersistedState()
        {
            // Arrange
            var testTime = DateTime.UtcNow;
            var metadata = new LLMCacheMetadata
            {
                Enabled = true,
                LastChangedAt = testTime,
                LastChangedBy = "test-user",
                LastChangeReason = "Testing"
            };

            var globalSetting = new GlobalSetting
            {
                Key = LLM_CACHE_SETTING_KEY,
                Value = JsonSerializer.Serialize(metadata),
                Description = "LLM cache state"
            };

            _mockGlobalSettingRepository
                .Setup(x => x.GetByKeyAsync(
                    LLM_CACHE_SETTING_KEY,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(globalSetting);

            // Act
            var result = await _service.GetLLMCacheStatusAsync();

            // Assert
            result.Should().NotBeNull();
            result.Enabled.Should().BeTrue();
            result.LastChangedAt.Should().Be(testTime);
            result.LastChangedBy.Should().Be("test-user");
            result.LastChangeReason.Should().Be("Testing");
        }

        [Fact]
        public async Task GetLLMCacheStatusAsync_WhenJsonCorrupted_ReturnsDefaultState()
        {
            // Arrange
            var globalSetting = new GlobalSetting
            {
                Key = LLM_CACHE_SETTING_KEY,
                Value = "{ invalid json }",
                Description = "LLM cache state"
            };

            _mockGlobalSettingRepository
                .Setup(x => x.GetByKeyAsync(
                    LLM_CACHE_SETTING_KEY,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(globalSetting);

            // Act
            var result = await _service.GetLLMCacheStatusAsync();

            // Assert
            result.Should().NotBeNull();
            result.Enabled.Should().BeFalse();
            result.LastChangedAt.Should().BeNull();
        }

        [Fact]
        public async Task GetLLMCacheStatusAsync_WhenDatabaseFails_ReturnsDefaultState()
        {
            // Arrange
            _mockGlobalSettingRepository
                .Setup(x => x.GetByKeyAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Database error"));

            // Act
            var result = await _service.GetLLMCacheStatusAsync();

            // Assert - Should not throw, returns default state
            result.Should().NotBeNull();
            result.Enabled.Should().BeFalse();
        }

        #endregion

        #region ToggleLLMCacheAsync Tests

        [Fact]
        public async Task ToggleLLMCacheAsync_EnablesCache_PersistsAndPublishesEvent()
        {
            // Arrange
            var changedBy = "admin@test.com";
            var reason = "Enabling for production";

            // Setup: Return a setting with ID after upsert
            _mockGlobalSettingRepository
                .Setup(x => x.GetByKeyAsync(LLM_CACHE_SETTING_KEY, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GlobalSetting { Id = 42, Key = LLM_CACHE_SETTING_KEY, Value = "{}" });

            // Act
            var result = await _service.ToggleLLMCacheAsync(true, changedBy, reason);

            // Assert
            result.Should().NotBeNull();
            result.Enabled.Should().BeTrue();
            result.LastChangedBy.Should().Be(changedBy);
            result.LastChangeReason.Should().Be(reason);
            result.LastChangedAt.Should().NotBeNull();

            // Verify database persistence
            _mockGlobalSettingRepository.Verify(
                x => x.UpsertAsync(
                    LLM_CACHE_SETTING_KEY,
                    It.Is<string>(json => json.Contains("\"Enabled\":true")),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify GlobalSettingChanged event published (for cache invalidation)
            _mockPublishEndpoint.Verify(
                x => x.PublishAsync(
                    It.Is<GlobalSettingChanged>(e =>
                        e.SettingKey == LLM_CACHE_SETTING_KEY &&
                        e.ChangeType == "Updated"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ToggleLLMCacheAsync_DisablesCache_PersistsAndPublishesEvent()
        {
            // Arrange
            var changedBy = "admin@test.com";
            var reason = "Disabling for debugging";

            // Setup: Return a setting with ID after upsert
            _mockGlobalSettingRepository
                .Setup(x => x.GetByKeyAsync(LLM_CACHE_SETTING_KEY, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GlobalSetting { Id = 42, Key = LLM_CACHE_SETTING_KEY, Value = "{}" });

            // Act
            var result = await _service.ToggleLLMCacheAsync(false, changedBy, reason);

            // Assert
            result.Should().NotBeNull();
            result.Enabled.Should().BeFalse();
            result.LastChangedBy.Should().Be(changedBy);
            result.LastChangeReason.Should().Be(reason);

            // Verify database persistence
            _mockGlobalSettingRepository.Verify(
                x => x.UpsertAsync(
                    LLM_CACHE_SETTING_KEY,
                    It.Is<string>(json => json.Contains("\"Enabled\":false")),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify GlobalSettingChanged event published
            _mockPublishEndpoint.Verify(
                x => x.PublishAsync(
                    It.Is<GlobalSettingChanged>(e => e.SettingKey == LLM_CACHE_SETTING_KEY),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ToggleLLMCacheAsync_WithNullReason_WorksCorrectly()
        {
            // Arrange
            var changedBy = "admin@test.com";

            // Setup: Return a setting with ID after upsert
            _mockGlobalSettingRepository
                .Setup(x => x.GetByKeyAsync(LLM_CACHE_SETTING_KEY, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GlobalSetting { Id = 42, Key = LLM_CACHE_SETTING_KEY, Value = "{}" });

            // Act
            var result = await _service.ToggleLLMCacheAsync(true, changedBy, null);

            // Assert
            result.Should().NotBeNull();
            result.Enabled.Should().BeTrue();
            result.LastChangeReason.Should().BeNull();

            // Verify persistence still occurred
            _mockGlobalSettingRepository.Verify(
                x => x.UpsertAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ToggleLLMCacheAsync_WhenDatabaseFails_ThrowsException()
        {
            // Arrange
            _mockGlobalSettingRepository
                .Setup(x => x.UpsertAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Database error"));

            // Act
            Func<Task> act = async () => await _service.ToggleLLMCacheAsync(true, "admin", "test");

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Database error");
        }

        [Fact]
        public async Task ToggleLLMCacheAsync_SerializesMetadataCorrectly()
        {
            // Arrange
            var changedBy = "test-user";
            var reason = "test reason";
            string? capturedJson = null;

            _mockGlobalSettingRepository
                .Setup(x => x.UpsertAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, string, string?, CancellationToken>(
                    (key, value, desc, ct) => capturedJson = value)
                .ReturnsAsync(true);

            // Setup: Return a setting with ID after upsert
            _mockGlobalSettingRepository
                .Setup(x => x.GetByKeyAsync(LLM_CACHE_SETTING_KEY, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GlobalSetting { Id = 42, Key = LLM_CACHE_SETTING_KEY, Value = "{}" });

            // Act
            await _service.ToggleLLMCacheAsync(true, changedBy, reason);

            // Assert
            capturedJson.Should().NotBeNull();
            var metadata = JsonSerializer.Deserialize<LLMCacheMetadata>(capturedJson!);
            metadata.Should().NotBeNull();
            metadata!.Enabled.Should().BeTrue();
            metadata.LastChangedBy.Should().Be(changedBy);
            metadata.LastChangeReason.Should().Be(reason);
            metadata.LastChangedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task ToggleLLMCacheAsync_LogsWarning()
        {
            // Setup: Return a setting with ID after upsert
            _mockGlobalSettingRepository
                .Setup(x => x.GetByKeyAsync(LLM_CACHE_SETTING_KEY, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GlobalSetting { Id = 42, Key = LLM_CACHE_SETTING_KEY, Value = "{}" });

            // Act
            await _service.ToggleLLMCacheAsync(true, "admin", "Test");

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Toggling LLM cache")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion
    }
}

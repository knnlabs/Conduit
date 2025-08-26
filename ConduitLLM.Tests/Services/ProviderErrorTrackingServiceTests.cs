using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Events;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Tests.Builders;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace ConduitLLM.Tests.Services
{
    public class ProviderErrorTrackingServiceTests
    {
        private readonly Mock<IConnectionMultiplexer> _redisMock;
        private readonly Mock<IDatabase> _databaseMock;
        private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
        private readonly Mock<ILogger<ProviderErrorTrackingService>> _loggerMock;
        private readonly ProviderErrorTrackingService _service;
        private readonly Mock<IProviderKeyCredentialRepository> _keyRepoMock;
        private readonly Mock<IProviderRepository> _providerRepoMock;
        private readonly Mock<IPublishEndpoint> _publishEndpointMock;

        public ProviderErrorTrackingServiceTests()
        {
            _redisMock = new Mock<IConnectionMultiplexer>();
            _databaseMock = new Mock<IDatabase>();
            _scopeFactoryMock = new Mock<IServiceScopeFactory>();
            _loggerMock = new Mock<ILogger<ProviderErrorTrackingService>>();
            _keyRepoMock = new Mock<IProviderKeyCredentialRepository>();
            _providerRepoMock = new Mock<IProviderRepository>();
            _publishEndpointMock = new Mock<IPublishEndpoint>();

            _redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_databaseMock.Object);

            SetupServiceScope();

            _service = new ProviderErrorTrackingService(
                _redisMock.Object,
                _scopeFactoryMock.Object,
                _loggerMock.Object);
        }
        
        private void SetupCommonMocks()
        {
            // Setup common mocks that are used by TrackErrorAsync
            _databaseMock.Setup(x => x.HashIncrementAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<long>(),
                CommandFlags.None))
                .ReturnsAsync(1);
            
            _databaseMock.Setup(x => x.HashSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<HashEntry[]>(),
                CommandFlags.None))
                .Returns(Task.CompletedTask);
            
            _databaseMock.Setup(x => x.HashSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<When>(),
                CommandFlags.None))
                .ReturnsAsync(true);
            
            _databaseMock.Setup(x => x.SortedSetAddAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<double>(),
                It.IsAny<SortedSetWhen>(),
                CommandFlags.None))
                .ReturnsAsync(true);
            
            _databaseMock.Setup(x => x.SortedSetRemoveRangeByRankAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                CommandFlags.None))
                .ReturnsAsync(0);
            
            _databaseMock.Setup(x => x.KeyExpireAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<ExpireWhen>(),
                CommandFlags.None))
                .ReturnsAsync(true);
        }

        private void SetupServiceScope()
        {
            var scopeMock = new Mock<IServiceScope>();
            var serviceProviderMock = new Mock<IServiceProvider>();

            serviceProviderMock.Setup(x => x.GetService(typeof(IProviderKeyCredentialRepository)))
                .Returns(_keyRepoMock.Object);
            serviceProviderMock.Setup(x => x.GetService(typeof(IProviderRepository)))
                .Returns(_providerRepoMock.Object);
            serviceProviderMock.Setup(x => x.GetService(typeof(IPublishEndpoint)))
                .Returns(_publishEndpointMock.Object);

            scopeMock.Setup(x => x.ServiceProvider)
                .Returns(serviceProviderMock.Object);

            _scopeFactoryMock.Setup(x => x.CreateScope())
                .Returns(scopeMock.Object);
        }

        [Fact]
        public async Task TrackErrorAsync_FatalError_IncrementsCounter()
        {
            // Arrange
            var error = new ProviderErrorInfoBuilder()
                .WithKeyCredentialId(123)
                .WithProviderId(456)
                .WithFatalError(ProviderErrorType.InvalidApiKey)
                .Build();

            var keyPrefix = $"provider:errors:key:{error.KeyCredentialId}:fatal";
            
            _databaseMock.Setup(x => x.HashIncrementAsync(
                keyPrefix, "count", 1, CommandFlags.None))
                .ReturnsAsync(1);
            
            _databaseMock.Setup(x => x.HashSetAsync(
                It.IsAny<RedisKey>(), 
                It.IsAny<HashEntry[]>(), 
                CommandFlags.None))
                .Returns(Task.CompletedTask);

            _databaseMock.Setup(x => x.SortedSetAddAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<double>(),
                It.IsAny<SortedSetWhen>(),
                CommandFlags.None))
                .ReturnsAsync(true);
            
            _databaseMock.Setup(x => x.SortedSetRemoveRangeByRankAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                CommandFlags.None))
                .ReturnsAsync(0);
            
            _databaseMock.Setup(x => x.HashIncrementAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<long>(),
                CommandFlags.None))
                .ReturnsAsync(1);
            
            _databaseMock.Setup(x => x.HashSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<When>(),
                CommandFlags.None))
                .ReturnsAsync(true);

            // Act
            await _service.TrackErrorAsync(error);

            // Assert
            _databaseMock.Verify(x => x.HashIncrementAsync(
                keyPrefix, "count", 1, CommandFlags.None), 
                Times.Once);
            
            _databaseMock.Verify(x => x.HashSetAsync(
                keyPrefix,
                It.Is<HashEntry[]>(entries => 
                    entries.Any(e => e.Name == "error_type" && e.Value == "InvalidApiKey") &&
                    entries.Any(e => e.Name == "last_error_message" && e.Value == error.ErrorMessage)),
                CommandFlags.None), 
                Times.Once);
        }

        [Fact]
        public async Task TrackErrorAsync_ThresholdExceeded_DisablesKey()
        {
            // Arrange
            var error = new ProviderErrorInfoBuilder()
                .WithKeyCredentialId(123)
                .WithProviderId(456)
                .WithFatalError(ProviderErrorType.InvalidApiKey) // Immediate disable
                .Build();

            var fatalKey = $"provider:errors:key:{error.KeyCredentialId}:fatal";
            
            // Setup all necessary mocks for the full flow
            SetupCommonMocks();
            
            // Setup Redis to return values that trigger disable
            _databaseMock.Setup(x => x.HashGetAsync(fatalKey, "error_type", CommandFlags.None))
                .ReturnsAsync("InvalidApiKey");
            _databaseMock.Setup(x => x.HashGetAsync(fatalKey, "count", CommandFlags.None))
                .ReturnsAsync(1);
            _databaseMock.Setup(x => x.HashGetAsync(fatalKey, "last_seen", CommandFlags.None))
                .ReturnsAsync(DateTime.UtcNow.ToString("O"));

            var testKey = new ProviderKeyCredential
            {
                Id = error.KeyCredentialId,
                ProviderId = error.ProviderId,
                IsEnabled = true,
                IsPrimary = false,
                KeyName = "Test Key"
            };

            _keyRepoMock.Setup(x => x.GetByIdAsync(error.KeyCredentialId))
                .ReturnsAsync(testKey);
            _keyRepoMock.Setup(x => x.UpdateAsync(It.IsAny<ProviderKeyCredential>()))
                .ReturnsAsync(true);

            // Act
            await _service.TrackErrorAsync(error);

            // Assert
            _keyRepoMock.Verify(x => x.UpdateAsync(
                It.Is<ProviderKeyCredential>(k => k.Id == error.KeyCredentialId && !k.IsEnabled)), 
                Times.Once);
            
            _publishEndpointMock.Verify(x => x.Publish(
                It.Is<ProviderKeyDisabledEvent>(e => 
                    e.KeyId == error.KeyCredentialId &&
                    e.ProviderId == error.ProviderId),
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task TrackErrorAsync_Warning_DoesNotDisableKey()
        {
            // Arrange
            var error = new ProviderErrorInfoBuilder()
                .WithKeyCredentialId(123)
                .WithProviderId(456)
                .WithWarning(ProviderErrorType.RateLimitExceeded)
                .Build();

            var warningKey = $"provider:errors:key:{error.KeyCredentialId}:warnings";
            
            // Setup all common mocks
            SetupCommonMocks();

            // Act
            await _service.TrackErrorAsync(error);

            // Assert
            // Verify warning was added to sorted set
            _databaseMock.Verify(x => x.SortedSetAddAsync(
                warningKey,
                It.IsAny<RedisValue>(),
                It.IsAny<double>(),
                It.IsAny<SortedSetWhen>(),
                CommandFlags.None), 
                Times.Once);
            
            // Should not attempt to disable key
            _keyRepoMock.Verify(x => x.UpdateAsync(It.IsAny<ProviderKeyCredential>()), Times.Never);
        }

        [Fact]
        public async Task TrackErrorAsync_MultipleWarnings_MaintainsLimit()
        {
            // Arrange
            var error = new ProviderErrorInfoBuilder()
                .WithKeyCredentialId(123)
                .WithProviderId(456)
                .WithWarning(ProviderErrorType.RateLimitExceeded)
                .Build();

            var warningKey = $"provider:errors:key:{error.KeyCredentialId}:warnings";
            
            // Setup all common mocks
            SetupCommonMocks();

            // Act
            await _service.TrackErrorAsync(error);

            // Assert
            // Should trim old warnings (keep last 100)
            _databaseMock.Verify(x => x.SortedSetRemoveRangeByRankAsync(
                warningKey, 0, -101, CommandFlags.None), 
                Times.Once);
            
            // Should set TTL - verify it was called with the warning key
            _databaseMock.Verify(x => x.KeyExpireAsync(
                warningKey, 
                It.IsAny<TimeSpan?>(), 
                It.IsAny<ExpireWhen>(), 
                CommandFlags.None), 
                Times.Once);
        }

        [Fact]
        public async Task ClearErrorsForKeyAsync_RemovesRedisData()
        {
            // Arrange
            var keyId = 123;
            var expectedKeys = new RedisKey[]
            {
                $"provider:errors:key:{keyId}:fatal",
                $"provider:errors:key:{keyId}:warnings"
            };

            _databaseMock.Setup(x => x.KeyDeleteAsync(
                It.IsAny<RedisKey[]>(), CommandFlags.None))
                .ReturnsAsync(2);

            // Act
            await _service.ClearErrorsForKeyAsync(keyId);

            // Assert
            _databaseMock.Verify(x => x.KeyDeleteAsync(
                It.Is<RedisKey[]>(keys => 
                    keys.Length == 2 &&
                    keys[0] == expectedKeys[0] &&
                    keys[1] == expectedKeys[1]),
                CommandFlags.None), 
                Times.Once);
        }

        [Fact]
        public async Task GetRecentErrorsAsync_ReturnsFilteredResults()
        {
            // Arrange
            var providerId = 456;
            
            var errorData1 = JsonSerializer.Serialize(new
            {
                keyId = 123,
                providerId = 456,
                type = "InvalidApiKey",
                message = "Invalid API key",
                timestamp = DateTime.UtcNow.AddMinutes(-5)
            });
            
            var errorData2 = JsonSerializer.Serialize(new
            {
                keyId = 124,
                providerId = 456,
                type = "RateLimitExceeded",
                message = "Rate limit exceeded",
                timestamp = DateTime.UtcNow.AddMinutes(-2)
            });
            
            var errorData3 = JsonSerializer.Serialize(new
            {
                keyId = 125,
                providerId = 457, // Different provider
                type = "ServiceUnavailable",
                message = "Service unavailable",
                timestamp = DateTime.UtcNow.AddMinutes(-1)
            });

            _databaseMock.Setup(x => x.SortedSetRangeByScoreAsync(
                "provider:errors:recent",
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<Exclude>(),
                Order.Descending,
                It.IsAny<long>(),
                100,
                CommandFlags.None))
                .ReturnsAsync(new RedisValue[] { errorData1, errorData2, errorData3 });

            // Act
            var errors = await _service.GetRecentErrorsAsync(providerId: providerId);

            // Assert
            errors.Should().HaveCount(2);
            errors.Should().AllSatisfy(e => e.ProviderId.Should().Be(providerId));
            errors.Should().Contain(e => e.KeyCredentialId == 123 && e.ErrorType == ProviderErrorType.InvalidApiKey);
            errors.Should().Contain(e => e.KeyCredentialId == 124 && e.ErrorType == ProviderErrorType.RateLimitExceeded);
        }

        [Fact]
        public async Task ShouldDisableKeyAsync_InvalidApiKey_ReturnsTrue()
        {
            // Arrange
            var keyId = 123;
            var errorType = ProviderErrorType.InvalidApiKey;
            
            // InvalidApiKey has immediate disable policy
            // Act
            var result = await _service.ShouldDisableKeyAsync(keyId, errorType);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ShouldDisableKeyAsync_InsufficientBalance_ChecksThreshold()
        {
            // Arrange
            var keyId = 123;
            var errorType = ProviderErrorType.InsufficientBalance;
            var fatalKey = $"provider:errors:key:{keyId}:fatal";
            var lastSeenTime = DateTime.UtcNow.AddMinutes(-2); // Within 5 minute window
            
            // Return RedisValue with proper values that HasValue will be true
            _databaseMock.Setup(x => x.HashGetAsync(fatalKey, "error_type", CommandFlags.None))
                .ReturnsAsync((RedisValue)"InsufficientBalance");
            _databaseMock.Setup(x => x.HashGetAsync(fatalKey, "count", CommandFlags.None))
                .ReturnsAsync((RedisValue)2); // Threshold is 2
            _databaseMock.Setup(x => x.HashGetAsync(fatalKey, "last_seen", CommandFlags.None))
                .ReturnsAsync((RedisValue)lastSeenTime.ToString("O"));

            // Act
            var result = await _service.ShouldDisableKeyAsync(keyId, errorType);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ShouldDisableKeyAsync_BelowThreshold_ReturnsFalse()
        {
            // Arrange
            var keyId = 123;
            var errorType = ProviderErrorType.InsufficientBalance;
            var fatalKey = $"provider:errors:key:{keyId}:fatal";
            
            _databaseMock.Setup(x => x.HashGetAsync(fatalKey, "error_type", CommandFlags.None))
                .ReturnsAsync("InsufficientBalance");
            _databaseMock.Setup(x => x.HashGetAsync(fatalKey, "count", CommandFlags.None))
                .ReturnsAsync(1); // Below threshold of 2
            _databaseMock.Setup(x => x.HashGetAsync(fatalKey, "last_seen", CommandFlags.None))
                .ReturnsAsync(DateTime.UtcNow.AddMinutes(-2).ToString("O"));

            // Act
            var result = await _service.ShouldDisableKeyAsync(keyId, errorType);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task DisableKeyAsync_PrimaryKey_DisablesProvider()
        {
            // Arrange
            var keyId = 123;
            var providerId = 456;
            var reason = "Test disable reason";
            
            var primaryKey = new ProviderKeyCredential
            {
                Id = keyId,
                ProviderId = providerId,
                IsEnabled = true,
                IsPrimary = true
            };
            
            var provider = new Provider
            {
                Id = providerId,
                ProviderName = "Test Provider",
                IsEnabled = true
            };

            _keyRepoMock.Setup(x => x.GetByIdAsync(keyId))
                .ReturnsAsync(primaryKey);
            _providerRepoMock.Setup(x => x.GetByIdAsync(providerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(provider);

            // Act
            await _service.DisableKeyAsync(keyId, reason);

            // Assert
            _providerRepoMock.Verify(x => x.UpdateAsync(
                It.Is<Provider>(p => p.Id == providerId && !p.IsEnabled),
                It.IsAny<CancellationToken>()), 
                Times.Once);
            
            _publishEndpointMock.Verify(x => x.Publish(
                It.Is<ProviderKeyDisabledEvent>(e => 
                    e.KeyId == keyId &&
                    e.Reason.Contains("Provider disabled")),
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task DisableKeyAsync_SecondaryKey_DisablesKeyOnly()
        {
            // Arrange
            var keyId = 123;
            var providerId = 456;
            var reason = "Test disable reason";
            
            var secondaryKey = new ProviderKeyCredential
            {
                Id = keyId,
                ProviderId = providerId,
                IsEnabled = true,
                IsPrimary = false
            };
            
            var otherKey = new ProviderKeyCredential
            {
                Id = 124,
                ProviderId = providerId,
                IsEnabled = true,
                IsPrimary = true
            };

            _keyRepoMock.Setup(x => x.GetByIdAsync(keyId))
                .ReturnsAsync(secondaryKey);
            _keyRepoMock.Setup(x => x.GetByProviderIdAsync(providerId))
                .ReturnsAsync(new List<ProviderKeyCredential> { secondaryKey, otherKey });

            // Act
            await _service.DisableKeyAsync(keyId, reason);

            // Assert
            _keyRepoMock.Verify(x => x.UpdateAsync(
                It.Is<ProviderKeyCredential>(k => k.Id == keyId && !k.IsEnabled)), 
                Times.Once);
            
            // Should not disable provider (other key is still enabled)
            _providerRepoMock.Verify(x => x.UpdateAsync(It.IsAny<Provider>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DisableKeyAsync_AllKeysDisabled_DisablesProvider()
        {
            // Arrange
            var keyId = 123;
            var providerId = 456;
            var reason = "Test disable reason";
            
            var key1 = new ProviderKeyCredential
            {
                Id = keyId,
                ProviderId = providerId,
                IsEnabled = true,
                IsPrimary = false
            };
            
            var key2 = new ProviderKeyCredential
            {
                Id = 124,
                ProviderId = providerId,
                IsEnabled = false, // Already disabled
                IsPrimary = true
            };
            
            var provider = new Provider
            {
                Id = providerId,
                ProviderName = "Test Provider",
                IsEnabled = true
            };

            _keyRepoMock.Setup(x => x.GetByIdAsync(keyId))
                .ReturnsAsync(key1);
            _keyRepoMock.Setup(x => x.GetByProviderIdAsync(providerId))
                .ReturnsAsync(new List<ProviderKeyCredential> { key1, key2 });
            _providerRepoMock.Setup(x => x.GetByIdAsync(providerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(provider);

            // Act
            await _service.DisableKeyAsync(keyId, reason);

            // Assert
            // Should disable the provider since all keys are now disabled
            _providerRepoMock.Verify(x => x.UpdateAsync(
                It.Is<Provider>(p => p.Id == providerId && !p.IsEnabled),
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task GetKeyErrorDetailsAsync_ReturnsCorrectDetails()
        {
            // Arrange
            var keyId = 123;
            var key = new ProviderKeyCredential
            {
                Id = keyId,
                KeyName = "Test Key",
                IsEnabled = false
            };
            
            _keyRepoMock.Setup(x => x.GetByIdAsync(keyId))
                .ReturnsAsync(key);
            
            var fatalData = new HashEntry[]
            {
                new HashEntry("error_type", "InvalidApiKey"),
                new HashEntry("count", "3"),
                new HashEntry("first_seen", DateTime.UtcNow.AddHours(-2).ToString("O")),
                new HashEntry("last_seen", DateTime.UtcNow.AddMinutes(-5).ToString("O")),
                new HashEntry("last_error_message", "Invalid API key"),
                new HashEntry("last_status_code", "401"),
                new HashEntry("disabled_at", DateTime.UtcNow.AddMinutes(-5).ToString("O"))
            };
            
            _databaseMock.Setup(x => x.HashGetAllAsync(
                $"provider:errors:key:{keyId}:fatal", CommandFlags.None))
                .ReturnsAsync(fatalData);

            // Act
            var details = await _service.GetKeyErrorDetailsAsync(keyId);

            // Assert
            details.Should().NotBeNull();
            details!.KeyId.Should().Be(keyId);
            details.KeyName.Should().Be("Test Key");
            details.IsDisabled.Should().BeTrue();
            details.DisabledAt.Should().NotBeNull();
            details.FatalError.Should().NotBeNull();
            details.FatalError!.ErrorType.Should().Be(ProviderErrorType.InvalidApiKey);
            details.FatalError.Count.Should().Be(3);
            details.FatalError.LastStatusCode.Should().Be(401);
        }

        [Fact]
        public async Task GetProviderSummaryAsync_ReturnsCorrectSummary()
        {
            // Arrange
            var providerId = 456;
            var summaryData = new HashEntry[]
            {
                new HashEntry("total_errors", "50"),
                new HashEntry("fatal_errors", "10"),
                new HashEntry("warnings", "40"),
                new HashEntry("disabled_keys", "[123,124]"),
                new HashEntry("last_error", DateTime.UtcNow.AddMinutes(-5).ToString("O"))
            };
            
            _databaseMock.Setup(x => x.HashGetAllAsync(
                $"provider:errors:provider:{providerId}:summary", CommandFlags.None))
                .ReturnsAsync(summaryData);

            // Act
            var summary = await _service.GetProviderSummaryAsync(providerId);

            // Assert
            summary.Should().NotBeNull();
            summary!.ProviderId.Should().Be(providerId);
            summary.TotalErrors.Should().Be(50);
            summary.FatalErrors.Should().Be(10);
            summary.Warnings.Should().Be(40);
            summary.DisabledKeyIds.Should().BeEquivalentTo(new[] { 123, 124 });
            summary.LastError.Should().NotBeNull();
        }

        [Fact]
        public async Task GetErrorStatisticsAsync_CalculatesCorrectStats()
        {
            // Arrange
            var window = TimeSpan.FromHours(1);
            var cutoff = DateTime.UtcNow - window;
            
            var errorEntries = new[]
            {
                JsonSerializer.Serialize(new { type = "InvalidApiKey", timestamp = DateTime.UtcNow.AddMinutes(-30) }),
                JsonSerializer.Serialize(new { type = "InvalidApiKey", timestamp = DateTime.UtcNow.AddMinutes(-20) }),
                JsonSerializer.Serialize(new { type = "RateLimitExceeded", timestamp = DateTime.UtcNow.AddMinutes(-10) }),
                JsonSerializer.Serialize(new { type = "ServiceUnavailable", timestamp = DateTime.UtcNow.AddMinutes(-5) })
            };
            
            _databaseMock.Setup(x => x.SortedSetRangeByScoreAsync(
                "provider:errors:recent",
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<Exclude>(),
                It.IsAny<Order>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                CommandFlags.None))
                .ReturnsAsync(errorEntries.Select(e => (RedisValue)e).ToArray());
            
            var allKeys = new[]
            {
                new ProviderKeyCredential { Id = 1, IsEnabled = true },
                new ProviderKeyCredential { Id = 2, IsEnabled = false },
                new ProviderKeyCredential { Id = 3, IsEnabled = false }
            };
            
            _keyRepoMock.Setup(x => x.GetAllAsync())
                .ReturnsAsync(allKeys.ToList());

            // Act
            var stats = await _service.GetErrorStatisticsAsync(window);

            // Assert
            stats.TotalErrors.Should().Be(4);
            stats.FatalErrors.Should().Be(2); // 2x InvalidApiKey
            stats.Warnings.Should().Be(2); // RateLimitExceeded + ServiceUnavailable
            stats.DisabledKeys.Should().Be(2);
            stats.ErrorsByType.Should().ContainKey("InvalidApiKey").WhoseValue.Should().Be(2);
            stats.ErrorsByType.Should().ContainKey("RateLimitExceeded").WhoseValue.Should().Be(1);
            stats.ErrorsByType.Should().ContainKey("ServiceUnavailable").WhoseValue.Should().Be(1);
        }

        [Fact]
        public async Task TrackErrorAsync_HandlesExceptionGracefully()
        {
            // Arrange
            var error = new ProviderErrorInfoBuilder()
                .WithFatalError(ProviderErrorType.InvalidApiKey)
                .Build();
            
            _databaseMock.Setup(x => x.HashIncrementAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<long>(), CommandFlags.None))
                .ThrowsAsync(new Exception("Redis error"));

            // Act & Assert
            // Should not throw
            await _service.TrackErrorAsync(error);
            
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}
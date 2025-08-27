using System;
using System.Collections.Generic;
using System.Linq;
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
using Xunit;

namespace ConduitLLM.Tests.Services
{
    public class ProviderErrorTrackingServiceTests
    {
        private readonly Mock<IRedisErrorStore> _errorStoreMock;
        private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
        private readonly Mock<ILogger<ProviderErrorTrackingService>> _loggerMock;
        private readonly ProviderErrorTrackingService _service;
        private readonly Mock<IProviderKeyCredentialRepository> _keyRepoMock;
        private readonly Mock<IProviderRepository> _providerRepoMock;
        private readonly Mock<IPublishEndpoint> _publishEndpointMock;

        public ProviderErrorTrackingServiceTests()
        {
            _errorStoreMock = new Mock<IRedisErrorStore>();
            _scopeFactoryMock = new Mock<IServiceScopeFactory>();
            _loggerMock = new Mock<ILogger<ProviderErrorTrackingService>>();
            _keyRepoMock = new Mock<IProviderKeyCredentialRepository>();
            _providerRepoMock = new Mock<IProviderRepository>();
            _publishEndpointMock = new Mock<IPublishEndpoint>();

            SetupServiceScope();

            _service = new ProviderErrorTrackingService(
                _errorStoreMock.Object,
                _scopeFactoryMock.Object,
                _loggerMock.Object);
        }

        private void SetupServiceScope()
        {
            var scopeMock = new Mock<IServiceScope>();
            var serviceProviderMock = new Mock<IServiceProvider>();

            serviceProviderMock.Setup(x => x.GetService(typeof(IProviderKeyCredentialRepository)))
                .Returns(_keyRepoMock.Object);
            serviceProviderMock.Setup(x => x.GetService(typeof(IProviderRepository)))
                .Returns(_providerRepoMock.Object);
            serviceProviderMock.Setup(x => x.GetService(typeof(MassTransit.IPublishEndpoint)))
                .Returns(_publishEndpointMock.Object);

            scopeMock.Setup(x => x.ServiceProvider)
                .Returns(serviceProviderMock.Object);

            _scopeFactoryMock.Setup(x => x.CreateScope())
                .Returns(scopeMock.Object);
        }

        [Fact]
        public async Task TrackErrorAsync_FatalError_CallsErrorStore()
        {
            // Arrange
            var error = new ProviderErrorInfoBuilder()
                .WithKeyCredentialId(123)
                .WithProviderId(456)
                .WithFatalError(ProviderErrorType.InvalidApiKey)
                .Build();

            // Act
            await _service.TrackErrorAsync(error);

            // Assert
            _errorStoreMock.Verify(x => x.TrackFatalErrorAsync(
                error.KeyCredentialId, error), 
                Times.Once);
            
            _errorStoreMock.Verify(x => x.UpdateProviderSummaryAsync(
                error.ProviderId, true), 
                Times.Once);
            
            _errorStoreMock.Verify(x => x.AddToGlobalFeedAsync(error), 
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
            _keyRepoMock.Setup(x => x.GetByProviderIdAsync(error.ProviderId))
                .ReturnsAsync(new List<ProviderKeyCredential> { testKey });

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

            // Act
            await _service.TrackErrorAsync(error);

            // Assert
            _errorStoreMock.Verify(x => x.TrackWarningAsync(
                error.KeyCredentialId, error), 
                Times.Once);
            
            _errorStoreMock.Verify(x => x.UpdateProviderSummaryAsync(
                error.ProviderId, false), 
                Times.Once);
            
            // Should not attempt to disable key
            _keyRepoMock.Verify(x => x.UpdateAsync(It.IsAny<ProviderKeyCredential>()), Times.Never);
        }

        [Fact]
        public async Task TrackErrorAsync_CallsAllRequiredMethods()
        {
            // Arrange
            var error = new ProviderErrorInfoBuilder()
                .WithKeyCredentialId(123)
                .WithProviderId(456)
                .WithWarning(ProviderErrorType.RateLimitExceeded)
                .Build();

            // Act
            await _service.TrackErrorAsync(error);

            // Assert
            // Verify all required methods are called
            _errorStoreMock.Verify(x => x.TrackWarningAsync(
                error.KeyCredentialId, error), 
                Times.Once);
            
            _errorStoreMock.Verify(x => x.UpdateProviderSummaryAsync(
                error.ProviderId, false), 
                Times.Once);
            
            _errorStoreMock.Verify(x => x.AddToGlobalFeedAsync(error), 
                Times.Once);
        }

        [Fact]
        public async Task ClearErrorsForKeyAsync_CallsErrorStore()
        {
            // Arrange
            var keyId = 123;

            // Act
            await _service.ClearErrorsForKeyAsync(keyId);

            // Assert
            _errorStoreMock.Verify(x => x.ClearErrorsForKeyAsync(keyId), 
                Times.Once);
        }

        [Fact]
        public async Task GetRecentErrorsAsync_ReturnsFilteredResults()
        {
            // Arrange
            var providerId = 456;
            
            var feedEntries = new List<ErrorFeedEntry>
            {
                new ErrorFeedEntry
                {
                    KeyId = 123,
                    ProviderId = 456,
                    ErrorType = "InvalidApiKey",
                    Message = "Invalid API key",
                    Timestamp = DateTime.UtcNow.AddMinutes(-5)
                },
                new ErrorFeedEntry
                {
                    KeyId = 124,
                    ProviderId = 456,
                    ErrorType = "RateLimitExceeded",
                    Message = "Rate limit exceeded",
                    Timestamp = DateTime.UtcNow.AddMinutes(-2)
                },
                new ErrorFeedEntry
                {
                    KeyId = 125,
                    ProviderId = 457, // Different provider
                    ErrorType = "ServiceUnavailable",
                    Message = "Service unavailable",
                    Timestamp = DateTime.UtcNow.AddMinutes(-1)
                }
            };

            _errorStoreMock.Setup(x => x.GetRecentErrorsAsync(100))
                .ReturnsAsync(feedEntries);

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
            var lastSeenTime = DateTime.UtcNow.AddMinutes(-2); // Within 5 minute window
            
            var fatalData = new FatalErrorData
            {
                ErrorType = "InsufficientBalance",
                Count = 2, // Threshold is 2
                LastSeen = lastSeenTime
            };
            
            _errorStoreMock.Setup(x => x.GetFatalErrorDataAsync(keyId))
                .ReturnsAsync(fatalData);

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
            
            var fatalData = new FatalErrorData
            {
                ErrorType = "InsufficientBalance",
                Count = 1, // Below threshold of 2
                LastSeen = DateTime.UtcNow.AddMinutes(-2)
            };
            
            _errorStoreMock.Setup(x => x.GetFatalErrorDataAsync(keyId))
                .ReturnsAsync(fatalData);

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
            
            var errorData = new KeyErrorData
            {
                FatalError = new FatalErrorData
                {
                    ErrorType = "InvalidApiKey",
                    Count = 3,
                    FirstSeen = DateTime.UtcNow.AddHours(-2),
                    LastSeen = DateTime.UtcNow.AddMinutes(-5),
                    LastErrorMessage = "Invalid API key",
                    LastStatusCode = 401,
                    DisabledAt = DateTime.UtcNow.AddMinutes(-5)
                }
            };
            
            _errorStoreMock.Setup(x => x.GetKeyErrorDataAsync(keyId))
                .ReturnsAsync(errorData);

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
            var summaryData = new ProviderSummaryData
            {
                TotalErrors = 50,
                FatalErrors = 10,
                Warnings = 40,
                DisabledKeyIds = new List<int> { 123, 124 },
                LastError = DateTime.UtcNow.AddMinutes(-5)
            };
            
            _errorStoreMock.Setup(x => x.GetProviderSummaryAsync(providerId))
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
            
            var statsData = new ErrorStatsData
            {
                TotalErrors = 4,
                FatalErrors = 2,
                Warnings = 2,
                ErrorsByType = new Dictionary<string, int>
                {
                    ["InvalidApiKey"] = 2,
                    ["RateLimitExceeded"] = 1,
                    ["ServiceUnavailable"] = 1
                }
            };
            
            _errorStoreMock.Setup(x => x.GetErrorStatisticsAsync(window))
                .ReturnsAsync(statsData);
            
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
            
            _errorStoreMock.Setup(x => x.TrackFatalErrorAsync(
                It.IsAny<int>(), It.IsAny<ProviderErrorInfo>()))
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
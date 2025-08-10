using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Services;
using MassTransit;

namespace ConduitLLM.Tests.Http.Services
{
    /// <summary>
    /// Unit tests for CachedApiVirtualKeyService focusing on balance-aware authentication
    /// </summary>
    public class CachedApiVirtualKeyServiceTests : TestBase
    {
        private readonly Mock<IVirtualKeyRepository> _virtualKeyRepositoryMock;
        private readonly Mock<IVirtualKeySpendHistoryRepository> _spendHistoryRepositoryMock;
        private readonly Mock<IVirtualKeyGroupRepository> _groupRepositoryMock;
        private readonly Mock<IVirtualKeyCache> _cacheMock;
        private readonly Mock<IPublishEndpoint> _publishEndpointMock;
        private readonly Mock<ILogger<CachedApiVirtualKeyService>> _loggerMock;
        private readonly CachedApiVirtualKeyService _service;

        public CachedApiVirtualKeyServiceTests(ITestOutputHelper output) : base(output)
        {
            _virtualKeyRepositoryMock = new Mock<IVirtualKeyRepository>();
            _spendHistoryRepositoryMock = new Mock<IVirtualKeySpendHistoryRepository>();
            _groupRepositoryMock = new Mock<IVirtualKeyGroupRepository>();
            _cacheMock = new Mock<IVirtualKeyCache>();
            _publishEndpointMock = new Mock<IPublishEndpoint>();
            _loggerMock = CreateLogger<CachedApiVirtualKeyService>();

            _service = new CachedApiVirtualKeyService(
                _virtualKeyRepositoryMock.Object,
                _spendHistoryRepositoryMock.Object,
                _groupRepositoryMock.Object,
                _cacheMock.Object,
                _publishEndpointMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task ValidateVirtualKeyForAuthenticationAsync_WithValidKey_ReturnsVirtualKey()
        {
            // Arrange
            var keyValue = "condt_test123";
            var keyHash = ComputeExpectedHash(keyValue);
            var virtualKey = CreateEnabledVirtualKey();

            _cacheMock.Setup(c => c.GetVirtualKeyAsync(keyHash, It.IsAny<Func<string, Task<VirtualKey>>>()))
                .ReturnsAsync(virtualKey);

            // Act
            var result = await _service.ValidateVirtualKeyForAuthenticationAsync(keyValue);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(virtualKey.Id, result.Id);
            Assert.Equal(virtualKey.KeyName, result.KeyName);
            
            _cacheMock.Verify(c => c.GetVirtualKeyAsync(keyHash, It.IsAny<Func<string, Task<VirtualKey>>>()), Times.Once);
            
            // Verify that group balance is NOT checked for authentication
            _groupRepositoryMock.Verify(g => g.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ValidateVirtualKeyForAuthenticationAsync_WithDisabledKey_ReturnsNull()
        {
            // Arrange
            var keyValue = "condt_disabled";
            var keyHash = ComputeExpectedHash(keyValue);
            var virtualKey = CreateDisabledVirtualKey();

            _cacheMock.Setup(c => c.GetVirtualKeyAsync(keyHash, It.IsAny<Func<string, Task<VirtualKey>>>()))
                .ReturnsAsync(virtualKey);

            // Act
            var result = await _service.ValidateVirtualKeyForAuthenticationAsync(keyValue);

            // Assert
            Assert.Null(result);
            
            _cacheMock.Verify(c => c.GetVirtualKeyAsync(keyHash, It.IsAny<Func<string, Task<VirtualKey>>>()), Times.Once);
        }

        [Fact]
        public async Task ValidateVirtualKeyForAuthenticationAsync_WithExpiredKey_ReturnsNull()
        {
            // Arrange
            var keyValue = "condt_expired";
            var keyHash = ComputeExpectedHash(keyValue);
            var virtualKey = CreateExpiredVirtualKey();

            _cacheMock.Setup(c => c.GetVirtualKeyAsync(keyHash, It.IsAny<Func<string, Task<VirtualKey>>>()))
                .ReturnsAsync(virtualKey);

            // Act
            var result = await _service.ValidateVirtualKeyForAuthenticationAsync(keyValue);

            // Assert
            Assert.Null(result);
            
            _cacheMock.Verify(c => c.GetVirtualKeyAsync(keyHash, It.IsAny<Func<string, Task<VirtualKey>>>()), Times.Once);
        }

        [Fact]
        public async Task ValidateVirtualKeyAsync_WithZeroBalance_ReturnsNull()
        {
            // Arrange
            var keyValue = "condt_zerobalance";
            var keyHash = ComputeExpectedHash(keyValue);
            var virtualKey = CreateEnabledVirtualKey();
            var group = CreateGroupWithBalance(0.00m); // Zero balance

            _cacheMock.Setup(c => c.GetVirtualKeyAsync(keyHash, It.IsAny<Func<string, Task<VirtualKey>>>()))
                .ReturnsAsync(virtualKey);
            
            _groupRepositoryMock.Setup(g => g.GetByIdAsync(virtualKey.VirtualKeyGroupId))
                .ReturnsAsync(group);

            // Act
            var result = await _service.ValidateVirtualKeyAsync(keyValue);

            // Assert
            Assert.Null(result); // Returns null due to insufficient balance
            
            _cacheMock.Verify(c => c.GetVirtualKeyAsync(keyHash, It.IsAny<Func<string, Task<VirtualKey>>>()), Times.Once);
            _groupRepositoryMock.Verify(g => g.GetByIdAsync(virtualKey.VirtualKeyGroupId), Times.Once);
            
            // Verify cache invalidation was NOT called (our fix)
            _cacheMock.Verify(c => c.InvalidateVirtualKeyAsync(keyHash), Times.Never);
        }

        [Fact]
        public async Task ValidateVirtualKeyAsync_WithSufficientBalance_ReturnsVirtualKey()
        {
            // Arrange
            var keyValue = "condt_goodbalance";
            var keyHash = ComputeExpectedHash(keyValue);
            var virtualKey = CreateEnabledVirtualKey();
            var group = CreateGroupWithBalance(50.00m); // Sufficient balance

            _cacheMock.Setup(c => c.GetVirtualKeyAsync(keyHash, It.IsAny<Func<string, Task<VirtualKey>>>()))
                .ReturnsAsync(virtualKey);
            
            _groupRepositoryMock.Setup(g => g.GetByIdAsync(virtualKey.VirtualKeyGroupId))
                .ReturnsAsync(group);

            // Act
            var result = await _service.ValidateVirtualKeyAsync(keyValue);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(virtualKey.Id, result.Id);
            
            _cacheMock.Verify(c => c.GetVirtualKeyAsync(keyHash, It.IsAny<Func<string, Task<VirtualKey>>>()), Times.Once);
            _groupRepositoryMock.Verify(g => g.GetByIdAsync(virtualKey.VirtualKeyGroupId), Times.Once);
            
            // Verify no cache invalidation for valid keys
            _cacheMock.Verify(c => c.InvalidateVirtualKeyAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ValidateVirtualKeyForAuthenticationAsync_WithModelRestriction_ValidatesModel()
        {
            // Arrange
            var keyValue = "condt_restricted";
            var keyHash = ComputeExpectedHash(keyValue);
            var virtualKey = CreateVirtualKeyWithAllowedModels("gpt-4,gpt-3.5-turbo");
            var requestedModel = "gpt-4";

            _cacheMock.Setup(c => c.GetVirtualKeyAsync(keyHash, It.IsAny<Func<string, Task<VirtualKey>>>()))
                .ReturnsAsync(virtualKey);

            // Act
            var result = await _service.ValidateVirtualKeyForAuthenticationAsync(keyValue, requestedModel);

            // Assert
            Assert.NotNull(result); // Should succeed for allowed model
            Assert.Equal(virtualKey.Id, result.Id);
            
            _cacheMock.Verify(c => c.GetVirtualKeyAsync(keyHash, It.IsAny<Func<string, Task<VirtualKey>>>()), Times.Once);
        }

        [Fact]
        public async Task ValidateVirtualKeyForAuthenticationAsync_WithRestrictedModel_ReturnsNull()
        {
            // Arrange
            var keyValue = "condt_restricted";
            var keyHash = ComputeExpectedHash(keyValue);
            var virtualKey = CreateVirtualKeyWithAllowedModels("gpt-4,gpt-3.5-turbo");
            var requestedModel = "claude-3-opus"; // Not in allowed list

            _cacheMock.Setup(c => c.GetVirtualKeyAsync(keyHash, It.IsAny<Func<string, Task<VirtualKey>>>()))
                .ReturnsAsync(virtualKey);

            // Act
            var result = await _service.ValidateVirtualKeyForAuthenticationAsync(keyValue, requestedModel);

            // Assert
            Assert.Null(result); // Should fail for disallowed model
            
            _cacheMock.Verify(c => c.GetVirtualKeyAsync(keyHash, It.IsAny<Func<string, Task<VirtualKey>>>()), Times.Once);
        }

        [Fact]
        public async Task ValidateVirtualKeyForAuthenticationAsync_WithEmptyKey_ReturnsNull()
        {
            // Act & Assert
            var result1 = await _service.ValidateVirtualKeyForAuthenticationAsync("");
            var result2 = await _service.ValidateVirtualKeyForAuthenticationAsync(null);

            Assert.Null(result1);
            Assert.Null(result2);
            
            // Verify cache was never accessed
            _cacheMock.Verify(c => c.GetVirtualKeyAsync(It.IsAny<string>(), It.IsAny<Func<string, Task<VirtualKey>>>()), Times.Never);
        }

        [Fact]
        public async Task ValidateVirtualKeyForAuthenticationAsync_WithCacheMiss_CallsRepository()
        {
            // Arrange
            var keyValue = "condt_cachemiss";
            var keyHash = ComputeExpectedHash(keyValue);
            var virtualKey = CreateEnabledVirtualKey();

            // Setup cache to call the fallback function (simulating cache miss)
            _cacheMock.Setup(c => c.GetVirtualKeyAsync(keyHash, It.IsAny<Func<string, Task<VirtualKey>>>()))
                .Returns<string, Func<string, Task<VirtualKey>>>(async (hash, fallback) => await fallback(hash));
            
            _virtualKeyRepositoryMock.Setup(r => r.GetByKeyHashAsync(keyHash, default))
                .ReturnsAsync(virtualKey);

            // Act
            var result = await _service.ValidateVirtualKeyForAuthenticationAsync(keyValue);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(virtualKey.Id, result.Id);
            
            _cacheMock.Verify(c => c.GetVirtualKeyAsync(keyHash, It.IsAny<Func<string, Task<VirtualKey>>>()), Times.Once);
            _virtualKeyRepositoryMock.Verify(r => r.GetByKeyHashAsync(keyHash, default), Times.Once);
        }

        /// <summary>
        /// Creates an enabled virtual key for testing
        /// </summary>
        private VirtualKey CreateEnabledVirtualKey()
        {
            return new VirtualKey
            {
                Id = 1,
                KeyName = "Test Key",
                KeyHash = "test-hash",
                IsEnabled = true,
                VirtualKeyGroupId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };
        }

        /// <summary>
        /// Creates a disabled virtual key for testing
        /// </summary>
        private VirtualKey CreateDisabledVirtualKey()
        {
            var key = CreateEnabledVirtualKey();
            key.IsEnabled = false;
            return key;
        }

        /// <summary>
        /// Creates an expired virtual key for testing
        /// </summary>
        private VirtualKey CreateExpiredVirtualKey()
        {
            var key = CreateEnabledVirtualKey();
            key.ExpiresAt = DateTime.UtcNow.AddDays(-1); // Expired yesterday
            return key;
        }

        /// <summary>
        /// Creates a virtual key with specific allowed models
        /// </summary>
        private VirtualKey CreateVirtualKeyWithAllowedModels(string allowedModels)
        {
            var key = CreateEnabledVirtualKey();
            key.AllowedModels = allowedModels;
            return key;
        }

        /// <summary>
        /// Creates a virtual key group with specified balance
        /// </summary>
        private VirtualKeyGroup CreateGroupWithBalance(decimal balance)
        {
            return new VirtualKeyGroup
            {
                Id = 1,
                GroupName = "Test Group",
                Balance = balance,
                LifetimeCreditsAdded = 100.00m,
                LifetimeSpent = 100.00m - balance,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Computes the expected SHA256 hash for a key (matches the service implementation)
        /// </summary>
        private string ComputeExpectedHash(string key)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(key);
            var hash = sha256.ComputeHash(bytes);
            
            var builder = new System.Text.StringBuilder();
            foreach (byte b in hash)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }
    }
}
using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace ConduitLLM.Tests.Services
{
    /// <summary>
    /// Unit tests for RedisDistributedLockService covering Redis-based distributed locking scenarios.
    /// </summary>
    public class RedisDistributedLockServiceTests : IDisposable
    {
        private readonly Mock<IConnectionMultiplexer> _mockRedis;
        private readonly Mock<IDatabase> _mockDatabase;
        private readonly Mock<ILogger<RedisDistributedLockService>> _mockLogger;
        private readonly RedisDistributedLockService _service;

        public RedisDistributedLockServiceTests()
        {
            _mockRedis = new Mock<IConnectionMultiplexer>();
            _mockDatabase = new Mock<IDatabase>();
            _mockLogger = new Mock<ILogger<RedisDistributedLockService>>();

            _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_mockDatabase.Object);

            _service = new RedisDistributedLockService(_mockRedis.Object, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithNullRedis_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new RedisDistributedLockService(null, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new RedisDistributedLockService(_mockRedis.Object, null));
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesService()
        {
            // Act
            var service = new RedisDistributedLockService(_mockRedis.Object, _mockLogger.Object);

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public async Task AcquireLockAsync_WithValidKey_AcquiresLock()
        {
            // Arrange
            var key = "test-lock";
            var expiry = TimeSpan.FromMinutes(5);

            _mockDatabase.Setup(db => db.StringSetAsync(
                It.Is<RedisKey>(k => k.ToString() == "lock:test-lock"),
                It.IsAny<RedisValue>(),
                expiry,
                When.NotExists,
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            var lockHandle = await _service.AcquireLockAsync(key, expiry);

            // Assert
            Assert.NotNull(lockHandle);
            Assert.Equal("lock:test-lock", lockHandle.Key);
            Assert.NotNull(lockHandle.LockValue);
            Assert.True(lockHandle.IsValid);
            Assert.True(lockHandle.ExpiryTime > DateTime.UtcNow);

            // Verify Redis call
            _mockDatabase.Verify(db => db.StringSetAsync(
                It.Is<RedisKey>(k => k.ToString() == "lock:test-lock"),
                It.IsAny<RedisValue>(),
                expiry,
                When.NotExists,
                It.IsAny<CommandFlags>()), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AcquireLockAsync_WithInvalidKey_ThrowsArgumentException(string invalidKey)
        {
            // Arrange
            var expiry = TimeSpan.FromMinutes(5);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.AcquireLockAsync(invalidKey, expiry));
        }

        [Fact]
        public async Task AcquireLockAsync_WhenLockAlreadyHeld_ReturnsNull()
        {
            // Arrange
            var key = "test-lock";
            var expiry = TimeSpan.FromMinutes(5);

            // Redis returns false indicating lock already exists
            _mockDatabase.Setup(db => db.StringSetAsync(
                It.Is<RedisKey>(k => k.ToString() == "lock:test-lock"),
                It.IsAny<RedisValue>(),
                expiry,
                When.NotExists,
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(false);

            // Act
            var lockHandle = await _service.AcquireLockAsync(key, expiry);

            // Assert
            Assert.Null(lockHandle);
        }

        [Fact]
        public async Task AcquireLockAsync_WithRedisException_RethrowsException()
        {
            // Arrange
            var key = "test-lock";
            var expiry = TimeSpan.FromMinutes(5);

            _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
                .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis connection failed"));

            // Act & Assert
            await Assert.ThrowsAsync<RedisConnectionException>(() =>
                _service.AcquireLockAsync(key, expiry));
        }

        [Fact]
        public async Task AcquireLockWithRetryAsync_WithAvailableLock_AcquiresImmediately()
        {
            // Arrange
            var key = "test-lock";
            var expiry = TimeSpan.FromMinutes(5);
            var timeout = TimeSpan.FromSeconds(2);
            var retryDelay = TimeSpan.FromMilliseconds(100);

            _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            var startTime = DateTime.UtcNow;
            var lockHandle = await _service.AcquireLockWithRetryAsync(key, expiry, timeout, retryDelay);
            var elapsed = DateTime.UtcNow - startTime;

            // Assert
            Assert.NotNull(lockHandle);
            Assert.Equal("lock:test-lock", lockHandle.Key);
            Assert.True(elapsed < TimeSpan.FromSeconds(1)); // Should acquire quickly
        }

        [Fact]
        public async Task AcquireLockWithRetryAsync_WithPersistentLock_ThrowsTimeoutException()
        {
            // Arrange
            var key = "test-lock";
            var expiry = TimeSpan.FromMinutes(5);
            var shortTimeout = TimeSpan.FromMilliseconds(200);
            var retryDelay = TimeSpan.FromMilliseconds(50);

            // Redis always returns false (lock held)
            _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(() =>
                _service.AcquireLockWithRetryAsync(key, expiry, shortTimeout, retryDelay));
        }

        [Fact]
        public async Task AcquireLockWithRetryAsync_WithCancellation_ThrowsOperationCancelledException()
        {
            // Arrange
            var key = "test-lock";
            var expiry = TimeSpan.FromMinutes(5);
            var timeout = TimeSpan.FromSeconds(5);
            var retryDelay = TimeSpan.FromMilliseconds(100);

            _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(false);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(150));

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _service.AcquireLockWithRetryAsync(key, expiry, timeout, retryDelay, cts.Token));
        }

        [Fact]
        public async Task IsLockedAsync_WithHeldLock_ReturnsTrue()
        {
            // Arrange
            var key = "test-lock";

            _mockDatabase.Setup(db => db.KeyExistsAsync(
                It.Is<RedisKey>(k => k.ToString() == "lock:test-lock"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            var isLocked = await _service.IsLockedAsync(key);

            // Assert
            Assert.True(isLocked);

            _mockDatabase.Verify(db => db.KeyExistsAsync(
                It.Is<RedisKey>(k => k.ToString() == "lock:test-lock"),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task IsLockedAsync_WithoutLock_ReturnsFalse()
        {
            // Arrange
            var key = "test-lock";

            _mockDatabase.Setup(db => db.KeyExistsAsync(
                It.Is<RedisKey>(k => k.ToString() == "lock:test-lock"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(false);

            // Act
            var isLocked = await _service.IsLockedAsync(key);

            // Assert
            Assert.False(isLocked);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task IsLockedAsync_WithInvalidKey_ThrowsArgumentException(string invalidKey)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.IsLockedAsync(invalidKey));
        }

        [Fact]
        public async Task IsLockedAsync_WithRedisException_RethrowsException()
        {
            // Arrange
            var key = "test-lock";

            _mockDatabase.Setup(db => db.KeyExistsAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
                .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis connection failed"));

            // Act & Assert
            await Assert.ThrowsAsync<RedisConnectionException>(() => _service.IsLockedAsync(key));
        }

        [Fact]
        public async Task ExtendLockAsync_WithValidLock_ExtendsSuccessfully()
        {
            // Arrange
            var lockKey = "lock:test-lock";
            var lockValue = Guid.NewGuid().ToString();
            var extension = TimeSpan.FromMinutes(5);

            var mockLock = new Mock<IDistributedLock>();
            mockLock.Setup(l => l.Key).Returns(lockKey);
            mockLock.Setup(l => l.LockValue).Returns(lockValue);

            // Mock Lua script execution to return 1 (success)
            _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.Is<RedisKey[]>(keys => keys[0] == lockKey),
                It.Is<RedisValue[]>(values => values[0] == lockValue && values[1] == (long)extension.TotalMilliseconds),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisResult.Create(1));

            // Act
            var extended = await _service.ExtendLockAsync(mockLock.Object, extension);

            // Assert
            Assert.True(extended);

            _mockDatabase.Verify(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.Is<RedisKey[]>(keys => keys[0] == lockKey),
                It.Is<RedisValue[]>(values => values[0] == lockValue && values[1] == (long)extension.TotalMilliseconds),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task ExtendLockAsync_WithInvalidLock_ReturnsFalse()
        {
            // Arrange
            var lockKey = "lock:test-lock";
            var lockValue = Guid.NewGuid().ToString();
            var extension = TimeSpan.FromMinutes(5);

            var mockLock = new Mock<IDistributedLock>();
            mockLock.Setup(l => l.Key).Returns(lockKey);
            mockLock.Setup(l => l.LockValue).Returns(lockValue);

            // Mock Lua script execution to return 0 (failure)
            _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisResult.Create(0));

            // Act
            var extended = await _service.ExtendLockAsync(mockLock.Object, extension);

            // Assert
            Assert.False(extended);
        }

        [Fact]
        public async Task ExtendLockAsync_WithNullLock_ThrowsArgumentNullException()
        {
            // Arrange
            var extension = TimeSpan.FromMinutes(5);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.ExtendLockAsync(null, extension));
        }

        [Fact]
        public async Task ExtendLockAsync_WithRedisException_RethrowsException()
        {
            // Arrange
            var mockLock = new Mock<IDistributedLock>();
            mockLock.Setup(l => l.Key).Returns("lock:test-lock");
            mockLock.Setup(l => l.LockValue).Returns(Guid.NewGuid().ToString());
            var extension = TimeSpan.FromMinutes(5);

            _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
                .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis connection failed"));

            // Act & Assert
            await Assert.ThrowsAsync<RedisConnectionException>(() =>
                _service.ExtendLockAsync(mockLock.Object, extension));
        }

        [Fact]
        public async Task RedisDistributedLock_ReleaseAsync_ReleasesLockSuccessfully()
        {
            // Arrange
            var key = "test-lock";
            var expiry = TimeSpan.FromMinutes(5);
            string capturedLockValue = "";

            _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
                .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>((k, v, t, w, f) =>
                {
                    capturedLockValue = v.ToString();
                })
                .ReturnsAsync(true);

            // Mock successful script execution for release
            _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.Is<RedisValue[]>(values => values[0] == capturedLockValue),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisResult.Create(1));

            var lockHandle = await _service.AcquireLockAsync(key, expiry);
            Assert.NotNull(lockHandle);

            // Act
            await lockHandle.ReleaseAsync();

            // Assert
            _mockDatabase.Verify(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.Is<RedisKey[]>(keys => keys[0] == "lock:test-lock"),
                It.Is<RedisValue[]>(values => values[0] == capturedLockValue),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task RedisDistributedLock_ReleaseAsync_WhenNotOwned_HandlesGracefully()
        {
            // Arrange
            var key = "test-lock";
            var expiry = TimeSpan.FromMinutes(5);

            _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Mock script execution to return 0 (lock not owned)
            _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisResult.Create(0));

            var lockHandle = await _service.AcquireLockAsync(key, expiry);
            Assert.NotNull(lockHandle);

            // Act & Assert - Should not throw
            await lockHandle.ReleaseAsync();
        }

        [Fact]
        public async Task RedisDistributedLock_ReleaseAsync_WithRedisException_HandlesGracefully()
        {
            // Arrange
            var key = "test-lock";
            var expiry = TimeSpan.FromMinutes(5);

            _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
                .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis connection failed"));

            var lockHandle = await _service.AcquireLockAsync(key, expiry);
            Assert.NotNull(lockHandle);

            // Act & Assert - Should not throw (exception handled internally)
            await lockHandle.ReleaseAsync();
        }

        [Fact]
        public async Task RedisDistributedLock_Dispose_ReleasesLock()
        {
            // Arrange
            var key = "test-lock";
            var expiry = TimeSpan.FromMinutes(5);

            _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisResult.Create(1));

            // Act
            IDistributedLock lockHandle;
            using (lockHandle = await _service.AcquireLockAsync(key, expiry))
            {
                Assert.NotNull(lockHandle);
            } // Dispose called here

            // Allow async dispose to complete
            await Task.Delay(10);

            // Assert - Verify script was executed (release attempt made)
            _mockDatabase.Verify(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task RedisDistributedLock_MultipleReleases_DoesNotThrow()
        {
            // Arrange
            var key = "test-lock";
            var expiry = TimeSpan.FromMinutes(5);

            _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisResult.Create(1));

            var lockHandle = await _service.AcquireLockAsync(key, expiry);
            Assert.NotNull(lockHandle);

            // Act & Assert - Multiple releases should not throw
            await lockHandle.ReleaseAsync();
            await lockHandle.ReleaseAsync();
            lockHandle.Dispose();
            lockHandle.Dispose();
        }

        [Fact]
        public async Task RedisDistributedLock_IsValid_ReflectsExpirationCorrectly()
        {
            // Arrange
            var key = "test-lock";
            var shortExpiry = TimeSpan.FromMilliseconds(100);

            _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            var lockHandle = await _service.AcquireLockAsync(key, shortExpiry);
            Assert.NotNull(lockHandle);

            // Initially should be valid
            Assert.True(lockHandle.IsValid);

            // Wait for expiration
            await Task.Delay(TimeSpan.FromMilliseconds(150));

            // Should no longer be valid
            Assert.False(lockHandle.IsValid);
        }

        [Fact]
        public void RedisDistributedLock_Properties_ReturnCorrectValues()
        {
            // Arrange
            var key = "test-lock";
            var expiry = TimeSpan.FromMinutes(5);

            _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            var lockHandle = _service.AcquireLockAsync(key, expiry).Result;

            // Assert
            Assert.NotNull(lockHandle);
            Assert.Equal("lock:test-lock", lockHandle.Key);
            Assert.NotNull(lockHandle.LockValue);
            Assert.True(Guid.TryParse(lockHandle.LockValue, out _)); // Should be a valid GUID
            Assert.True(lockHandle.ExpiryTime > DateTime.UtcNow);
            Assert.True(lockHandle.ExpiryTime <= DateTime.UtcNow.Add(expiry).AddSeconds(1));
        }

        public void Dispose()
        {
            // No cleanup needed for mocked services
        }
    }
}
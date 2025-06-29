using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Services
{
    /// <summary>
    /// Unit tests for InMemoryDistributedLockService covering lock acquisition, expiration, and concurrent access.
    /// </summary>
    public class InMemoryDistributedLockServiceTests : IDisposable
    {
        private readonly Mock<ILogger<InMemoryDistributedLockService>> _mockLogger;
        private readonly InMemoryDistributedLockService _service;

        public InMemoryDistributedLockServiceTests()
        {
            _mockLogger = new Mock<ILogger<InMemoryDistributedLockService>>();
            _service = new InMemoryDistributedLockService(_mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new InMemoryDistributedLockService(null));
        }

        [Fact]
        public void Constructor_WithValidLogger_CreatesService()
        {
            // Act
            var service = new InMemoryDistributedLockService(_mockLogger.Object);

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public async Task AcquireLockAsync_WithValidKey_AcquiresLock()
        {
            // Arrange
            var key = "test-lock";
            var expiry = TimeSpan.FromMinutes(5);

            // Act
            var lockHandle = await _service.AcquireLockAsync(key, expiry);

            // Assert
            Assert.NotNull(lockHandle);
            Assert.Equal(key, lockHandle.Key);
            Assert.NotNull(lockHandle.LockValue);
            Assert.True(lockHandle.IsValid);
            Assert.True(lockHandle.ExpiryTime > DateTime.UtcNow);
            Assert.True(lockHandle.ExpiryTime <= DateTime.UtcNow.Add(expiry).AddSeconds(1)); // Allow small time difference
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

            // First acquisition should succeed
            var firstLock = await _service.AcquireLockAsync(key, expiry);
            Assert.NotNull(firstLock);

            // Act - Second acquisition should fail
            var secondLock = await _service.AcquireLockAsync(key, expiry);

            // Assert
            Assert.Null(secondLock);

            // Cleanup
            await firstLock.ReleaseAsync();
        }

        [Fact]
        public async Task AcquireLockAsync_AfterLockReleased_AcquiresNewLock()
        {
            // Arrange
            var key = "test-lock";
            var expiry = TimeSpan.FromMinutes(5);

            // First acquisition and release
            var firstLock = await _service.AcquireLockAsync(key, expiry);
            Assert.NotNull(firstLock);
            await firstLock.ReleaseAsync();

            // Act - Second acquisition should succeed
            var secondLock = await _service.AcquireLockAsync(key, expiry);

            // Assert
            Assert.NotNull(secondLock);
            Assert.Equal(key, secondLock.Key);
            Assert.NotEqual(firstLock.LockValue, secondLock.LockValue); // Should have different values

            // Cleanup
            await secondLock.ReleaseAsync();
        }

        [Fact]
        public async Task AcquireLockAsync_AfterLockExpired_AcquiresNewLock()
        {
            // Arrange
            var key = "test-lock";
            var shortExpiry = TimeSpan.FromMilliseconds(50);

            // First acquisition with short expiry
            var firstLock = await _service.AcquireLockAsync(key, shortExpiry);
            Assert.NotNull(firstLock);

            // Wait for lock to expire
            await Task.Delay(TimeSpan.FromMilliseconds(100));

            // Act - Second acquisition should succeed because first lock expired
            var secondLock = await _service.AcquireLockAsync(key, TimeSpan.FromMinutes(5));

            // Assert
            Assert.NotNull(secondLock);
            Assert.Equal(key, secondLock.Key);
            Assert.False(firstLock.IsValid); // First lock should be expired
            Assert.True(secondLock.IsValid); // Second lock should be valid

            // Cleanup
            await secondLock.ReleaseAsync();
        }

        [Fact]
        public async Task AcquireLockWithRetryAsync_WithAvailableLock_AcquiresImmediately()
        {
            // Arrange
            var key = "test-lock";
            var expiry = TimeSpan.FromMinutes(5);
            var timeout = TimeSpan.FromSeconds(2);
            var retryDelay = TimeSpan.FromMilliseconds(100);

            // Act
            var startTime = DateTime.UtcNow;
            var lockHandle = await _service.AcquireLockWithRetryAsync(key, expiry, timeout, retryDelay);
            var elapsed = DateTime.UtcNow - startTime;

            // Assert
            Assert.NotNull(lockHandle);
            Assert.Equal(key, lockHandle.Key);
            Assert.True(elapsed < TimeSpan.FromSeconds(1)); // Should acquire quickly

            // Cleanup
            await lockHandle.ReleaseAsync();
        }

        [Fact]
        public async Task AcquireLockWithRetryAsync_WithHeldLockThatExpires_EventuallyAcquires()
        {
            // Arrange
            var key = "test-lock";
            var shortExpiry = TimeSpan.FromMilliseconds(200);
            var longExpiry = TimeSpan.FromMinutes(5);
            var timeout = TimeSpan.FromSeconds(2);
            var retryDelay = TimeSpan.FromMilliseconds(50);

            // Acquire lock with short expiry
            var firstLock = await _service.AcquireLockAsync(key, shortExpiry);
            Assert.NotNull(firstLock);

            // Act - Try to acquire with retry (should succeed after first lock expires)
            var secondLock = await _service.AcquireLockWithRetryAsync(key, longExpiry, timeout, retryDelay);

            // Assert
            Assert.NotNull(secondLock);
            Assert.Equal(key, secondLock.Key);
            Assert.NotEqual(firstLock.LockValue, secondLock.LockValue);

            // Cleanup
            await secondLock.ReleaseAsync();
        }

        [Fact]
        public async Task AcquireLockWithRetryAsync_WithPersistentLock_ThrowsTimeoutException()
        {
            // Arrange
            var key = "test-lock";
            var longExpiry = TimeSpan.FromMinutes(10);
            var shortTimeout = TimeSpan.FromMilliseconds(200);
            var retryDelay = TimeSpan.FromMilliseconds(50);

            // Acquire lock with long expiry
            var firstLock = await _service.AcquireLockAsync(key, longExpiry);
            Assert.NotNull(firstLock);

            // Act & Assert - Retry should timeout
            await Assert.ThrowsAsync<TimeoutException>(() =>
                _service.AcquireLockWithRetryAsync(key, longExpiry, shortTimeout, retryDelay));

            // Cleanup
            await firstLock.ReleaseAsync();
        }

        [Fact]
        public async Task AcquireLockWithRetryAsync_WithCancellation_ThrowsOperationCancelledException()
        {
            // Arrange
            var key = "test-lock";
            var longExpiry = TimeSpan.FromMinutes(10);
            var timeout = TimeSpan.FromSeconds(5);
            var retryDelay = TimeSpan.FromMilliseconds(100);

            // Acquire lock with long expiry
            var firstLock = await _service.AcquireLockAsync(key, longExpiry);
            Assert.NotNull(firstLock);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(150));

            // Act & Assert - Should be cancelled
            await Assert.ThrowsAsync<TaskCanceledException>(() =>
                _service.AcquireLockWithRetryAsync(key, longExpiry, timeout, retryDelay, cts.Token));

            // Cleanup
            await firstLock.ReleaseAsync();
        }

        [Fact]
        public async Task IsLockedAsync_WithHeldLock_ReturnsTrue()
        {
            // Arrange
            var key = "test-lock";
            var expiry = TimeSpan.FromMinutes(5);
            var lockHandle = await _service.AcquireLockAsync(key, expiry);
            Assert.NotNull(lockHandle);

            // Act
            var isLocked = await _service.IsLockedAsync(key);

            // Assert
            Assert.True(isLocked);

            // Cleanup
            await lockHandle.ReleaseAsync();
        }

        [Fact]
        public async Task IsLockedAsync_WithoutLock_ReturnsFalse()
        {
            // Arrange
            var key = "non-existent-lock";

            // Act
            var isLocked = await _service.IsLockedAsync(key);

            // Assert
            Assert.False(isLocked);
        }

        [Fact]
        public async Task IsLockedAsync_WithExpiredLock_ReturnsFalse()
        {
            // Arrange
            var key = "test-lock";
            var shortExpiry = TimeSpan.FromMilliseconds(50);
            var lockHandle = await _service.AcquireLockAsync(key, shortExpiry);
            Assert.NotNull(lockHandle);

            // Wait for lock to expire
            await Task.Delay(TimeSpan.FromMilliseconds(100));

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
        public async Task IsLockedAsync_AfterLockReleased_ReturnsFalse()
        {
            // Arrange
            var key = "test-lock";
            var expiry = TimeSpan.FromMinutes(5);
            var lockHandle = await _service.AcquireLockAsync(key, expiry);
            Assert.NotNull(lockHandle);

            // Release the lock
            await lockHandle.ReleaseAsync();

            // Act
            var isLocked = await _service.IsLockedAsync(key);

            // Assert
            Assert.False(isLocked);
        }

        [Fact]
        public async Task ExtendLockAsync_WithValidLock_ExtendsSuccessfully()
        {
            // Arrange
            var key = "test-lock";
            var initialExpiry = TimeSpan.FromSeconds(1);
            var extension = TimeSpan.FromMinutes(5);

            var lockHandle = await _service.AcquireLockAsync(key, initialExpiry);
            Assert.NotNull(lockHandle);

            var originalExpiryTime = lockHandle.ExpiryTime;

            // Act
            var extended = await _service.ExtendLockAsync(lockHandle, extension);

            // Assert
            Assert.True(extended);
            Assert.True(lockHandle.ExpiryTime > originalExpiryTime);
            Assert.True(lockHandle.IsValid);
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
        public async Task ExtendLockAsync_WithReleasedLock_ReturnsFalse()
        {
            // Arrange
            var key = "test-lock";
            var expiry = TimeSpan.FromMinutes(5);
            var extension = TimeSpan.FromMinutes(2);

            var lockHandle = await _service.AcquireLockAsync(key, expiry);
            Assert.NotNull(lockHandle);

            // Release the lock
            await lockHandle.ReleaseAsync();

            // Act
            var extended = await _service.ExtendLockAsync(lockHandle, extension);

            // Assert
            Assert.False(extended);
        }

        [Fact]
        public async Task ExtendLockAsync_WithExpiredLock_ReturnsFalse()
        {
            // Arrange
            var key = "test-lock";
            var shortExpiry = TimeSpan.FromMilliseconds(50);
            var extension = TimeSpan.FromMinutes(5);

            var lockHandle = await _service.AcquireLockAsync(key, shortExpiry);
            Assert.NotNull(lockHandle);

            // Wait for lock to expire
            await Task.Delay(TimeSpan.FromMilliseconds(100));

            // Act
            var extended = await _service.ExtendLockAsync(lockHandle, extension);

            // Assert
            Assert.False(extended);
            Assert.False(lockHandle.IsValid);
        }

        [Fact]
        public async Task LockHandle_IsValid_ReflectsExpirationCorrectly()
        {
            // Arrange
            var key = "test-lock";
            var shortExpiry = TimeSpan.FromMilliseconds(100);

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
        public async Task LockHandle_Dispose_ReleasesLock()
        {
            // Arrange
            var key = "test-lock";
            var expiry = TimeSpan.FromMinutes(5);

            IDistributedLock lockHandle;
            using (lockHandle = await _service.AcquireLockAsync(key, expiry))
            {
                Assert.NotNull(lockHandle);
                Assert.True(await _service.IsLockedAsync(key));
            } // Dispose called here

            // Act & Assert
            Assert.False(await _service.IsLockedAsync(key));
        }

        [Fact]
        public async Task LockHandle_ReleaseAsync_ReleasesLock()
        {
            // Arrange
            var key = "test-lock";
            var expiry = TimeSpan.FromMinutes(5);

            var lockHandle = await _service.AcquireLockAsync(key, expiry);
            Assert.NotNull(lockHandle);
            Assert.True(await _service.IsLockedAsync(key));

            // Act
            await lockHandle.ReleaseAsync();

            // Assert
            Assert.False(await _service.IsLockedAsync(key));
        }

        [Fact]
        public async Task LockHandle_MultipleReleases_DoesNotThrow()
        {
            // Arrange
            var key = "test-lock";
            var expiry = TimeSpan.FromMinutes(5);

            var lockHandle = await _service.AcquireLockAsync(key, expiry);
            Assert.NotNull(lockHandle);

            // Act & Assert - Multiple releases should not throw
            await lockHandle.ReleaseAsync();
            await lockHandle.ReleaseAsync();
            lockHandle.Dispose();
            lockHandle.Dispose();
        }

        [Fact]
        public async Task ConcurrentLockAcquisition_OnlyOneSucceeds()
        {
            // Arrange
            var key = "concurrent-lock";
            var expiry = TimeSpan.FromMinutes(5);
            var concurrentTasks = new List<Task<IDistributedLock?>>();

            // Act - Try to acquire the same lock from 10 concurrent tasks
            for (int i = 0; i < 10; i++)
            {
                concurrentTasks.Add(_service.AcquireLockAsync(key, expiry));
            }

            var results = await Task.WhenAll(concurrentTasks);

            // Assert
            var successfulLocks = results.Where(result => result != null).ToList();
            var failedLocks = results.Where(result => result == null).ToList();

            Assert.Single(successfulLocks); // Only one should succeed
            Assert.Equal(9, failedLocks.Count); // Nine should fail

            // Cleanup
            if (successfulLocks.Any())
            {
                await successfulLocks[0].ReleaseAsync();
            }
        }

        [Fact]
        public async Task ConcurrentDifferentKeys_AllSucceed()
        {
            // Arrange
            var expiry = TimeSpan.FromMinutes(5);
            var concurrentTasks = new List<Task<IDistributedLock?>>();

            // Act - Acquire locks for different keys concurrently
            for (int i = 0; i < 10; i++)
            {
                var key = $"lock-{i}";
                concurrentTasks.Add(_service.AcquireLockAsync(key, expiry));
            }

            var results = await Task.WhenAll(concurrentTasks);

            // Assert
            Assert.All(results, result => Assert.NotNull(result));
            Assert.Equal(10, results.Where(r => r != null).Count());

            // Verify all keys are unique
            var keys = results.Where(r => r != null).Select(r => r.Key).ToHashSet();
            Assert.Equal(10, keys.Count);

            // Cleanup
            foreach (var lockHandle in results.Where(r => r != null))
            {
                await lockHandle.ReleaseAsync();
            }
        }

        [Fact]
        public async Task AutomaticCleanup_RemovesExpiredLocks()
        {
            // Arrange
            var key = "cleanup-test-lock";
            var shortExpiry = TimeSpan.FromMilliseconds(100);

            // Acquire a lock with short expiry
            var lockHandle = await _service.AcquireLockAsync(key, shortExpiry);
            Assert.NotNull(lockHandle);
            Assert.True(await _service.IsLockedAsync(key));

            // Wait for lock to expire and cleanup to potentially run
            await Task.Delay(TimeSpan.FromMilliseconds(200));

            // Act - Try to acquire the same lock again
            var newLockHandle = await _service.AcquireLockAsync(key, TimeSpan.FromMinutes(5));

            // Assert - Should be able to acquire new lock even without explicit cleanup
            Assert.NotNull(newLockHandle);
            Assert.Equal(key, newLockHandle.Key);

            // Cleanup
            await newLockHandle.ReleaseAsync();
        }

        [Fact]
        public async Task StressTest_RapidAcquireRelease_HandlesCorrectly()
        {
            // Arrange
            var key = "stress-test-lock";
            var expiry = TimeSpan.FromSeconds(1);
            var iterations = 100;
            var successCount = 0;

            // Act - Rapidly acquire and release locks
            for (int i = 0; i < iterations; i++)
            {
                var lockHandle = await _service.AcquireLockAsync(key, expiry);
                if (lockHandle != null)
                {
                    successCount++;
                    await lockHandle.ReleaseAsync();
                }
            }

            // Assert - All acquisitions should succeed since we release immediately
            Assert.Equal(iterations, successCount);
        }

        public void Dispose()
        {
            _service?.Dispose();
        }
    }
}
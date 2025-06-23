using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Services
{
    public class CancellableTaskRegistryTests
    {
        private readonly Mock<ILogger<CancellableTaskRegistry>> _mockLogger;
        private readonly CancellableTaskRegistry _registry;

        public CancellableTaskRegistryTests()
        {
            _mockLogger = new Mock<ILogger<CancellableTaskRegistry>>();
            _registry = new CancellableTaskRegistry(_mockLogger.Object);
        }

        [Fact]
        public void RegisterTask_WithValidTaskId_RegistersSuccessfully()
        {
            // Arrange
            var taskId = "test-task-123";
            using var cts = new CancellationTokenSource();

            // Act
            _registry.RegisterTask(taskId, cts);

            // Assert
            var found = _registry.TryGetCancellationToken(taskId, out var token);
            Assert.True(found);
            Assert.NotNull(token);
            Assert.Equal(cts.Token, token.Value);
        }

        [Fact]
        public void RegisterTask_WithNullTaskId_ThrowsArgumentException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _registry.RegisterTask(null!, cts));
        }

        [Fact]
        public void RegisterTask_WithNullCancellationTokenSource_ThrowsArgumentNullException()
        {
            // Arrange
            var taskId = "test-task-123";

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _registry.RegisterTask(taskId, null!));
        }

        [Fact]
        public void RegisterTask_WithDuplicateTaskId_LogsWarning()
        {
            // Arrange
            var taskId = "duplicate-task";
            using var cts1 = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            // Act
            _registry.RegisterTask(taskId, cts1);
            _registry.RegisterTask(taskId, cts2);

            // Assert - should keep the first registration
            var found = _registry.TryGetCancellationToken(taskId, out var token);
            Assert.True(found);
            Assert.Equal(cts1.Token, token.Value);

            // Verify warning was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Task {taskId} is already registered")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void TryCancel_WithRegisteredTask_CancelsSuccessfully()
        {
            // Arrange
            var taskId = "cancel-test";
            using var cts = new CancellationTokenSource();
            _registry.RegisterTask(taskId, cts);

            // Act
            var result = _registry.TryCancel(taskId);

            // Assert
            Assert.True(result);
            Assert.True(cts.IsCancellationRequested);
        }

        [Fact]
        public void TryCancel_WithUnregisteredTask_ReturnsFalse()
        {
            // Arrange
            var taskId = "non-existent";

            // Act
            var result = _registry.TryCancel(taskId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void TryCancel_WithAlreadyCancelledTask_ReturnsFalse()
        {
            // Arrange
            var taskId = "already-cancelled";
            using var cts = new CancellationTokenSource();
            _registry.RegisterTask(taskId, cts);
            cts.Cancel();

            // Act
            var result = _registry.TryCancel(taskId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void UnregisterTask_RemovesTaskFromRegistry()
        {
            // Arrange
            var taskId = "unregister-test";
            using var cts = new CancellationTokenSource();
            _registry.RegisterTask(taskId, cts);

            // Act
            _registry.UnregisterTask(taskId);

            // Assert
            var found = _registry.TryGetCancellationToken(taskId, out _);
            Assert.False(found);
        }

        [Fact]
        public async Task RegisterTask_AutomaticallyUnregistersOnCancellation()
        {
            // Arrange
            var taskId = "auto-unregister";
            using var cts = new CancellationTokenSource();
            _registry.RegisterTask(taskId, cts);

            // Act
            cts.Cancel();
            await Task.Delay(100); // Give time for the callback to execute

            // Assert
            var found = _registry.TryGetCancellationToken(taskId, out _);
            Assert.False(found);
        }

        [Fact]
        public void CancelAll_CancelsAllRegisteredTasks()
        {
            // Arrange
            var cts1 = new CancellationTokenSource();
            var cts2 = new CancellationTokenSource();
            var cts3 = new CancellationTokenSource();
            
            _registry.RegisterTask("task1", cts1);
            _registry.RegisterTask("task2", cts2);
            _registry.RegisterTask("task3", cts3);

            // Act
            _registry.CancelAll();

            // Assert
            Assert.True(cts1.IsCancellationRequested);
            Assert.True(cts2.IsCancellationRequested);
            Assert.True(cts3.IsCancellationRequested);

            // Verify all tasks were removed
            Assert.False(_registry.TryGetCancellationToken("task1", out _));
            Assert.False(_registry.TryGetCancellationToken("task2", out _));
            Assert.False(_registry.TryGetCancellationToken("task3", out _));

            // Cleanup
            cts1.Dispose();
            cts2.Dispose();
            cts3.Dispose();
        }

        [Fact]
        public void Dispose_DisposesAllCancellationTokenSources()
        {
            // Arrange
            var cts1 = new CancellationTokenSource();
            var cts2 = new CancellationTokenSource();
            
            _registry.RegisterTask("dispose-test-1", cts1);
            _registry.RegisterTask("dispose-test-2", cts2);

            // Act
            _registry.Dispose();

            // Assert
            Assert.Throws<ObjectDisposedException>(() => cts1.Token.ThrowIfCancellationRequested());
            Assert.Throws<ObjectDisposedException>(() => cts2.Token.ThrowIfCancellationRequested());
        }

        [Fact]
        public void TryGetCancellationToken_WithDisposedTokenSource_ReturnsFalse()
        {
            // Arrange
            var taskId = "disposed-token";
            var cts = new CancellationTokenSource();
            _registry.RegisterTask(taskId, cts);
            cts.Dispose();

            // Act
            var found = _registry.TryGetCancellationToken(taskId, out _);

            // Assert
            Assert.False(found);
        }
    }
}
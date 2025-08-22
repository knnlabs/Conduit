using FluentAssertions;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class CancellableTaskRegistryTests
    {
        [Fact]
        public void TryCancel_WithRegisteredTask_CancelsAndReturnsTrue()
        {
            // Arrange
            var taskId = "cancel-task-1";
            using var cts = new CancellationTokenSource();
            _registry.RegisterTask(taskId, cts);

            // Act
            var result = _registry.TryCancel(taskId);

            // Assert
            result.Should().BeTrue();
            cts.Token.IsCancellationRequested.Should().BeTrue();
            
            // Verify logging
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Cancelled task {taskId}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void TryCancel_WithNonExistentTask_ReturnsFalse()
        {
            // Act
            var result = _registry.TryCancel("non-existent-task");

            // Assert
            result.Should().BeFalse();
            
            // Verify debug log
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Task non-existent-task not found in registry")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void TryCancel_WithAlreadyCancelledTask_ReturnsFalse()
        {
            // Arrange
            var taskId = "already-cancelled-task";
            using var cts = new CancellationTokenSource();
            _registry.RegisterTask(taskId, cts);
            
            // Cancel the task first
            cts.Cancel();

            // Act
            var result = _registry.TryCancel(taskId);

            // Assert
            result.Should().BeFalse();
            
            // Verify debug log for already cancelled
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Task {taskId} was already cancelled")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void TryCancel_WithNullTaskId_ReturnsFalse()
        {
            // Act
            var result = _registry.TryCancel(null!);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void TryCancel_WithDisposedToken_HandlesGracefully()
        {
            // Arrange
            var taskId = "disposed-task";
            var cts = new CancellationTokenSource();
            _registry.RegisterTask(taskId, cts);
            
            // Dispose the token source
            cts.Dispose();

            // Act
            var result = _registry.TryCancel(taskId);

            // Assert
            result.Should().BeFalse();
            
            // Verify warning is logged for disposed token
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Cancellation token source for task {taskId} was already disposed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
            
            // Verify that UnregisterTask was called
            _registry.TryGetCancellationToken(taskId, out _).Should().BeFalse();
        }

        [Fact]
        public void CancelledTask_RemainsInRegistryDuringGracePeriod()
        {
            // Arrange
            var taskId = "grace-period-task";
            using var cts = new CancellationTokenSource();
            _registry.RegisterTask(taskId, cts);

            // Act
            _registry.TryCancel(taskId);

            // Assert
            // Task should still be retrievable during grace period
            _registry.TryGetCancellationToken(taskId, out var token).Should().BeTrue();
            token.Value.IsCancellationRequested.Should().BeTrue();
        }

        [Fact]
        public void TryGetCancellationToken_WithDisposedToken_HandlesGracefully()
        {
            // Arrange
            var taskId = "disposed-get-test";
            var cts = new CancellationTokenSource();
            _registry.RegisterTask(taskId, cts);
            
            // Dispose the token source
            cts.Dispose();

            // Act
            var result = _registry.TryGetCancellationToken(taskId, out var token);

            // Assert
            result.Should().BeFalse();
            token.Should().BeNull();
            
            // Verify warning is logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Cancellation token source for task {taskId} was disposed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}
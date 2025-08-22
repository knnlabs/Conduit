using FluentAssertions;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class CancellableTaskRegistryTests
    {
        [Fact]
        public void RegisterTask_WithValidTask_RegistersSuccessfully()
        {
            // Arrange
            var taskId = "test-task-1";
            using var cts = new CancellationTokenSource();

            // Act
            _registry.RegisterTask(taskId, cts);

            // Assert
            _registry.TryGetCancellationToken(taskId, out var token).Should().BeTrue();
            token.Should().NotBeNull();
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Registered cancellable task {taskId}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void RegisterTask_WithNullTaskId_ThrowsArgumentException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();

            // Act & Assert
            var act = () => _registry.RegisterTask(null!, cts);
            act.Should().Throw<ArgumentException>().WithParameterName("taskId");
        }

        [Fact]
        public void RegisterTask_WithEmptyTaskId_ThrowsArgumentException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();

            // Act & Assert
            var act = () => _registry.RegisterTask(string.Empty, cts);
            act.Should().Throw<ArgumentException>().WithParameterName("taskId");
        }

        [Fact]
        public void RegisterTask_WithWhitespaceTaskId_ThrowsArgumentException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();

            // Act & Assert
            var act = () => _registry.RegisterTask("   ", cts);
            act.Should().Throw<ArgumentException>().WithParameterName("taskId");
        }

        [Fact]
        public void RegisterTask_WithNullCancellationTokenSource_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => _registry.RegisterTask("test-task", null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("cts");
        }

        [Fact]
        public void RegisterTask_WithDuplicateTaskId_LogsWarning()
        {
            // Arrange
            var taskId = "duplicate-task";
            using var cts1 = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            // Register first task
            _registry.RegisterTask(taskId, cts1);

            // Act - Register with same ID
            _registry.RegisterTask(taskId, cts2);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Task {taskId} is already registered")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void TryGetCancellationToken_WithRegisteredTask_ReturnsToken()
        {
            // Arrange
            var taskId = "get-token-test";
            using var cts = new CancellationTokenSource();
            _registry.RegisterTask(taskId, cts);

            // Act
            var result = _registry.TryGetCancellationToken(taskId, out var token);

            // Assert
            result.Should().BeTrue();
            token.Should().NotBeNull();
            token.Should().Be(cts.Token);
        }

        [Fact]
        public void TryGetCancellationToken_WithNonExistentTask_ReturnsFalse()
        {
            // Act
            var result = _registry.TryGetCancellationToken("non-existent", out var token);

            // Assert
            result.Should().BeFalse();
            token.Should().BeNull();
        }

        [Fact]
        public void TryGetCancellationToken_WithNullTaskId_ReturnsFalse()
        {
            // Act
            var result = _registry.TryGetCancellationToken(null!, out var token);

            // Assert
            result.Should().BeFalse();
            token.Should().BeNull();
        }
    }
}
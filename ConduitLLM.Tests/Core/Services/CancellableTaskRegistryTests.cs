using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Services;
using ConduitLLM.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Core.Services
{
    [Trait("Category", "Unit")]
    [Trait("Phase", "2")]
    [Trait("Component", "Core")]
    public class CancellableTaskRegistryTests : TestBase, IDisposable
    {
        private readonly Mock<ILogger<CancellableTaskRegistry>> _loggerMock;
        private readonly CancellableTaskRegistry _registry;

        public CancellableTaskRegistryTests(ITestOutputHelper output) : base(output)
        {
            _loggerMock = CreateLogger<CancellableTaskRegistry>();
            _registry = new CancellableTaskRegistry(_loggerMock.Object);
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new CancellableTaskRegistry(null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        }

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
            var act = () => _registry.RegisterTask("", cts);
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

            // Act
            _registry.RegisterTask(taskId, cts1);
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
        public void TryCancel_WithRegisteredTask_CancelsAndReturnsTrue()
        {
            // Arrange
            var taskId = "cancel-test";
            using var cts = new CancellationTokenSource();
            _registry.RegisterTask(taskId, cts);

            // Act
            var result = _registry.TryCancel(taskId);

            // Assert
            result.Should().BeTrue();
            cts.IsCancellationRequested.Should().BeTrue();
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
            var result = _registry.TryCancel("non-existent");

            // Assert
            result.Should().BeFalse();
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Task non-existent not found in registry")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void TryCancel_WithAlreadyCancelledTask_ReturnsFalse()
        {
            // Arrange
            var taskId = "already-cancelled";
            using var cts = new CancellationTokenSource();
            _registry.RegisterTask(taskId, cts);
            
            // Cancel directly through CancellationTokenSource
            cts.Cancel();
            
            // Give time for the automatic unregistration to occur
            Thread.Sleep(100);

            // Act
            var result = _registry.TryCancel(taskId);

            // Assert
            result.Should().BeFalse();
            
            // Since the task was automatically unregistered, it won't be found
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Task {taskId} not found in registry")),
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
        public void UnregisterTask_RemovesTaskFromRegistry()
        {
            // Arrange
            var taskId = "unregister-test";
            using var cts = new CancellationTokenSource();
            _registry.RegisterTask(taskId, cts);

            // Act
            _registry.UnregisterTask(taskId);

            // Assert
            _registry.TryGetCancellationToken(taskId, out _).Should().BeFalse();
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Unregistered task {taskId}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void UnregisterTask_WithNullTaskId_DoesNothing()
        {
            // Act
            _registry.UnregisterTask(null!);

            // Assert - should not throw
        }

        [Fact]
        public void UnregisterTask_WithNonExistentTask_DoesNothing()
        {
            // Act
            _registry.UnregisterTask("non-existent");

            // Assert - should not throw
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

        [Fact]
        public void CancelAll_CancelsAllRegisteredTasks()
        {
            // Arrange
            using var cts1 = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();
            using var cts3 = new CancellationTokenSource();
            
            _registry.RegisterTask("task1", cts1);
            _registry.RegisterTask("task2", cts2);
            _registry.RegisterTask("task3", cts3);

            // Act
            _registry.CancelAll();

            // Assert
            cts1.IsCancellationRequested.Should().BeTrue();
            cts2.IsCancellationRequested.Should().BeTrue();
            cts3.IsCancellationRequested.Should().BeTrue();
            
            _registry.TryGetCancellationToken("task1", out _).Should().BeFalse();
            _registry.TryGetCancellationToken("task2", out _).Should().BeFalse();
            _registry.TryGetCancellationToken("task3", out _).Should().BeFalse();
            
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cancelling all 3 registered tasks")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void CancelAll_WithNoRegisteredTasks_LogsZeroCount()
        {
            // Act
            _registry.CancelAll();

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cancelling all 0 registered tasks")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void AutomaticUnregistration_WhenTokenCancelled_RemovesFromRegistry()
        {
            // Arrange
            var taskId = "auto-unregister";
            using var cts = new CancellationTokenSource();
            _registry.RegisterTask(taskId, cts);

            // Act
            cts.Cancel();
            
            // Give time for the callback to execute
            Thread.Sleep(100);

            // Assert
            _registry.TryGetCancellationToken(taskId, out _).Should().BeFalse();
        }

        [Fact]
        public void Dispose_DisposesAllCancellationTokenSources()
        {
            // Arrange
            var cts1 = new CancellationTokenSource();
            var cts2 = new CancellationTokenSource();
            
            _registry.RegisterTask("task1", cts1);
            _registry.RegisterTask("task2", cts2);

            // Act
            _registry.Dispose();

            // Assert
            var act1 = () => cts1.Token.IsCancellationRequested;
            act1.Should().Throw<ObjectDisposedException>();
            
            var act2 = () => cts2.Token.IsCancellationRequested;
            act2.Should().Throw<ObjectDisposedException>();
            
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Disposing CancellableTaskRegistry")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            // Act & Assert
            _registry.Dispose();
            _registry.Dispose(); // Should not throw
        }

        [Fact]
        public async Task ConcurrentRegistrations_HandlesSafely()
        {
            // Arrange
            var tasks = new Task[10];
            var cancellationTokenSources = new CancellationTokenSource[10];

            // Act
            for (int i = 0; i < 10; i++)
            {
                var index = i;
                cancellationTokenSources[i] = new CancellationTokenSource();
                tasks[i] = Task.Run(() => _registry.RegisterTask($"task-{index}", cancellationTokenSources[index]));
            }

            await Task.WhenAll(tasks);

            // Assert
            for (int i = 0; i < 10; i++)
            {
                _registry.TryGetCancellationToken($"task-{i}", out var token).Should().BeTrue();
            }

            // Cleanup
            foreach (var cts in cancellationTokenSources)
            {
                cts.Dispose();
            }
        }

        [Fact]
        public async Task ConcurrentCancellations_HandlesSafely()
        {
            // Arrange
            var cancellationTokenSources = new CancellationTokenSource[10];
            for (int i = 0; i < 10; i++)
            {
                cancellationTokenSources[i] = new CancellationTokenSource();
                _registry.RegisterTask($"task-{i}", cancellationTokenSources[i]);
            }

            // Act
            var tasks = new Task<bool>[10];
            for (int i = 0; i < 10; i++)
            {
                var index = i;
                tasks[i] = Task.Run(() => _registry.TryCancel($"task-{index}"));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().AllSatisfy(r => r.Should().BeTrue());
            for (int i = 0; i < 10; i++)
            {
                cancellationTokenSources[i].IsCancellationRequested.Should().BeTrue();
            }

            // Cleanup
            foreach (var cts in cancellationTokenSources)
            {
                cts.Dispose();
            }
        }

        [Fact]
        public void TryCancel_WithDisposedToken_HandlesGracefully()
        {
            // Arrange
            var taskId = "disposed-token";
            var cts = new CancellationTokenSource();
            _registry.RegisterTask(taskId, cts);
            cts.Dispose();

            // Act
            var result = _registry.TryCancel(taskId);

            // Assert
            result.Should().BeFalse();
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Cancellation token source for task {taskId} was already disposed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void TryGetCancellationToken_WithDisposedToken_HandlesGracefully()
        {
            // Arrange
            var taskId = "disposed-token";
            var cts = new CancellationTokenSource();
            _registry.RegisterTask(taskId, cts);
            cts.Dispose();

            // Act
            var result = _registry.TryGetCancellationToken(taskId, out var token);

            // Assert
            result.Should().BeFalse();
            token.Should().BeNull();
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Cancellation token source for task {taskId} was disposed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        public new void Dispose()
        {
            _registry?.Dispose();
            base.Dispose();
        }
    }
}
using System;
using System.Diagnostics;
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

            // Act - Try to cancel again (task is still in registry due to grace period)
            var result = _registry.TryCancel(taskId);

            // Assert
            result.Should().BeFalse("cannot cancel an already cancelled task");
            
            // The task is still in registry (grace period), but already cancelled
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
        public void AutomaticUnregistration_WhenTokenCancelled_KeepsTaskInRegistryDuringGracePeriod()
        {
            // Arrange
            var taskId = "auto-unregister";
            using var cts = new CancellationTokenSource();
            _registry.RegisterTask(taskId, cts);

            // Act
            cts.Cancel();
            
            // Give time for the callback to execute
            Thread.Sleep(100);

            // Assert - Task should still be in registry due to grace period
            _registry.TryGetCancellationToken(taskId, out _).Should().BeTrue("task should remain in registry during grace period");
            
            // Note: The actual removal happens after grace period expires (5 seconds by default)
            // This is tested in GracePeriod_WhenTokenCancelled_TaskRemainsAccessibleDuringGracePeriod
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

        [Fact]
        public void CancelledTask_RemainsInRegistryDuringGracePeriod()
        {
            // This test verifies that cancelled tasks remain accessible during the grace period.
            // The actual removal happens asynchronously via the cleanup timer.
            
            // Arrange
            var taskId = "grace-test";
            using var cts = new CancellationTokenSource();
            _registry.RegisterTask(taskId, cts);
            
            // Act - Cancel the task
            cts.Cancel();
            
            // Assert - Task should still be in registry immediately after cancellation
            _registry.TryGetCancellationToken(taskId, out var token).Should().BeTrue("task should remain during grace period");
            token.Should().NotBeNull();
            token!.Value.IsCancellationRequested.Should().BeTrue();
            
            // Verify that the task was marked as cancelled in the logs
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Marked task {taskId} as cancelled, will be removed after grace period")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task CleanupTimer_RemovesMultipleCancelledTasksAfterGracePeriod()
        {
            // Arrange
            var shortGracePeriod = TimeSpan.FromMilliseconds(300);
            var registry = new CancellableTaskRegistry(_loggerMock.Object, shortGracePeriod);
            var taskIds = new[] { "task1", "task2", "task3" };
            var ctsList = new List<CancellationTokenSource>();
            
            try
            {
                foreach (var taskId in taskIds)
                {
                    var cts = new CancellationTokenSource();
                    ctsList.Add(cts);
                    registry.RegisterTask(taskId, cts);
                }
                
                // Act - Cancel all tasks
                foreach (var cts in ctsList)
                {
                    cts.Cancel();
                }
                
                // Assert - All tasks should still be accessible immediately
                foreach (var taskId in taskIds)
                {
                    registry.TryGetCancellationToken(taskId, out _).Should().BeTrue();
                }
                
                // Wait for grace period to expire plus cleanup timer interval
                // Timer starts after 1 second delay, then runs every 1 second
                // We need: 1000ms (initial delay) + time for timer to execute cleanup
                // Add extra buffer for system load: 2500ms total to ensure timer has executed
                await Task.Delay(2500);
                
                // All tasks should be removed
                foreach (var taskId in taskIds)
                {
                    registry.TryGetCancellationToken(taskId, out _).Should().BeFalse();
                }
            }
            finally
            {
                // Cleanup
                foreach (var cts in ctsList)
                {
                    cts.Dispose();
                }
                registry.Dispose();
            }
        }

        [Fact]
        public async Task Dispose_WithRunningTimer_StopsCleanupOperations()
        {
            // Arrange
            var shortGracePeriod = TimeSpan.FromMilliseconds(200);
            var registry = new CancellableTaskRegistry(_loggerMock.Object, shortGracePeriod);
            var taskId = "timer-test";
            using var cts = new CancellationTokenSource();
            
            registry.RegisterTask(taskId, cts);
            cts.Cancel();
            
            // Act - Dispose the registry while timer is running
            registry.Dispose();
            
            // Wait longer than grace period
            await Task.Delay(400);
            
            // Assert - No exceptions should occur and dispose should log
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Disposing CancellableTaskRegistry")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
            
            // Verify timer doesn't continue after disposal by checking that
            // no cleanup logs occur after disposal
            var cleanupLogCount = _loggerMock.Invocations
                .Count(inv => inv.Arguments.Any(arg => 
                    arg != null && arg.ToString()!.Contains("Removed cancelled task")));
            
            // Wait a bit more to ensure timer is really stopped
            await Task.Delay(200);
            
            var newCleanupLogCount = _loggerMock.Invocations
                .Count(inv => inv.Arguments.Any(arg => 
                    arg != null && arg.ToString()!.Contains("Removed cancelled task")));
            
            newCleanupLogCount.Should().Be(cleanupLogCount, "timer should not run after disposal");
        }

        [Fact]
        public void Constructor_WithCustomGracePeriod_UsesProvidedValue()
        {
            // Arrange & Act
            var customGracePeriod = TimeSpan.FromSeconds(10);
            using var registry = new CancellableTaskRegistry(_loggerMock.Object, customGracePeriod);
            var taskId = "custom-grace";
            using var cts = new CancellationTokenSource();
            
            registry.RegisterTask(taskId, cts);
            cts.Cancel();
            
            // Assert - Task should still be in registry after default grace period
            Thread.Sleep(TimeSpan.FromSeconds(6)); // > default 5 seconds
            registry.TryGetCancellationToken(taskId, out _).Should().BeTrue("custom grace period should be respected");
        }

        [Fact(Skip = "Flaky timing test - relies on 1-second cleanup timer that can be delayed by system load in concurrent test runs")]
        public async Task NonCancelledTasks_AreNotRemovedByCleanupTimer()
        {
            // Arrange
            // Use a grace period that's shorter than the cleanup timer interval (1 second)
            // to ensure at least one cleanup cycle will run during our test
            var shortGracePeriod = TimeSpan.FromMilliseconds(500);
            var registry = new CancellableTaskRegistry(_loggerMock.Object, shortGracePeriod);
            var activeTaskId = "active-task";
            var cancelledTaskId = "cancelled-task";
            
            using var activeCts = new CancellationTokenSource();
            using var cancelledCts = new CancellationTokenSource();
            
            try
            {
                registry.RegisterTask(activeTaskId, activeCts);
                registry.RegisterTask(cancelledTaskId, cancelledCts);
                
                // Act - Cancel only one task
                cancelledCts.Cancel();
                
                // The cleanup timer runs every second, and we need to wait for:
                // 1. The grace period (500ms) to expire
                // 2. The next cleanup timer execution (up to 1 second)
                // Total maximum wait time: 1.5 seconds + buffer
                
                // Wait up to 3 seconds (60 iterations of 50ms each)
                var pollInterval = TimeSpan.FromMilliseconds(50);
                var maxIterations = 60;
                var iteration = 0;
                
                while (iteration < maxIterations)
                {
                    iteration++;
                    
                    // Check if the cancelled task has been removed
                    var cancelledTaskExists = registry.TryGetCancellationToken(cancelledTaskId, out _);
                    if (!cancelledTaskExists)
                    {
                        // Found the task was removed, now verify the active task is still there
                        registry.TryGetCancellationToken(activeTaskId, out _).Should().BeTrue("active task should not be removed");
                        return; // Test passed
                    }
                    
                    await Task.Delay(pollInterval);
                }
                
                // If we get here, the cancelled task was not removed in time
                throw new TimeoutException($"Cancelled task was not removed after grace period + cleanup cycle (waited {iteration * 50}ms)");
            }
            finally
            {
                registry.Dispose();
            }
        }

        public new void Dispose()
        {
            _registry?.Dispose();
            base.Dispose();
        }
    }
}
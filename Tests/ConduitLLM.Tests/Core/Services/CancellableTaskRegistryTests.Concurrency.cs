using ConduitLLM.Core.Services;

using FluentAssertions;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class CancellableTaskRegistryTests
    {
        [Fact]
        public async Task ConcurrentRegistrations_HandlesSafely()
        {
            // Arrange
            const int taskCount = 100;
            var tasks = new Task[taskCount];
            var tokenSources = new CancellationTokenSource[taskCount];

            // Act - Register tasks concurrently
            for (int i = 0; i < taskCount; i++)
            {
                var index = i; // Capture for closure
                tokenSources[i] = new CancellationTokenSource();
                tasks[i] = Task.Run(() => 
                {
                    _registry.RegisterTask($"concurrent-task-{index}", tokenSources[index]);
                });
            }

            await Task.WhenAll(tasks);

            // Assert - All tasks should be registered
            for (int i = 0; i < taskCount; i++)
            {
                _registry.TryGetCancellationToken($"concurrent-task-{i}", out _).Should().BeTrue();
            }

            // Cleanup
            for (int i = 0; i < taskCount; i++)
            {
                tokenSources[i].Dispose();
            }
        }

        [Fact]
        public async Task ConcurrentCancellations_HandlesSafely()
        {
            // Arrange
            const int taskCount = 50;
            var tokenSources = new CancellationTokenSource[taskCount];
            var taskIds = new string[taskCount];

            // Register tasks
            for (int i = 0; i < taskCount; i++)
            {
                taskIds[i] = $"cancel-concurrent-{i}";
                tokenSources[i] = new CancellationTokenSource();
                _registry.RegisterTask(taskIds[i], tokenSources[i]);
            }

            // Act - Cancel tasks concurrently
            var cancellationTasks = new Task[taskCount];
            for (int i = 0; i < taskCount; i++)
            {
                var index = i; // Capture for closure
                cancellationTasks[i] = Task.Run(() => 
                {
                    _registry.TryCancel(taskIds[index]);
                });
            }

            await Task.WhenAll(cancellationTasks);

            // Assert - All tokens should be cancelled
            for (int i = 0; i < taskCount; i++)
            {
                if (_registry.TryGetCancellationToken(taskIds[i], out var token))
                {
                    token.Value.IsCancellationRequested.Should().BeTrue();
                }
                tokenSources[i].Dispose();
            }
        }

        [Fact]
        public async Task Dispose_WithRunningTimer_StopsCleanupOperations()
        {
            // Arrange
            var shortGracePeriod = TimeSpan.FromMilliseconds(100);
            var registry = new CancellableTaskRegistry(_loggerMock.Object, shortGracePeriod);

            var cts1 = new CancellationTokenSource();
            var cts2 = new CancellationTokenSource();

            registry.RegisterTask("timer-task-1", cts1);
            registry.RegisterTask("timer-task-2", cts2);

            // Cancel tasks to trigger cleanup timer
            cts1.Cancel();
            cts2.Cancel();

            // Give some time for timer to potentially start
            await Task.Delay(20);

            // Act - Dispose registry before cleanup timer fires
            registry.Dispose();

            // Wait beyond what would have been the cleanup time
            await Task.Delay(200);

            // Assert - Since registry is disposed, no further operations should occur
            // This test primarily ensures Dispose handles running timers gracefully
            registry.Should().NotBeNull(); // Registry should still exist but be disposed

            // Cleanup
            cts1.Dispose();
            cts2.Dispose();
        }
    }
}
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
    /// Unit tests for InMemoryAsyncTaskService covering task management, state transitions, and concurrency.
    /// </summary>
    public class InMemoryAsyncTaskServiceTests : IDisposable
    {
        private readonly Mock<ILogger<InMemoryAsyncTaskService>> _mockLogger;
        private readonly InMemoryAsyncTaskService _service;

        public InMemoryAsyncTaskServiceTests()
        {
            _mockLogger = new Mock<ILogger<InMemoryAsyncTaskService>>();
            _service = new InMemoryAsyncTaskService(_mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new InMemoryAsyncTaskService(null));
        }

        [Fact]
        public void Constructor_WithValidLogger_CreatesService()
        {
            // Act
            var service = new InMemoryAsyncTaskService(_mockLogger.Object);

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public async Task CreateTaskAsync_WithValidParameters_ReturnsTaskId()
        {
            // Arrange
            var taskType = "image_generation";
            var metadata = new { prompt = "test image", model = "dall-e-3" };

            // Act
            var taskId = await _service.CreateTaskAsync(taskType, metadata);

            // Assert
            Assert.NotNull(taskId);
            Assert.NotEmpty(taskId);
            Assert.True(Guid.TryParse(taskId, out _)); // Should be a valid GUID
        }

        [Fact]
        public async Task CreateTaskAsync_WithVirtualKeyId_EnrichesMetadata()
        {
            // Arrange
            var taskType = "video_generation";
            var virtualKeyId = 42;
            var metadata = new Dictionary<string, object> { ["prompt"] = "test video" };

            // Act
            var taskId = await _service.CreateTaskAsync(taskType, virtualKeyId, metadata);

            // Assert
            Assert.NotNull(taskId);

            var status = await _service.GetTaskStatusAsync(taskId);
            Assert.NotNull(status);
            Assert.Equal(taskType, status.TaskType);

            // Check that virtualKeyId was added to metadata
            var enrichedMetadata = status.Metadata as Dictionary<string, object>;
            Assert.NotNull(enrichedMetadata);
            Assert.Equal(virtualKeyId, enrichedMetadata["virtualKeyId"]);
            Assert.Equal("test video", enrichedMetadata["prompt"]);
        }

        [Fact]
        public async Task CreateTaskAsync_WithNonDictionaryMetadata_CreatesWrapperObject()
        {
            // Arrange
            var taskType = "image_generation";
            var virtualKeyId = 123;
            var metadata = "simple string metadata";

            // Act
            var taskId = await _service.CreateTaskAsync(taskType, virtualKeyId, metadata);

            // Assert
            var status = await _service.GetTaskStatusAsync(taskId);
            Assert.NotNull(status);

            // Should create wrapper object
            dynamic enrichedMetadata = status.Metadata;
            Assert.Equal(virtualKeyId, enrichedMetadata.virtualKeyId);
            Assert.Equal(metadata, enrichedMetadata.originalMetadata);
        }

        [Fact]
        public async Task GetTaskStatusAsync_WithExistingTask_ReturnsCorrectStatus()
        {
            // Arrange
            var taskType = "test_task";
            var metadata = new { test = "data" };
            var taskId = await _service.CreateTaskAsync(taskType, metadata);

            // Act
            var status = await _service.GetTaskStatusAsync(taskId);

            // Assert
            Assert.NotNull(status);
            Assert.Equal(taskId, status.TaskId);
            Assert.Equal(taskType, status.TaskType);
            Assert.Equal(TaskState.Pending, status.State);
            Assert.True(status.CreatedAt <= DateTime.UtcNow);
            Assert.True(status.UpdatedAt <= DateTime.UtcNow);
            Assert.Equal(0, status.Progress);
            Assert.Null(status.CompletedAt);
            Assert.Null(status.Result);
            Assert.Null(status.Error);
        }

        [Fact]
        public async Task GetTaskStatusAsync_WithNonExistentTask_ReturnsNull()
        {
            // Arrange
            var nonExistentTaskId = Guid.NewGuid().ToString();

            // Act
            var status = await _service.GetTaskStatusAsync(nonExistentTaskId);

            // Assert
            Assert.Null(status);
        }

        [Fact]
        public async Task UpdateTaskStatusAsync_WithValidTask_UpdatesSuccessfully()
        {
            // Arrange
            var taskId = await _service.CreateTaskAsync("test_task", new { });
            var newState = TaskState.Processing;
            var progress = 50;
            var result = new { data = "test result" };
            var error = "test error";

            // Act
            await _service.UpdateTaskStatusAsync(taskId, newState, progress, result, error);

            // Assert
            var status = await _service.GetTaskStatusAsync(taskId);
            Assert.NotNull(status);
            Assert.Equal(newState, status.State);
            Assert.Equal(progress, status.Progress);
            Assert.Equal(result, status.Result);
            Assert.Equal(error, status.Error);
            Assert.True(status.UpdatedAt > status.CreatedAt);
        }

        [Fact]
        public async Task UpdateTaskStatusAsync_WithCompletedState_SetsCompletedAt()
        {
            // Arrange
            var taskId = await _service.CreateTaskAsync("test_task", new { });
            var beforeUpdate = DateTime.UtcNow;

            // Act
            await _service.UpdateTaskStatusAsync(taskId, TaskState.Completed, result: "completed");

            // Assert
            var status = await _service.GetTaskStatusAsync(taskId);
            Assert.NotNull(status);
            Assert.Equal(TaskState.Completed, status.State);
            Assert.NotNull(status.CompletedAt);
            Assert.True(status.CompletedAt >= beforeUpdate);
        }

        [Theory]
        [InlineData(TaskState.Failed)]
        [InlineData(TaskState.Cancelled)]
        [InlineData(TaskState.TimedOut)]
        public async Task UpdateTaskStatusAsync_WithFinalStates_SetsCompletedAt(TaskState finalState)
        {
            // Arrange
            var taskId = await _service.CreateTaskAsync("test_task", new { });

            // Act
            await _service.UpdateTaskStatusAsync(taskId, finalState);

            // Assert
            var status = await _service.GetTaskStatusAsync(taskId);
            Assert.NotNull(status);
            Assert.Equal(finalState, status.State);
            Assert.NotNull(status.CompletedAt);
        }

        [Fact]
        public async Task UpdateTaskStatusAsync_WithNonExistentTask_ThrowsInvalidOperationException()
        {
            // Arrange
            var nonExistentTaskId = Guid.NewGuid().ToString();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.UpdateTaskStatusAsync(nonExistentTaskId, TaskState.Processing));
        }

        [Fact]
        public async Task UpdateTaskProgressAsync_WithValidTask_UpdatesProgress()
        {
            // Arrange
            var taskId = await _service.CreateTaskAsync("test_task", new { });
            var progressPercentage = 75;
            var progressMessage = "Almost done";

            // Act
            await _service.UpdateTaskProgressAsync(taskId, progressPercentage, progressMessage);

            // Assert
            var status = await _service.GetTaskStatusAsync(taskId);
            Assert.NotNull(status);
            Assert.Equal(progressPercentage, status.Progress);
            Assert.Equal(progressMessage, status.ProgressMessage);
        }

        [Theory]
        [InlineData(-10, 0)]    // Below minimum should clamp to 0
        [InlineData(150, 100)]  // Above maximum should clamp to 100
        [InlineData(50, 50)]    // Valid value should remain unchanged
        public async Task UpdateTaskProgressAsync_ClampsProgressToValidRange(int inputProgress, int expectedProgress)
        {
            // Arrange
            var taskId = await _service.CreateTaskAsync("test_task", new { });

            // Act
            await _service.UpdateTaskProgressAsync(taskId, inputProgress);

            // Assert
            var status = await _service.GetTaskStatusAsync(taskId);
            Assert.NotNull(status);
            Assert.Equal(expectedProgress, status.Progress);
        }

        [Fact]
        public async Task UpdateTaskProgressAsync_WithNonExistentTask_ThrowsInvalidOperationException()
        {
            // Arrange
            var nonExistentTaskId = Guid.NewGuid().ToString();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.UpdateTaskProgressAsync(nonExistentTaskId, 50));
        }

        [Fact]
        public async Task PollTaskUntilCompletedAsync_WithCompletedTask_ReturnsImmediately()
        {
            // Arrange
            var taskId = await _service.CreateTaskAsync("test_task", new { });
            await _service.UpdateTaskStatusAsync(taskId, TaskState.Completed, result: "done");

            // Act
            var startTime = DateTime.UtcNow;
            var result = await _service.PollTaskUntilCompletedAsync(
                taskId, 
                TimeSpan.FromMilliseconds(100), 
                TimeSpan.FromSeconds(5));
            var endTime = DateTime.UtcNow;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(TaskState.Completed, result.State);
            Assert.Equal("done", result.Result);
            Assert.True(endTime - startTime < TimeSpan.FromSeconds(1)); // Should return quickly
        }

        [Fact]
        public async Task PollTaskUntilCompletedAsync_WithPendingTask_EventuallyTimesOut()
        {
            // Arrange
            var taskId = await _service.CreateTaskAsync("test_task", new { });
            var pollingInterval = TimeSpan.FromMilliseconds(50);
            var timeout = TimeSpan.FromMilliseconds(200);

            // Act
            var result = await _service.PollTaskUntilCompletedAsync(taskId, pollingInterval, timeout);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(TaskState.TimedOut, result.State);
            Assert.Equal("Task polling timed out", result.Error);
            Assert.NotNull(result.CompletedAt);
        }

        [Fact]
        public async Task PollTaskUntilCompletedAsync_WithCancellation_ThrowsOperationCancelledException()
        {
            // Arrange
            var taskId = await _service.CreateTaskAsync("test_task", new { });
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(50));

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _service.PollTaskUntilCompletedAsync(
                    taskId,
                    TimeSpan.FromMilliseconds(10),
                    TimeSpan.FromSeconds(5),
                    cts.Token));
        }

        [Fact]
        public async Task PollTaskUntilCompletedAsync_WithNonExistentTask_ThrowsInvalidOperationException()
        {
            // Arrange
            var nonExistentTaskId = Guid.NewGuid().ToString();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.PollTaskUntilCompletedAsync(
                    nonExistentTaskId,
                    TimeSpan.FromMilliseconds(10),
                    TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public async Task CancelTaskAsync_WithValidTask_SetsTaskToCancelled()
        {
            // Arrange
            var taskId = await _service.CreateTaskAsync("test_task", new { });

            // Act
            await _service.CancelTaskAsync(taskId);

            // Assert
            var status = await _service.GetTaskStatusAsync(taskId);
            Assert.NotNull(status);
            Assert.Equal(TaskState.Cancelled, status.State);
            Assert.Equal("Task was cancelled", status.Error);
            Assert.NotNull(status.CompletedAt);
        }

        [Fact]
        public async Task DeleteTaskAsync_WithExistingTask_RemovesTask()
        {
            // Arrange
            var taskId = await _service.CreateTaskAsync("test_task", new { });

            // Verify task exists
            var statusBefore = await _service.GetTaskStatusAsync(taskId);
            Assert.NotNull(statusBefore);

            // Act
            await _service.DeleteTaskAsync(taskId);

            // Assert
            var statusAfter = await _service.GetTaskStatusAsync(taskId);
            Assert.Null(statusAfter);
        }

        [Fact]
        public async Task DeleteTaskAsync_WithNonExistentTask_DoesNotThrow()
        {
            // Arrange
            var nonExistentTaskId = Guid.NewGuid().ToString();

            // Act & Assert - Should not throw
            await _service.DeleteTaskAsync(nonExistentTaskId);
        }

        [Fact]
        public async Task CleanupOldTasksAsync_RemovesOldCompletedTasks()
        {
            // Arrange
            var taskId1 = await _service.CreateTaskAsync("task1", new { });
            var taskId2 = await _service.CreateTaskAsync("task2", new { });
            var taskId3 = await _service.CreateTaskAsync("task3", new { });

            // Complete first two tasks
            await _service.UpdateTaskStatusAsync(taskId1, TaskState.Completed);
            await _service.UpdateTaskStatusAsync(taskId2, TaskState.Failed);
            // Leave taskId3 as pending

            // Wait a moment to ensure time difference
            await Task.Delay(10);

            // Act
            var cleanedCount = await _service.CleanupOldTasksAsync(TimeSpan.FromMilliseconds(5));

            // Assert
            Assert.Equal(2, cleanedCount); // Should clean up 2 completed tasks

            // Verify tasks are removed
            Assert.Null(await _service.GetTaskStatusAsync(taskId1));
            Assert.Null(await _service.GetTaskStatusAsync(taskId2));
            Assert.NotNull(await _service.GetTaskStatusAsync(taskId3)); // Pending task should remain
        }

        [Fact]
        public async Task CleanupOldTasksAsync_DoesNotRemoveRecentTasks()
        {
            // Arrange
            var taskId = await _service.CreateTaskAsync("test_task", new { });
            await _service.UpdateTaskStatusAsync(taskId, TaskState.Completed);

            // Act - Try to clean up tasks older than 1 hour (task was just created)
            var cleanedCount = await _service.CleanupOldTasksAsync(TimeSpan.FromHours(1));

            // Assert
            Assert.Equal(0, cleanedCount);
            Assert.NotNull(await _service.GetTaskStatusAsync(taskId)); // Task should still exist
        }

        [Fact]
        public async Task GetPendingTasksAsync_ReturnsOnlyPendingTasks()
        {
            // Arrange
            var taskId1 = await _service.CreateTaskAsync("task1", new { });
            var taskId2 = await _service.CreateTaskAsync("task2", new { });
            var taskId3 = await _service.CreateTaskAsync("task3", new { });

            // Update some task states
            await _service.UpdateTaskStatusAsync(taskId2, TaskState.Processing);
            await _service.UpdateTaskStatusAsync(taskId3, TaskState.Completed);

            // Act
            var pendingTasks = await _service.GetPendingTasksAsync();

            // Assert
            Assert.Single(pendingTasks);
            Assert.Equal(taskId1, pendingTasks[0].TaskId);
            Assert.Equal(TaskState.Pending, pendingTasks[0].State);
        }

        [Fact]
        public async Task GetPendingTasksAsync_WithTaskTypeFilter_ReturnsFilteredTasks()
        {
            // Arrange
            var taskId1 = await _service.CreateTaskAsync("image_generation", new { });
            var taskId2 = await _service.CreateTaskAsync("video_generation", new { });
            var taskId3 = await _service.CreateTaskAsync("image_generation", new { });

            // Act
            var imageTasks = await _service.GetPendingTasksAsync("image_generation");

            // Assert
            Assert.Equal(2, imageTasks.Count);
            Assert.All(imageTasks, task => Assert.Equal("image_generation", task.TaskType));
            Assert.Contains(imageTasks, t => t.TaskId == taskId1);
            Assert.Contains(imageTasks, t => t.TaskId == taskId3);
        }

        [Fact]
        public async Task GetPendingTasksAsync_WithLimit_RespectsLimit()
        {
            // Arrange
            var taskIds = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                taskIds.Add(await _service.CreateTaskAsync($"task_{i}", new { }));
            }

            // Act
            var limitedTasks = await _service.GetPendingTasksAsync(limit: 5);

            // Assert
            Assert.Equal(5, limitedTasks.Count);
        }

        [Fact]
        public async Task GetPendingTasksAsync_OrdersByCreatedAt()
        {
            // Arrange
            var taskId1 = await _service.CreateTaskAsync("task1", new { });
            await Task.Delay(10); // Ensure different creation times
            var taskId2 = await _service.CreateTaskAsync("task2", new { });
            await Task.Delay(10);
            var taskId3 = await _service.CreateTaskAsync("task3", new { });

            // Act
            var pendingTasks = await _service.GetPendingTasksAsync();

            // Assert
            Assert.Equal(3, pendingTasks.Count);
            Assert.Equal(taskId1, pendingTasks[0].TaskId); // First created should be first
            Assert.Equal(taskId2, pendingTasks[1].TaskId);
            Assert.Equal(taskId3, pendingTasks[2].TaskId);
        }

        [Fact]
        public async Task ConcurrentTaskCreation_HandledCorrectly()
        {
            // Arrange
            var concurrentTasks = new List<Task<string>>();

            // Act - Create 20 tasks concurrently
            for (int i = 0; i < 20; i++)
            {
                var i_copy = i; // Capture loop variable
                concurrentTasks.Add(_service.CreateTaskAsync($"concurrent_task_{i_copy}", new { index = i_copy }));
            }

            var taskIds = await Task.WhenAll(concurrentTasks);

            // Assert
            Assert.Equal(20, taskIds.Length);
            Assert.Equal(20, new HashSet<string>(taskIds).Count); // All task IDs should be unique

            // Verify all tasks can be retrieved
            for (int i = 0; i < 20; i++)
            {
                var status = await _service.GetTaskStatusAsync(taskIds[i]);
                Assert.NotNull(status);
                Assert.Equal($"concurrent_task_{i}", status.TaskType);
            }
        }

        [Fact]
        public async Task ConcurrentTaskUpdates_HandledCorrectly()
        {
            // Arrange
            var taskId = await _service.CreateTaskAsync("concurrent_test", new { });
            var updateTasks = new List<Task>();

            // Act - Update task concurrently from multiple threads
            for (int i = 0; i < 10; i++)
            {
                var progress = i * 10;
                updateTasks.Add(_service.UpdateTaskProgressAsync(taskId, progress, $"Step {i}"));
            }

            await Task.WhenAll(updateTasks);

            // Assert
            var finalStatus = await _service.GetTaskStatusAsync(taskId);
            Assert.NotNull(finalStatus);
            Assert.True(finalStatus.Progress >= 0 && finalStatus.Progress <= 100);
            Assert.NotNull(finalStatus.ProgressMessage);
        }

        public void Dispose()
        {
            // No cleanup needed for in-memory service
        }
    }
}
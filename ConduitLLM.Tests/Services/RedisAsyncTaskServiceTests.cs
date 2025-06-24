using System;
using System.Collections.Generic;
using System.Text.Json;
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
    /// Unit tests for RedisAsyncTaskService covering Redis-based task management and distributed scenarios.
    /// </summary>
    public class RedisAsyncTaskServiceTests : IDisposable
    {
        private readonly Mock<IConnectionMultiplexer> _mockRedis;
        private readonly Mock<IDatabase> _mockDatabase;
        private readonly Mock<ILogger<RedisAsyncTaskService>> _mockLogger;
        private readonly RedisAsyncTaskService _service;
        private readonly JsonSerializerOptions _jsonOptions;

        public RedisAsyncTaskServiceTests()
        {
            _mockRedis = new Mock<IConnectionMultiplexer>();
            _mockDatabase = new Mock<IDatabase>();
            _mockLogger = new Mock<ILogger<RedisAsyncTaskService>>();

            _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_mockDatabase.Object);

            _service = new RedisAsyncTaskService(_mockRedis.Object, _mockLogger.Object);

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        [Fact]
        public void Constructor_WithNullRedis_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new RedisAsyncTaskService(null, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new RedisAsyncTaskService(_mockRedis.Object, null));
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesService()
        {
            // Act
            var service = new RedisAsyncTaskService(_mockRedis.Object, _mockLogger.Object);

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public async Task CreateTaskAsync_WithValidParameters_CreatesTaskInRedis()
        {
            // Arrange
            var taskType = "image_generation";
            var metadata = new { prompt = "test image", model = "dall-e-3" };

            _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(), 
                It.IsAny<RedisValue>(), 
                It.IsAny<TimeSpan?>(), 
                It.IsAny<bool>(), 
                It.IsAny<When>(), 
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            _mockDatabase.Setup(db => db.SetAddAsync(
                It.IsAny<RedisKey>(), 
                It.IsAny<RedisValue>(), 
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            var taskId = await _service.CreateTaskAsync(taskType, metadata);

            // Assert
            Assert.NotNull(taskId);
            Assert.NotEmpty(taskId);
            Assert.True(Guid.TryParse(taskId, out _));

            // Verify Redis operations
            _mockDatabase.Verify(db => db.StringSetAsync(
                It.Is<RedisKey>(k => k.ToString().StartsWith("conduit:tasks:") && k.ToString().EndsWith(taskId)),
                It.IsAny<RedisValue>(),
                TimeSpan.FromHours(24),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()), Times.Once);

            _mockDatabase.Verify(db => db.SetAddAsync(
                It.Is<RedisKey>(k => k.ToString() == "conduit:tasks:index"),
                It.Is<RedisValue>(v => v.ToString() == taskId),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task CreateTaskAsync_WithVirtualKeyId_EnrichesMetadata()
        {
            // Arrange
            var taskType = "video_generation";
            var virtualKeyId = 42;
            var metadata = new Dictionary<string, object> { ["prompt"] = "test video" };
            string capturedJson = "";

            _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(), 
                It.IsAny<RedisValue>(), 
                It.IsAny<TimeSpan?>(), 
                It.IsAny<bool>(), 
                It.IsAny<When>(), 
                It.IsAny<CommandFlags>()))
                .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>((key, value, expiry, keepTtl, when, flags) =>
                {
                    capturedJson = value.ToString();
                })
                .ReturnsAsync(true);

            _mockDatabase.Setup(db => db.SetAddAsync(
                It.IsAny<RedisKey>(), 
                It.IsAny<RedisValue>(), 
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            var taskId = await _service.CreateTaskAsync(taskType, virtualKeyId, metadata);

            // Assert
            Assert.NotNull(taskId);

            // Verify the metadata was enriched with virtualKeyId
            var deserializedTask = JsonSerializer.Deserialize<AsyncTaskStatus>(capturedJson, _jsonOptions);
            Assert.NotNull(deserializedTask);
            Assert.Equal(taskType, deserializedTask.TaskType);

            var enrichedMetadata = deserializedTask.Metadata as JsonElement?;
            Assert.True(enrichedMetadata.HasValue);
        }

        [Fact]
        public async Task GetTaskStatusAsync_WithExistingTask_ReturnsTask()
        {
            // Arrange
            var taskId = Guid.NewGuid().ToString();
            var expectedTask = new AsyncTaskStatus
            {
                TaskId = taskId,
                TaskType = "test_task",
                State = TaskState.Processing,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Progress = 50
            };

            var json = JsonSerializer.Serialize(expectedTask, _jsonOptions);
            _mockDatabase.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"conduit:tasks:{taskId}"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue(json));

            // Act
            var result = await _service.GetTaskStatusAsync(taskId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(taskId, result.TaskId);
            Assert.Equal("test_task", result.TaskType);
            Assert.Equal(TaskState.Processing, result.State);
            Assert.Equal(50, result.Progress);
        }

        [Fact]
        public async Task GetTaskStatusAsync_WithNonExistentTask_ReturnsNull()
        {
            // Arrange
            var taskId = Guid.NewGuid().ToString();
            _mockDatabase.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"conduit:tasks:{taskId}"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);

            // Act
            var result = await _service.GetTaskStatusAsync(taskId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task UpdateTaskStatusAsync_WithValidTask_UpdatesRedis()
        {
            // Arrange
            var taskId = Guid.NewGuid().ToString();
            var existingTask = new AsyncTaskStatus
            {
                TaskId = taskId,
                TaskType = "test_task",
                State = TaskState.Pending,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
            };

            var existingJson = JsonSerializer.Serialize(existingTask, _jsonOptions);
            string updatedJson = "";

            // Setup mocks for get then set operations
            _mockDatabase.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"conduit:tasks:{taskId}"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue(existingJson));

            _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(), 
                It.IsAny<RedisValue>(), 
                It.IsAny<TimeSpan?>(), 
                It.IsAny<bool>(), 
                It.IsAny<When>(), 
                It.IsAny<CommandFlags>()))
                .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>((key, value, expiry, keepTtl, when, flags) =>
                {
                    updatedJson = value.ToString();
                })
                .ReturnsAsync(true);

            // Act
            await _service.UpdateTaskStatusAsync(taskId, TaskState.Completed, 100, "test result", null);

            // Assert
            var updatedTask = JsonSerializer.Deserialize<AsyncTaskStatus>(updatedJson, _jsonOptions);
            Assert.NotNull(updatedTask);
            Assert.Equal(TaskState.Completed, updatedTask.State);
            Assert.Equal(100, updatedTask.Progress);
            Assert.Equal("test result", updatedTask.Result?.ToString());
            Assert.NotNull(updatedTask.CompletedAt);

            // Verify Redis set was called with correct expiration for completed task
            _mockDatabase.Verify(db => db.StringSetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"conduit:tasks:{taskId}"),
                It.IsAny<RedisValue>(),
                TimeSpan.FromHours(2), // Completed tasks have 2-hour expiration
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task UpdateTaskStatusAsync_WithNonExistentTask_ThrowsInvalidOperationException()
        {
            // Arrange
            var taskId = Guid.NewGuid().ToString();
            _mockDatabase.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"conduit:tasks:{taskId}"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.UpdateTaskStatusAsync(taskId, TaskState.Processing));
        }

        [Fact]
        public async Task UpdateTaskProgressAsync_WithValidTask_UpdatesProgress()
        {
            // Arrange
            var taskId = Guid.NewGuid().ToString();
            var existingTask = new AsyncTaskStatus
            {
                TaskId = taskId,
                TaskType = "test_task",
                State = TaskState.Processing,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5),
                Progress = 25
            };

            var existingJson = JsonSerializer.Serialize(existingTask, _jsonOptions);
            string updatedJson = "";

            _mockDatabase.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"conduit:tasks:{taskId}"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue(existingJson));

            _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(), 
                It.IsAny<RedisValue>(), 
                It.IsAny<TimeSpan?>(), 
                It.IsAny<bool>(), 
                It.IsAny<When>(), 
                It.IsAny<CommandFlags>()))
                .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>((key, value, expiry, keepTtl, when, flags) =>
                {
                    updatedJson = value.ToString();
                })
                .ReturnsAsync(true);

            // Act
            await _service.UpdateTaskProgressAsync(taskId, 75, "Almost done");

            // Assert
            var updatedTask = JsonSerializer.Deserialize<AsyncTaskStatus>(updatedJson, _jsonOptions);
            Assert.NotNull(updatedTask);
            Assert.Equal(75, updatedTask.Progress);
            Assert.Equal("Almost done", updatedTask.ProgressMessage);
            Assert.True(updatedTask.UpdatedAt > existingTask.UpdatedAt);
        }

        [Theory]
        [InlineData(-10, 0)]    // Below minimum should clamp to 0
        [InlineData(150, 100)]  // Above maximum should clamp to 100
        [InlineData(50, 50)]    // Valid value should remain unchanged
        public async Task UpdateTaskProgressAsync_ClampsProgressToValidRange(int inputProgress, int expectedProgress)
        {
            // Arrange
            var taskId = Guid.NewGuid().ToString();
            var existingTask = new AsyncTaskStatus
            {
                TaskId = taskId,
                TaskType = "test_task",
                State = TaskState.Processing,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var existingJson = JsonSerializer.Serialize(existingTask, _jsonOptions);
            string updatedJson = "";

            _mockDatabase.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue(existingJson));

            _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), 
                It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>((key, value, expiry, keepTtl, when, flags) =>
                {
                    updatedJson = value.ToString();
                })
                .ReturnsAsync(true);

            // Act
            await _service.UpdateTaskProgressAsync(taskId, inputProgress);

            // Assert
            var updatedTask = JsonSerializer.Deserialize<AsyncTaskStatus>(updatedJson, _jsonOptions);
            Assert.NotNull(updatedTask);
            Assert.Equal(expectedProgress, updatedTask.Progress);
        }

        [Fact]
        public async Task CancelTaskAsync_WithValidTask_SetsTaskToCancelled()
        {
            // Arrange
            var taskId = Guid.NewGuid().ToString();
            var existingTask = new AsyncTaskStatus
            {
                TaskId = taskId,
                TaskType = "test_task",
                State = TaskState.Processing,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var existingJson = JsonSerializer.Serialize(existingTask, _jsonOptions);
            string updatedJson = "";

            _mockDatabase.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue(existingJson));

            _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), 
                It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>((key, value, expiry, keepTtl, when, flags) =>
                {
                    updatedJson = value.ToString();
                })
                .ReturnsAsync(true);

            // Act
            await _service.CancelTaskAsync(taskId);

            // Assert
            var updatedTask = JsonSerializer.Deserialize<AsyncTaskStatus>(updatedJson, _jsonOptions);
            Assert.NotNull(updatedTask);
            Assert.Equal(TaskState.Cancelled, updatedTask.State);
            Assert.Equal("Task was cancelled", updatedTask.Error);
            Assert.NotNull(updatedTask.CompletedAt);
        }

        [Fact]
        public async Task DeleteTaskAsync_WithValidTask_RemovesFromRedis()
        {
            // Arrange
            var taskId = Guid.NewGuid().ToString();

            _mockDatabase.Setup(db => db.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == $"conduit:tasks:{taskId}"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            _mockDatabase.Setup(db => db.SetRemoveAsync(
                It.Is<RedisKey>(k => k.ToString() == "conduit:tasks:index"),
                It.Is<RedisValue>(v => v.ToString() == taskId),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _service.DeleteTaskAsync(taskId);

            // Assert
            _mockDatabase.Verify(db => db.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == $"conduit:tasks:{taskId}"),
                It.IsAny<CommandFlags>()), Times.Once);

            _mockDatabase.Verify(db => db.SetRemoveAsync(
                It.Is<RedisKey>(k => k.ToString() == "conduit:tasks:index"),
                It.Is<RedisValue>(v => v.ToString() == taskId),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task CleanupOldTasksAsync_RemovesOldCompletedTasks()
        {
            // Arrange
            var taskId1 = Guid.NewGuid().ToString();
            var taskId2 = Guid.NewGuid().ToString();
            var taskId3 = Guid.NewGuid().ToString();

            var oldTime = DateTime.UtcNow.AddHours(-2);
            var recentTime = DateTime.UtcNow.AddMinutes(-5);

            var oldCompletedTask = new AsyncTaskStatus
            {
                TaskId = taskId1,
                State = TaskState.Completed,
                UpdatedAt = oldTime
            };

            var oldFailedTask = new AsyncTaskStatus
            {
                TaskId = taskId2,
                State = TaskState.Failed,
                UpdatedAt = oldTime
            };

            var recentTask = new AsyncTaskStatus
            {
                TaskId = taskId3,
                State = TaskState.Completed,
                UpdatedAt = recentTime
            };

            // Setup index with all task IDs
            _mockDatabase.Setup(db => db.SetMembersAsync(
                It.Is<RedisKey>(k => k.ToString() == "conduit:tasks:index"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue[] { taskId1, taskId2, taskId3 });

            // Setup individual task retrievals
            _mockDatabase.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"conduit:tasks:{taskId1}"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(JsonSerializer.Serialize(oldCompletedTask, _jsonOptions));

            _mockDatabase.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"conduit:tasks:{taskId2}"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(JsonSerializer.Serialize(oldFailedTask, _jsonOptions));

            _mockDatabase.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"conduit:tasks:{taskId3}"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(JsonSerializer.Serialize(recentTask, _jsonOptions));

            // Setup deletion operations
            _mockDatabase.Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);
            _mockDatabase.Setup(db => db.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            var cleanedCount = await _service.CleanupOldTasksAsync(TimeSpan.FromHours(1));

            // Assert
            Assert.Equal(2, cleanedCount);

            // Verify old tasks were deleted
            _mockDatabase.Verify(db => db.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == $"conduit:tasks:{taskId1}"),
                It.IsAny<CommandFlags>()), Times.Once);

            _mockDatabase.Verify(db => db.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == $"conduit:tasks:{taskId2}"),
                It.IsAny<CommandFlags>()), Times.Once);

            // Verify recent task was NOT deleted
            _mockDatabase.Verify(db => db.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == $"conduit:tasks:{taskId3}"),
                It.IsAny<CommandFlags>()), Times.Never);
        }

        [Fact]
        public async Task CleanupOldTasksAsync_RemovesOrphanedIndexEntries()
        {
            // Arrange
            var orphanedTaskId = Guid.NewGuid().ToString();

            _mockDatabase.Setup(db => db.SetMembersAsync(
                It.Is<RedisKey>(k => k.ToString() == "conduit:tasks:index"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue[] { orphanedTaskId });

            _mockDatabase.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"conduit:tasks:{orphanedTaskId}"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null); // Task doesn't exist

            _mockDatabase.Setup(db => db.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            var cleanedCount = await _service.CleanupOldTasksAsync(TimeSpan.FromHours(1));

            // Assert
            Assert.Equal(0, cleanedCount); // No tasks cleaned, but orphaned entry removed

            // Verify orphaned index entry was removed
            _mockDatabase.Verify(db => db.SetRemoveAsync(
                It.Is<RedisKey>(k => k.ToString() == "conduit:tasks:index"),
                It.Is<RedisValue>(v => v.ToString() == orphanedTaskId),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task GetPendingTasksAsync_ReturnsOnlyPendingTasks()
        {
            // Arrange
            var taskId1 = Guid.NewGuid().ToString();
            var taskId2 = Guid.NewGuid().ToString();
            var taskId3 = Guid.NewGuid().ToString();

            var pendingTask1 = new AsyncTaskStatus
            {
                TaskId = taskId1,
                TaskType = "task1",
                State = TaskState.Pending,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            };

            var processingTask = new AsyncTaskStatus
            {
                TaskId = taskId2,
                TaskType = "task2",
                State = TaskState.Processing,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            };

            var pendingTask2 = new AsyncTaskStatus
            {
                TaskId = taskId3,
                TaskType = "task3",
                State = TaskState.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _mockDatabase.Setup(db => db.SetMembersAsync(
                It.Is<RedisKey>(k => k.ToString() == "conduit:tasks:index"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue[] { taskId1, taskId2, taskId3 });

            _mockDatabase.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"conduit:tasks:{taskId1}"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(JsonSerializer.Serialize(pendingTask1, _jsonOptions));

            _mockDatabase.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"conduit:tasks:{taskId2}"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(JsonSerializer.Serialize(processingTask, _jsonOptions));

            _mockDatabase.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"conduit:tasks:{taskId3}"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(JsonSerializer.Serialize(pendingTask2, _jsonOptions));

            // Act
            var pendingTasks = await _service.GetPendingTasksAsync();

            // Assert
            Assert.Equal(2, pendingTasks.Count);
            Assert.Contains(pendingTasks, t => t.TaskId == taskId1);
            Assert.Contains(pendingTasks, t => t.TaskId == taskId3);
            Assert.DoesNotContain(pendingTasks, t => t.TaskId == taskId2);

            // Verify tasks are ordered by creation time
            Assert.Equal(taskId1, pendingTasks[0].TaskId); // Older task first
            Assert.Equal(taskId3, pendingTasks[1].TaskId);
        }

        [Fact]
        public async Task GetPendingTasksAsync_WithTaskTypeFilter_ReturnsFilteredTasks()
        {
            // Arrange
            var taskId1 = Guid.NewGuid().ToString();
            var taskId2 = Guid.NewGuid().ToString();

            var imageTask = new AsyncTaskStatus
            {
                TaskId = taskId1,
                TaskType = "image_generation",
                State = TaskState.Pending,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            };

            var videoTask = new AsyncTaskStatus
            {
                TaskId = taskId2,
                TaskType = "video_generation",
                State = TaskState.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _mockDatabase.Setup(db => db.SetMembersAsync(
                It.Is<RedisKey>(k => k.ToString() == "conduit:tasks:index"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue[] { taskId1, taskId2 });

            _mockDatabase.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"conduit:tasks:{taskId1}"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(JsonSerializer.Serialize(imageTask, _jsonOptions));

            _mockDatabase.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"conduit:tasks:{taskId2}"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(JsonSerializer.Serialize(videoTask, _jsonOptions));

            // Act
            var imageTasks = await _service.GetPendingTasksAsync("image_generation");

            // Assert
            Assert.Single(imageTasks);
            Assert.Equal(taskId1, imageTasks[0].TaskId);
            Assert.Equal("image_generation", imageTasks[0].TaskType);
        }

        [Fact]
        public async Task GetPendingTasksAsync_WithLimit_RespectsLimit()
        {
            // Arrange
            var taskIds = new List<string>();
            var tasks = new List<AsyncTaskStatus>();

            for (int i = 0; i < 10; i++)
            {
                var taskId = Guid.NewGuid().ToString();
                taskIds.Add(taskId);
                
                var task = new AsyncTaskStatus
                {
                    TaskId = taskId,
                    TaskType = $"task_{i}",
                    State = TaskState.Pending,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-i)
                };
                tasks.Add(task);

                _mockDatabase.Setup(db => db.StringGetAsync(
                    It.Is<RedisKey>(k => k.ToString() == $"conduit:tasks:{taskId}"),
                    It.IsAny<CommandFlags>()))
                    .ReturnsAsync(JsonSerializer.Serialize(task, _jsonOptions));
            }

            _mockDatabase.Setup(db => db.SetMembersAsync(
                It.Is<RedisKey>(k => k.ToString() == "conduit:tasks:index"),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(taskIds.Select(id => new RedisValue(id)).ToArray());

            // Act
            var limitedTasks = await _service.GetPendingTasksAsync(limit: 5);

            // Assert
            Assert.Equal(5, limitedTasks.Count);
        }

        public void Dispose()
        {
            // No cleanup needed for mocked services
        }
    }
}
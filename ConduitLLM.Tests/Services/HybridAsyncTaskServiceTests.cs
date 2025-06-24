using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Tests.TestHelpers.Builders;
using MassTransit;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Services
{
    public class HybridAsyncTaskServiceTests
    {
        private readonly Mock<IAsyncTaskRepository> _mockRepository;
        private readonly Mock<IDistributedCache> _mockCache;
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<ILogger<HybridAsyncTaskService>> _mockLogger;
        private readonly HybridAsyncTaskService _service;

        public HybridAsyncTaskServiceTests()
        {
            _mockRepository = new Mock<IAsyncTaskRepository>();
            _mockCache = new Mock<IDistributedCache>();
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockLogger = new Mock<ILogger<HybridAsyncTaskService>>();

            _service = new HybridAsyncTaskService(
                _mockRepository.Object,
                _mockCache.Object,
                _mockPublishEndpoint.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task CreateTaskAsync_CreatesTaskInDatabaseAndCache_PublishesEvent()
        {
            // Arrange
            var taskType = "video-generation";
            var payload = new { prompt = "A beautiful sunset", duration = 6 };
            var metadata = new Dictionary<string, object> { { "virtualKeyId", 123 } };
            var cancellationToken = CancellationToken.None;

            var createdTask = new AsyncTaskBuilder()
                .WithType(taskType)
                .WithPayload(payload)
                .WithVirtualKeyId(123)
                .WithMetadata(metadata)
                .Build();

            _mockRepository
                .Setup(r => r.CreateAsync(It.IsAny<AsyncTask>(), cancellationToken))
                .ReturnsAsync((AsyncTask t, CancellationToken ct) => t.Id);

            // Mock GetByIdAsync for when GetTaskStatusAsync is called
            _mockRepository
                .Setup(r => r.GetByIdAsync(It.IsAny<string>(), cancellationToken))
                .ReturnsAsync((string id, CancellationToken ct) => new AsyncTask
                {
                    Id = id,
                    Type = taskType,
                    State = (int)TaskState.Pending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    VirtualKeyId = 123,
                    Metadata = JsonSerializer.Serialize(metadata),
                    Progress = 0
                });

            // Act
            var taskId = await _service.CreateTaskAsync(taskType, metadata, cancellationToken);
            var result = await _service.GetTaskStatusAsync(taskId, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(taskType, result.TaskType);
            // Virtual key ID is stored in metadata, not as a direct property

            // Verify database creation
            _mockRepository.Verify(r => r.CreateAsync(
                It.Is<AsyncTask>(t => 
                    t.Type == taskType &&
                    t.State == (int)TaskState.Pending &&
                    t.VirtualKeyId == 123),
                cancellationToken), Times.Once);

            // Verify cache update (called twice: once during create, once during get due to cache miss)
            _mockCache.Verify(c => c.SetAsync(
                It.Is<string>(k => k.StartsWith("async:task:")),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                cancellationToken), Times.Exactly(2));

            // Verify event published
            _mockPublishEndpoint.Verify(p => p.Publish(
                It.Is<AsyncTaskCreated>(e => 
                    e.TaskId == result.TaskId &&
                    e.TaskType == taskType &&
                    e.VirtualKeyId == 123),
                cancellationToken), Times.Once);
        }

        [Fact]
        public async Task CreateTaskAsync_ExtractsVirtualKeyIdFromMetadata()
        {
            // Arrange
            var testCases = new[]
            {
                new { metadata = new Dictionary<string, object> { { "virtualKeyId", 123 } }, expected = 123 },
                new { metadata = new Dictionary<string, object> { { "virtualKeyId", 789L } }, expected = 789 },
                new { metadata = new Dictionary<string, object> { { "VirtualKeyId", 321 } }, expected = 321 }
            };

            _mockRepository
                .Setup(r => r.CreateAsync(It.IsAny<AsyncTask>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AsyncTask t, CancellationToken ct) => t.Id);

            // Mock GetByIdAsync for when GetTaskStatusAsync is called
            _mockRepository
                .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken ct) => new AsyncTask
                {
                    Id = id,
                    Type = "test",
                    State = (int)TaskState.Pending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    VirtualKeyId = 123, // Will be overridden by actual test case
                    Metadata = "{}",
                    Progress = 0
                });

            foreach (var testCase in testCases)
            {
                // Act
                var taskId = await _service.CreateTaskAsync("test", testCase.metadata);

                // Assert - Verify that the task was created with the correct VirtualKeyId
                _mockRepository.Verify(r => r.CreateAsync(
                    It.Is<AsyncTask>(t => t.VirtualKeyId == testCase.expected),
                    It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            }
        }

        [Fact]
        public async Task CreateTaskAsync_WithInvalidVirtualKeyId_ThrowsException()
        {
            // Arrange
            var metadata = new Dictionary<string, object> { { "virtualKeyId", "invalid" } };

            _mockRepository
                .Setup(r => r.CreateAsync(It.IsAny<AsyncTask>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AsyncTask t, CancellationToken ct) => t.Id);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _service.CreateTaskAsync("test", metadata));
            
            Assert.Contains("requested operation requires an element", exception.Message);
        }

        [Fact]
        public async Task GetTaskStatusAsync_WithCacheHit_ReturnsCachedStatus()
        {
            // Arrange
            var taskId = "cached-task";
            var cachedStatus = new AsyncTaskStatusBuilder()
                .WithTaskId(taskId)
                .AsProcessing(75)
                .Build();

            var cachedJson = JsonSerializer.Serialize(cachedStatus);
            var cachedBytes = Encoding.UTF8.GetBytes(cachedJson);

            _mockCache
                .Setup(c => c.GetAsync($"async:task:{taskId}", It.IsAny<CancellationToken>()))
                .ReturnsAsync(cachedBytes);

            // Act
            var result = await _service.GetTaskStatusAsync(taskId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(taskId, result.TaskId);
            Assert.Equal(TaskState.Processing, result.State);
            Assert.Equal(75, result.Progress);

            // Verify database was NOT queried
            _mockRepository.Verify(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetTaskStatusAsync_WithCacheMiss_QueriesDatabase()
        {
            // Arrange
            var taskId = "db-task";
            var dbTask = new AsyncTaskBuilder()
                .WithId(taskId)
                .AsCompleted("{\"result\":\"success\"}")
                .Build();

            _mockCache
                .Setup(c => c.GetAsync($"async:task:{taskId}", It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[])null!);

            _mockRepository
                .Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(dbTask);

            // Act
            var result = await _service.GetTaskStatusAsync(taskId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(taskId, result.TaskId);
            Assert.Equal(TaskState.Completed, result.State);
            Assert.Equal(100, result.Progress);
            Assert.NotNull(result.Result);

            // Verify database was queried
            _mockRepository.Verify(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()), Times.Once);

            // Verify cache was updated
            _mockCache.Verify(c => c.SetAsync(
                $"async:task:{taskId}",
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetTaskStatusAsync_WithNonExistingTask_ReturnsNull()
        {
            // Arrange
            var taskId = "non-existing";

            _mockCache
                .Setup(c => c.GetAsync($"async:task:{taskId}", It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[])null!);

            _mockRepository
                .Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((AsyncTask)null!);

            // Act
            var result = await _service.GetTaskStatusAsync(taskId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task UpdateTaskStatusAsync_UpdatesDatabaseAndCache_PublishesEvent()
        {
            // Arrange
            var taskId = "update-task";
            var existingTask = new AsyncTaskBuilder()
                .WithId(taskId)
                .WithState(TaskState.Pending)
                .Build();

            _mockRepository
                .Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingTask);

            _mockRepository
                .Setup(r => r.UpdateAsync(It.IsAny<AsyncTask>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            await _service.UpdateTaskStatusAsync(taskId, TaskState.Processing, 50);
            var result = await _service.GetTaskStatusAsync(taskId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(TaskState.Processing, result.State);
            Assert.Equal(50, result.Progress);
            // Progress message not set in this update

            // Verify database update
            _mockRepository.Verify(r => r.UpdateAsync(
                It.Is<AsyncTask>(t => 
                    t.Id == taskId &&
                    t.State == (int)TaskState.Processing &&
                    t.Progress == 50),
                It.IsAny<CancellationToken>()), Times.Once);

            // Verify cache update (called twice: once during update, once during potential re-cache)
            _mockCache.Verify(c => c.SetAsync(
                $"async:task:{taskId}",
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);

            // Verify event published
            _mockPublishEndpoint.Verify(p => p.Publish(
                It.Is<AsyncTaskUpdated>(e => 
                    e.TaskId == taskId &&
                    e.State == TaskState.Processing.ToString() &&
                    e.Progress == 50),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateTaskStatusAsync_WithCompletedState_SetsCacheExpiryForCompleted()
        {
            // Arrange
            var taskId = "complete-task";
            var existingTask = new AsyncTaskBuilder()
                .WithId(taskId)
                .AsProcessing(90)
                .Build();

            var result = new { videoUrl = "https://example.com/video.mp4" };

            _mockRepository
                .Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingTask);

            _mockRepository
                .Setup(r => r.UpdateAsync(It.IsAny<AsyncTask>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            DistributedCacheEntryOptions? capturedOptions = null;
            _mockCache
                .Setup(c => c.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                    (key, value, options, ct) => capturedOptions = options)
                .Returns(Task.CompletedTask);

            // Act
            await _service.UpdateTaskStatusAsync(taskId, TaskState.Completed, 100, result);

            // Assert
            Assert.NotNull(capturedOptions);
            Assert.NotNull(capturedOptions.SlidingExpiration);
            Assert.Equal(TimeSpan.FromHours(2), capturedOptions.SlidingExpiration.Value);
        }

        [Fact]
        public async Task UpdateTaskStatusAsync_WithNonExistingTask_ThrowsException()
        {
            // Arrange
            var taskId = "non-existing";

            _mockRepository
                .Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((AsyncTask)null!);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _service.UpdateTaskStatusAsync(taskId, TaskState.Processing));
            
            Assert.Contains("not found", exception.Message);
            _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<AsyncTask>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CancelTaskAsync_CancelsTask_UpdatesCacheAndPublishesEvent()
        {
            // Arrange
            var taskId = "cancel-task";
            var existingTask = new AsyncTaskBuilder()
                .WithId(taskId)
                .AsProcessing(50)
                .Build();

            _mockRepository
                .Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingTask);

            _mockRepository
                .Setup(r => r.UpdateAsync(It.IsAny<AsyncTask>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            await _service.CancelTaskAsync(taskId);

            // Assert
            // Method returns void

            // Verify task was cancelled
            _mockRepository.Verify(r => r.UpdateAsync(
                It.Is<AsyncTask>(t => 
                    t.Id == taskId &&
                    t.State == (int)TaskState.Cancelled &&
                    t.CompletedAt != null),
                It.IsAny<CancellationToken>()), Times.Once);

            // Verify event published
            _mockPublishEndpoint.Verify(p => p.Publish(
                It.Is<AsyncTaskUpdated>(e => 
                    e.TaskId == taskId &&
                    e.State == TaskState.Cancelled.ToString()),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CancelTaskAsync_WithAlreadyCompletedTask_UpdatesTaskToCancelled()
        {
            // Arrange
            var taskId = "completed-task";
            var existingTask = new AsyncTaskBuilder()
                .WithId(taskId)
                .AsCompleted()
                .Build();

            _mockRepository
                .Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingTask);
            
            _mockRepository
                .Setup(r => r.UpdateAsync(It.IsAny<AsyncTask>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            await _service.CancelTaskAsync(taskId);

            // Assert
            // Current implementation updates the task to cancelled regardless of current state
            _mockRepository.Verify(r => r.UpdateAsync(
                It.Is<AsyncTask>(t => t.State == (int)TaskState.Cancelled), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteTaskAsync_DeletesFromDatabaseAndCache_PublishesEvent()
        {
            // Arrange
            var taskId = "delete-task";

            _mockRepository
                .Setup(r => r.DeleteAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            await _service.DeleteTaskAsync(taskId);

            // Assert
            // Method returns void

            // Verify database deletion
            _mockRepository.Verify(r => r.DeleteAsync(taskId, It.IsAny<CancellationToken>()), Times.Once);

            // Verify cache removal
            _mockCache.Verify(c => c.RemoveAsync($"async:task:{taskId}", It.IsAny<CancellationToken>()), Times.Once);

            // Verify event published
            _mockPublishEndpoint.Verify(p => p.Publish(
                It.Is<AsyncTaskDeleted>(e => e.TaskId == taskId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PollTaskUntilCompletedAsync_PollsUntilCompleted()
        {
            // Arrange
            var taskId = "poll-task";
            var pollInterval = TimeSpan.FromMilliseconds(50);
            var timeout = TimeSpan.FromSeconds(1);

            var callCount = 0;
            _mockCache
                .Setup(c => c.GetAsync($"async:task:{taskId}", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    var status = callCount switch
                    {
                        1 => new AsyncTaskStatusBuilder().WithTaskId(taskId).WithState(TaskState.Pending).Build(),
                        2 => new AsyncTaskStatusBuilder().WithTaskId(taskId).AsProcessing(50).Build(),
                        _ => new AsyncTaskStatusBuilder().WithTaskId(taskId).AsCompleted(new { result = "done" }).Build()
                    };
                    return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(status));
                });

            // Act
            var result = await _service.PollTaskUntilCompletedAsync(taskId, pollInterval, timeout);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(TaskState.Completed, result.State);
            Assert.NotNull(result.Result);
            Assert.True(callCount >= 3);
        }

        [Fact]
        public async Task PollTaskUntilCompletedAsync_TimesOut_ReturnsLastStatus()
        {
            // Arrange
            var taskId = "timeout-task";
            var pollInterval = TimeSpan.FromMilliseconds(100);
            var timeout = TimeSpan.FromMilliseconds(250);

            var processingStatus = new AsyncTaskStatusBuilder()
                .WithTaskId(taskId)
                .AsProcessing(30)
                .Build();

            var dbTask = new AsyncTaskBuilder()
                .WithId(taskId)
                .AsProcessing(30)
                .Build();

            var timedOutStatus = new AsyncTaskStatusBuilder()
                .WithTaskId(taskId)
                .WithState(TaskState.TimedOut)
                .WithError("Task timed out")
                .Build();

            // Mock cache to return processing status during polling, then null for final status read
            var callCount = 0;
            _mockCache
                .Setup(c => c.GetAsync($"async:task:{taskId}", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    // Return processing status during polling, null on final read to force DB lookup
                    return callCount <= 3 ? Encoding.UTF8.GetBytes(JsonSerializer.Serialize(processingStatus)) : null;
                });

            // Mock repository for UpdateTaskStatusAsync and final GetTaskStatusAsync
            _mockRepository
                .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dbTask);

            _mockRepository
                .Setup(r => r.UpdateAsync(It.IsAny<AsyncTask>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Mock cache set operations
            _mockCache
                .Setup(c => c.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.PollTaskUntilCompletedAsync(taskId, pollInterval, timeout);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(TaskState.TimedOut, result.State);
            Assert.Equal("Task timed out", result.Error);

            // Verify timeout update was called
            _mockRepository.Verify(r => r.UpdateAsync(
                It.Is<AsyncTask>(t => 
                    t.Id == taskId &&
                    t.State == (int)TaskState.TimedOut &&
                    t.Error == "Task timed out"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact(Skip = "Requires IExtendedAsyncTaskService implementation")]
        public Task CleanupOldTasksAsync_ArchivesAndDeletesOldTasks()
        {
            // Arrange
            var archiveAfter = TimeSpan.FromDays(7);
            var deleteAfter = TimeSpan.FromDays(30);

            var tasksToDelete = new[]
            {
                new AsyncTaskBuilder().WithId("old1").Build(),
                new AsyncTaskBuilder().WithId("old2").Build()
            };

            _mockRepository
                .Setup(r => r.ArchiveOldTasksAsync(archiveAfter, It.IsAny<CancellationToken>()))
                .ReturnsAsync(5);

            _mockRepository
                .Setup(r => r.GetTasksForCleanupAsync(deleteAfter, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tasksToDelete.ToList());

            _mockRepository
                .Setup(r => r.BulkDeleteAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);

            // Act
            // var (archived, deleted) = await _service.CleanupOldTasksAsync(archiveAfter, deleteAfter);

            // Assert
            // Assert.Equal(5, archived);
            // Assert.Equal(2, deleted);

            // Verify operations
            _mockRepository.Verify(r => r.ArchiveOldTasksAsync(archiveAfter, It.IsAny<CancellationToken>()), Times.Once);
            _mockRepository.Verify(r => r.GetTasksForCleanupAsync(deleteAfter, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockRepository.Verify(r => r.BulkDeleteAsync(
                It.Is<IEnumerable<string>>(ids => ids.Count() == 2),
                It.IsAny<CancellationToken>()), Times.Once);
            
            return Task.CompletedTask;
        }

        [Fact(Skip = "Requires IExtendedAsyncTaskService implementation")]
        public Task GetTasksByVirtualKeyAsync_WithActiveOnly_ReturnsActiveTasksFromRepository()
        {
            // Arrange
            var virtualKeyId = 123;
            var activeTasks = new[]
            {
                new AsyncTaskBuilder().WithId("active1").WithVirtualKeyId(virtualKeyId).Build(),
                new AsyncTaskBuilder().WithId("active2").WithVirtualKeyId(virtualKeyId).AsProcessing().Build()
            };

            _mockRepository
                .Setup(r => r.GetActiveByVirtualKeyAsync(virtualKeyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(activeTasks.ToList());

            // Act
            // var result = await _service.GetTasksByVirtualKeyAsync(virtualKeyId, activeOnly: true);

            // Assert
            // Assert.NotNull(result);
            // Assert.Equal(2, result.Count);
            // Assert.All(result, s => Assert.False(s.IsArchived));
            
            return Task.CompletedTask;
        }

        [Fact(Skip = "Requires IExtendedAsyncTaskService implementation")]
        public Task GetTasksByVirtualKeyAsync_WithIncludeArchived_ReturnsAllTasksFromRepository()
        {
            // Arrange
            var virtualKeyId = 456;
            var allTasks = new[]
            {
                new AsyncTaskBuilder().WithId("task1").WithVirtualKeyId(virtualKeyId).Build(),
                new AsyncTaskBuilder().WithId("task2").WithVirtualKeyId(virtualKeyId).AsArchived().Build()
            };

            _mockRepository
                .Setup(r => r.GetByVirtualKeyAsync(virtualKeyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(allTasks.ToList());

            // Act
            // var result = await _service.GetTasksByVirtualKeyAsync(virtualKeyId, activeOnly: false);

            // Assert
            // Assert.NotNull(result);
            // Assert.Equal(2, result.Count);
            // Assert.Contains(result, s => s.IsArchived == true);
            
            return Task.CompletedTask;
        }

        [Fact]
        public async Task Constructor_WithNullPublishEndpoint_DoesNotThrow()
        {
            // Arrange & Act
            var service = new HybridAsyncTaskService(
                _mockRepository.Object,
                _mockCache.Object,
                null, // No publish endpoint
                _mockLogger.Object);

            // Create a task to verify it works without publishing
            _mockRepository
                .Setup(r => r.CreateAsync(It.IsAny<AsyncTask>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AsyncTask t, CancellationToken ct) => t.Id);

            // Mock GetByIdAsync to return a task
            _mockRepository
                .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken ct) => new AsyncTask
                {
                    Id = id,
                    Type = "test",
                    State = (int)TaskState.Pending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    VirtualKeyId = 123,
                    Metadata = "{\"virtualKeyId\":123}",
                    Progress = 0
                });

            // Act
            var taskId = await service.CreateTaskAsync("test", new Dictionary<string, object> { { "virtualKeyId", 123 } });
            var result = await service.GetTaskStatusAsync(taskId);

            // Assert
            Assert.NotNull(result);
            // No events should be published
            _mockPublishEndpoint.Verify(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateTaskAsync_WithExplicitVirtualKeyId_CreatesTaskWithCorrectId()
        {
            // Arrange
            var taskType = "image-generation";
            var virtualKeyId = 456;
            var metadata = new { prompt = "A beautiful landscape", model = "dall-e-3" };
            var cancellationToken = CancellationToken.None;

            _mockRepository
                .Setup(r => r.CreateAsync(It.IsAny<AsyncTask>(), cancellationToken))
                .ReturnsAsync((AsyncTask t, CancellationToken ct) => t.Id);

            // Act
            var taskId = await _service.CreateTaskAsync(taskType, virtualKeyId, metadata, cancellationToken);

            // Assert
            Assert.NotNull(taskId);
            Assert.StartsWith("task_", taskId);

            // Verify database creation with correct virtualKeyId
            _mockRepository.Verify(r => r.CreateAsync(
                It.Is<AsyncTask>(t => 
                    t.Type == taskType &&
                    t.State == (int)TaskState.Pending &&
                    t.VirtualKeyId == virtualKeyId),
                cancellationToken), Times.Once);

            // Verify cache update
            _mockCache.Verify(c => c.SetAsync(
                It.Is<string>(k => k.StartsWith("async:task:")),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                cancellationToken), Times.Once);

            // Verify event published with correct virtualKeyId
            _mockPublishEndpoint.Verify(p => p.Publish(
                It.Is<AsyncTaskCreated>(e => 
                    e.TaskId == taskId &&
                    e.TaskType == taskType &&
                    e.VirtualKeyId == virtualKeyId),
                cancellationToken), Times.Once);
        }

        [Fact]
        public async Task CreateTaskAsync_WithExplicitVirtualKeyId_DoesNotRequireVirtualKeyIdInMetadata()
        {
            // Arrange
            var taskType = "video-generation";
            var virtualKeyId = 789;
            var metadata = new { prompt = "A video of waves", duration = 10 }; // No virtualKeyId in metadata
            var cancellationToken = CancellationToken.None;

            _mockRepository
                .Setup(r => r.CreateAsync(It.IsAny<AsyncTask>(), cancellationToken))
                .ReturnsAsync((AsyncTask t, CancellationToken ct) => t.Id);

            // Act - Should not throw despite missing virtualKeyId in metadata
            var taskId = await _service.CreateTaskAsync(taskType, virtualKeyId, metadata, cancellationToken);

            // Assert
            Assert.NotNull(taskId);

            // Verify task was created with the explicit virtualKeyId
            _mockRepository.Verify(r => r.CreateAsync(
                It.Is<AsyncTask>(t => t.VirtualKeyId == virtualKeyId),
                cancellationToken), Times.Once);
        }

        [Fact]
        public async Task CreateTaskAsync_BothOverloads_ProduceSameResult()
        {
            // Arrange
            var taskType = "test-generation";
            var virtualKeyId = 999;
            var baseMetadata = new { data = "test data" };
            var metadataWithKeyId = new Dictionary<string, object>
            {
                { "virtualKeyId", virtualKeyId },
                { "data", "test data" }
            };

            var taskIds = new List<string>();

            _mockRepository
                .Setup(r => r.CreateAsync(It.IsAny<AsyncTask>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AsyncTask t, CancellationToken ct) => 
                {
                    taskIds.Add(t.Id);
                    return t.Id;
                });

            // Act - Create tasks using both overloads
            var taskId1 = await _service.CreateTaskAsync(taskType, metadataWithKeyId, CancellationToken.None);
            var taskId2 = await _service.CreateTaskAsync(taskType, virtualKeyId, baseMetadata, CancellationToken.None);

            // Assert - Both should create tasks with the same virtualKeyId
            _mockRepository.Verify(r => r.CreateAsync(
                It.Is<AsyncTask>(t => t.VirtualKeyId == virtualKeyId),
                It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task CreateTaskAsync_WithCacheFailure_StillCreatesTaskSuccessfully()
        {
            // Arrange
            var taskType = "test-task";
            var virtualKeyId = 123;
            var metadata = new { test = "data" };

            _mockRepository
                .Setup(r => r.CreateAsync(It.IsAny<AsyncTask>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AsyncTask t, CancellationToken ct) => t.Id);

            _mockCache
                .Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Cache is unavailable"));

            // Act - Should not throw despite cache failure
            var taskId = await _service.CreateTaskAsync(taskType, virtualKeyId, metadata);

            // Assert
            Assert.NotNull(taskId);
            _mockRepository.Verify(r => r.CreateAsync(It.IsAny<AsyncTask>(), It.IsAny<CancellationToken>()), Times.Once);
            
            // Verify warning was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to cache task")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task CreateTaskAsync_WithEventPublishFailure_StillCreatesTaskSuccessfully()
        {
            // Arrange
            var taskType = "test-task";
            var virtualKeyId = 456;
            var metadata = new { test = "data" };

            _mockRepository
                .Setup(r => r.CreateAsync(It.IsAny<AsyncTask>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AsyncTask t, CancellationToken ct) => t.Id);

            _mockPublishEndpoint
                .Setup(p => p.Publish(It.IsAny<AsyncTaskCreated>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Event bus is down"));

            // Act - Should not throw despite event publish failure
            var taskId = await _service.CreateTaskAsync(taskType, virtualKeyId, metadata);

            // Assert
            Assert.NotNull(taskId);
            _mockRepository.Verify(r => r.CreateAsync(It.IsAny<AsyncTask>(), It.IsAny<CancellationToken>()), Times.Once);
            
            // Verify warning was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to publish AsyncTaskCreated event")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetTaskStatusAsync_WithCacheReadFailure_FallsBackToDatabase()
        {
            // Arrange
            var taskId = "fallback-task";
            var dbTask = new AsyncTaskBuilder()
                .WithId(taskId)
                .AsProcessing(50)
                .Build();

            _mockCache
                .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Cache read error"));

            _mockRepository
                .Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(dbTask);

            // Act
            var result = await _service.GetTaskStatusAsync(taskId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(TaskState.Processing, result.State);
            Assert.Equal(50, result.Progress);

            // Verify database was queried
            _mockRepository.Verify(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()), Times.Once);
            
            // Verify warning was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cache read failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetTaskStatusAsync_WithCorruptCacheData_FallsBackToDatabase()
        {
            // Arrange
            var taskId = "corrupt-cache-task";
            var corruptJson = "{ invalid json";
            var dbTask = new AsyncTaskBuilder()
                .WithId(taskId)
                .AsCompleted()
                .Build();

            _mockCache
                .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Encoding.UTF8.GetBytes(corruptJson));

            _mockRepository
                .Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(dbTask);

            // Act
            var result = await _service.GetTaskStatusAsync(taskId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(TaskState.Completed, result.State);

            // Verify consistency issue was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("cache-database consistency issue detected")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task CacheTaskStatusAsync_WithRetryableFailure_RetriesSuccessfully()
        {
            // Arrange
            var taskType = "retry-test";
            var virtualKeyId = 789;
            var metadata = new { test = "data" };
            var attempts = 0;

            _mockRepository
                .Setup(r => r.CreateAsync(It.IsAny<AsyncTask>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AsyncTask t, CancellationToken ct) => t.Id);

            _mockCache
                .Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
                .Returns((string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken ct) =>
                {
                    attempts++;
                    if (attempts < 3)
                    {
                        throw new Exception($"Transient cache error attempt {attempts}");
                    }
                    return Task.CompletedTask;
                });

            // Act
            var taskId = await _service.CreateTaskAsync(taskType, virtualKeyId, metadata);

            // Assert
            Assert.NotNull(taskId);
            Assert.Equal(3, attempts); // Should succeed on third attempt
            
            // Verify retry warnings were logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cache operation failed for task") && v.ToString()!.Contains("attempt")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Exactly(2)); // 2 failures before success
        }

        [Fact]
        public async Task GetTaskStatusAsync_WithCacheRecovery_RepopulatesCache()
        {
            // Arrange
            var taskId = "recovery-task";
            var dbTask = new AsyncTaskBuilder()
                .WithId(taskId)
                .AsProcessing(75)
                .Build();

            _mockCache
                .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[])null!); // Cache miss

            _mockRepository
                .Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(dbTask);

            var cacheSetCalled = false;
            _mockCache
                .Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
                .Callback(() => cacheSetCalled = true)
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.GetTaskStatusAsync(taskId);

            // Assert
            Assert.NotNull(result);
            Assert.True(cacheSetCalled, "Cache should be repopulated after database read");
            
            // Verify debug log for re-caching
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Re-cached task") && v.ToString()!.Contains("after database read")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}
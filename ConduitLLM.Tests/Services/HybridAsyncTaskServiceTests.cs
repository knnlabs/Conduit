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
                .ReturnsAsync((AsyncTask t, CancellationToken ct) => t);

            // Act
            var result = await _service.CreateTaskAsync(taskType, payload, metadata, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(taskType, result.Type);
            Assert.Equal(123, result.VirtualKeyId);

            // Verify database creation
            _mockRepository.Verify(r => r.CreateAsync(
                It.Is<AsyncTask>(t => 
                    t.Type == taskType &&
                    t.State == (int)TaskState.Pending &&
                    t.VirtualKeyId == 123),
                cancellationToken), Times.Once);

            // Verify cache update
            _mockCache.Verify(c => c.SetStringAsync(
                It.Is<string>(k => k.StartsWith("task:")),
                It.IsAny<string>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                cancellationToken), Times.Once);

            // Verify event published
            _mockPublishEndpoint.Verify(p => p.Publish(
                It.Is<AsyncTaskCreated>(e => 
                    e.TaskId == result.Id &&
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
                new { metadata = new Dictionary<string, object> { { "virtualKeyId", "456" } }, expected = 456 },
                new { metadata = new Dictionary<string, object> { { "virtualKeyId", 789L } }, expected = 789 },
                new { metadata = new Dictionary<string, object> { { "VirtualKeyId", 321 } }, expected = 321 },
                new { metadata = new Dictionary<string, object> { { "virtual_key_id", 654 } }, expected = 654 }
            };

            _mockRepository
                .Setup(r => r.CreateAsync(It.IsAny<AsyncTask>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AsyncTask t, CancellationToken ct) => t);

            foreach (var testCase in testCases)
            {
                // Act
                var result = await _service.CreateTaskAsync("test", new { }, testCase.metadata);

                // Assert
                Assert.Equal(testCase.expected, result.VirtualKeyId);
            }
        }

        [Fact]
        public async Task CreateTaskAsync_WithInvalidVirtualKeyId_LogsWarning()
        {
            // Arrange
            var metadata = new Dictionary<string, object> { { "virtualKeyId", "invalid" } };

            _mockRepository
                .Setup(r => r.CreateAsync(It.IsAny<AsyncTask>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AsyncTask t, CancellationToken ct) => t);

            // Act
            var result = await _service.CreateTaskAsync("test", new { }, metadata);

            // Assert
            Assert.Equal(0, result.VirtualKeyId); // Default value

            // Verify warning was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to extract VirtualKeyId")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
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
                .Setup(c => c.GetAsync($"task:{taskId}", It.IsAny<CancellationToken>()))
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
                .Setup(c => c.GetAsync($"task:{taskId}", It.IsAny<CancellationToken>()))
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
            _mockCache.Verify(c => c.SetStringAsync(
                $"task:{taskId}",
                It.IsAny<string>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetTaskStatusAsync_WithNonExistingTask_ReturnsNull()
        {
            // Arrange
            var taskId = "non-existing";

            _mockCache
                .Setup(c => c.GetAsync($"task:{taskId}", It.IsAny<CancellationToken>()))
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

            var update = new AsyncTaskStatusUpdate
            {
                State = TaskState.Processing,
                Progress = 50,
                ProgressMessage = "Processing video..."
            };

            _mockRepository
                .Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingTask);

            _mockRepository
                .Setup(r => r.UpdateAsync(It.IsAny<AsyncTask>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpdateTaskStatusAsync(taskId, update);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(TaskState.Processing, result.State);
            Assert.Equal(50, result.Progress);
            Assert.Equal("Processing video...", result.ProgressMessage);

            // Verify database update
            _mockRepository.Verify(r => r.UpdateAsync(
                It.Is<AsyncTask>(t => 
                    t.Id == taskId &&
                    t.State == (int)TaskState.Processing &&
                    t.Progress == 50),
                It.IsAny<CancellationToken>()), Times.Once);

            // Verify cache update
            _mockCache.Verify(c => c.SetStringAsync(
                $"task:{taskId}",
                It.IsAny<string>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);

            // Verify event published
            _mockPublishEndpoint.Verify(p => p.Publish(
                It.Is<AsyncTaskUpdated>(e => 
                    e.TaskId == taskId &&
                    e.State == (int)TaskState.Processing &&
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

            var update = new AsyncTaskStatusUpdate
            {
                State = TaskState.Completed,
                Progress = 100,
                Result = new { videoUrl = "https://example.com/video.mp4" }
            };

            _mockRepository
                .Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingTask);

            _mockRepository
                .Setup(r => r.UpdateAsync(It.IsAny<AsyncTask>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            DistributedCacheEntryOptions? capturedOptions = null;
            _mockCache
                .Setup(c => c.SetStringAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, string, DistributedCacheEntryOptions, CancellationToken>(
                    (key, value, options, ct) => capturedOptions = options)
                .Returns(Task.CompletedTask);

            // Act
            await _service.UpdateTaskStatusAsync(taskId, update);

            // Assert
            Assert.NotNull(capturedOptions);
            Assert.NotNull(capturedOptions.SlidingExpiration);
            Assert.Equal(TimeSpan.FromHours(1), capturedOptions.SlidingExpiration.Value);
        }

        [Fact]
        public async Task UpdateTaskStatusAsync_WithNonExistingTask_ReturnsNull()
        {
            // Arrange
            var taskId = "non-existing";
            var update = new AsyncTaskStatusUpdate { State = TaskState.Processing };

            _mockRepository
                .Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((AsyncTask)null!);

            // Act
            var result = await _service.UpdateTaskStatusAsync(taskId, update);

            // Assert
            Assert.Null(result);
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
            var result = await _service.CancelTaskAsync(taskId);

            // Assert
            Assert.True(result);

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
                    e.State == (int)TaskState.Cancelled),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CancelTaskAsync_WithAlreadyCompletedTask_ReturnsFalse()
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

            // Act
            var result = await _service.CancelTaskAsync(taskId);

            // Assert
            Assert.False(result);
            _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<AsyncTask>(), It.IsAny<CancellationToken>()), Times.Never);
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
            var result = await _service.DeleteTaskAsync(taskId);

            // Assert
            Assert.True(result);

            // Verify database deletion
            _mockRepository.Verify(r => r.DeleteAsync(taskId, It.IsAny<CancellationToken>()), Times.Once);

            // Verify cache removal
            _mockCache.Verify(c => c.RemoveAsync($"task:{taskId}", It.IsAny<CancellationToken>()), Times.Once);

            // Verify event published
            _mockPublishEndpoint.Verify(p => p.Publish(
                It.Is<AsyncTaskDeleted>(e => e.TaskId == taskId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PollForCompletionAsync_PollsUntilCompleted()
        {
            // Arrange
            var taskId = "poll-task";
            var pollInterval = TimeSpan.FromMilliseconds(50);
            var timeout = TimeSpan.FromSeconds(1);

            var callCount = 0;
            _mockCache
                .Setup(c => c.GetAsync($"task:{taskId}", It.IsAny<CancellationToken>()))
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
            var result = await _service.PollForCompletionAsync(taskId, pollInterval, timeout);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(TaskState.Completed, result.State);
            Assert.NotNull(result.Result);
            Assert.True(callCount >= 3);
        }

        [Fact]
        public async Task PollForCompletionAsync_TimesOut_ReturnsLastStatus()
        {
            // Arrange
            var taskId = "timeout-task";
            var pollInterval = TimeSpan.FromMilliseconds(100);
            var timeout = TimeSpan.FromMilliseconds(250);

            var processingStatus = new AsyncTaskStatusBuilder()
                .WithTaskId(taskId)
                .AsProcessing(30)
                .Build();

            _mockCache
                .Setup(c => c.GetAsync($"task:{taskId}", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(processingStatus)));

            // Act
            var result = await _service.PollForCompletionAsync(taskId, pollInterval, timeout);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(TaskState.Processing, result.State);
            Assert.Equal(30, result.Progress);
        }

        [Fact]
        public async Task CleanupOldTasksAsync_ArchivesAndDeletesOldTasks()
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
                .Setup(r => r.GetTasksForCleanupAsync(deleteAfter, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tasksToDelete);

            _mockRepository
                .Setup(r => r.BulkDeleteAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);

            // Act
            var (archived, deleted) = await _service.CleanupOldTasksAsync(archiveAfter, deleteAfter);

            // Assert
            Assert.Equal(5, archived);
            Assert.Equal(2, deleted);

            // Verify operations
            _mockRepository.Verify(r => r.ArchiveOldTasksAsync(archiveAfter, It.IsAny<CancellationToken>()), Times.Once);
            _mockRepository.Verify(r => r.GetTasksForCleanupAsync(deleteAfter, It.IsAny<CancellationToken>()), Times.Once);
            _mockRepository.Verify(r => r.BulkDeleteAsync(
                It.Is<IEnumerable<string>>(ids => ids.Count() == 2),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetTasksByVirtualKeyAsync_WithActiveOnly_ReturnsActiveTasksFromRepository()
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
                .ReturnsAsync(activeTasks);

            // Act
            var result = await _service.GetTasksByVirtualKeyAsync(virtualKeyId, activeOnly: true);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.All(result, s => Assert.False(s.IsArchived));
        }

        [Fact]
        public async Task GetTasksByVirtualKeyAsync_WithIncludeArchived_ReturnsAllTasksFromRepository()
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
                .ReturnsAsync(allTasks);

            // Act
            var result = await _service.GetTasksByVirtualKeyAsync(virtualKeyId, activeOnly: false);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, s => s.IsArchived == true);
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
                .ReturnsAsync((AsyncTask t, CancellationToken ct) => t);

            // Act
            var result = await service.CreateTaskAsync("test", new { }, new Dictionary<string, object>());

            // Assert
            Assert.NotNull(result);
            // No events should be published
            _mockPublishEndpoint.Verify(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
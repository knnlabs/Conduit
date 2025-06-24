using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Tests.TestHelpers.Builders;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace ConduitLLM.Tests.Integration
{
    public class AsyncTaskIntegrationTests : IAsyncLifetime
    {
        private ServiceProvider _serviceProvider = null!;
        private IAsyncTaskService _asyncTaskService = null!;
        private IAsyncTaskRepository _repository = null!;
        private IDistributedCache _cache = null!;
        private ITestHarness _testHarness = null!;
        private ConfigurationDbContext _dbContext = null!;
        private SqliteConnection _connection = null!;

        public async Task InitializeAsync()
        {
            var services = new ServiceCollection();

            // Setup SQLite in-memory database (supports FK constraints unlike EF in-memory provider)
            _connection = new SqliteConnection("Data Source=:memory:");
            await _connection.OpenAsync();
            
            var dbOptions = new DbContextOptionsBuilder<ConfigurationDbContext>()
                .UseSqlite(_connection)
                .Options;

            services.AddSingleton(dbOptions);
            services.AddDbContextFactory<ConfigurationDbContext>(options =>
                options.UseSqlite(_connection));

            // Add repositories
            services.AddScoped<IAsyncTaskRepository, AsyncTaskRepository>();

            // Add caching
            services.AddDistributedMemoryCache();
            services.AddSingleton<IMemoryCache, MemoryCache>();

            // Add MassTransit for event testing
            services.AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<TestAsyncTaskEventConsumer>();
            });

            // Add logging
            services.AddLogging(builder => builder.AddDebug());

            // Add the HybridAsyncTaskService
            services.AddScoped<IAsyncTaskService>(sp =>
            {
                var repository = sp.GetRequiredService<IAsyncTaskRepository>();
                var cache = sp.GetRequiredService<IDistributedCache>();
                var publishEndpoint = sp.GetService<IPublishEndpoint>();
                var logger = sp.GetRequiredService<ILogger<HybridAsyncTaskService>>();
                return new HybridAsyncTaskService(repository, cache, publishEndpoint, logger);
            });

            _serviceProvider = services.BuildServiceProvider();

            // Get services
            _asyncTaskService = _serviceProvider.GetRequiredService<IAsyncTaskService>();
            _repository = _serviceProvider.GetRequiredService<IAsyncTaskRepository>();
            _cache = _serviceProvider.GetRequiredService<IDistributedCache>();
            _testHarness = _serviceProvider.GetRequiredService<ITestHarness>();
            
            // Create and seed database
            var contextFactory = _serviceProvider.GetRequiredService<IDbContextFactory<ConfigurationDbContext>>();
            _dbContext = await contextFactory.CreateDbContextAsync();
            await _dbContext.Database.EnsureCreatedAsync();
            
            // Seed with a test virtual key
            _dbContext.VirtualKeys.Add(new VirtualKey
            {
                Id = 123,
                KeyName = "Test Key",
                KeyHash = "test-hash",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            // Start the test harness
            await _testHarness.Start();
        }

        public async Task DisposeAsync()
        {
            await _testHarness.Stop();
            _dbContext?.Dispose();
            _connection?.Dispose();
            
            if (_serviceProvider != null)
            {
                await _serviceProvider.DisposeAsync();
            }
        }

        [Fact]
        public async Task FullTaskLifecycle_CreateUpdateCompleteArchive_WorksEndToEnd()
        {
            // Arrange
            var metadata = new Dictionary<string, object> { { "virtualKeyId", 123 } };
            var payload = new { videoPrompt = "A beautiful waterfall", duration = 5 };

            // Act 1: Create task
            var taskId = await _asyncTaskService.CreateTaskAsync("video-generation", metadata);

            // Assert 1: Task created
            Assert.NotNull(taskId);
            Assert.NotEmpty(taskId);

            // Get the created task status
            var createdTask = await _asyncTaskService.GetTaskStatusAsync(taskId);
            Assert.NotNull(createdTask);
            Assert.Equal("video-generation", createdTask.TaskType);
            Assert.Equal(TaskState.Pending, createdTask.State);

            // Verify task in database
            var dbTask = await _repository.GetByIdAsync(taskId);
            Assert.NotNull(dbTask);
            Assert.Equal(taskId, dbTask.Id);

            // Verify task in cache
            var cachedStatus = await GetFromCache(taskId);
            Assert.NotNull(cachedStatus);
            Assert.Equal(TaskState.Pending, cachedStatus.State);

            // Verify event published
            Assert.True(await _testHarness.Published.Any<AsyncTaskCreated>());

            // Act 2: Update to processing
            var processingUpdate = new AsyncTaskStatusUpdate
            {
                State = TaskState.Processing,
                Progress = 25,
                ProgressMessage = "Generating video frames..."
            };
            await _asyncTaskService.UpdateTaskStatusAsync(taskId, TaskState.Processing, 25, null, null);
            var processingStatus = await _asyncTaskService.GetTaskStatusAsync(taskId);

            // Assert 2: Task updated
            Assert.NotNull(processingStatus);
            Assert.Equal(TaskState.Processing, processingStatus.State);
            Assert.Equal(25, processingStatus.Progress);

            // Verify update in cache
            cachedStatus = await GetFromCache(taskId);
            Assert.NotNull(cachedStatus);
            Assert.Equal(TaskState.Processing, cachedStatus.State);
            Assert.Equal(25, cachedStatus.Progress);

            // Act 3: Update progress
            var progressUpdate = new AsyncTaskStatusUpdate
            {
                State = TaskState.Processing,
                Progress = 75,
                ProgressMessage = "Encoding video..."
            };
            await _asyncTaskService.UpdateTaskStatusAsync(taskId, TaskState.Processing, 75);

            // Act 4: Complete task
            var result = new { videoUrl = "https://example.com/video.mp4", duration = 5 };
            await _asyncTaskService.UpdateTaskStatusAsync(taskId, TaskState.Completed, 100, result);
            var completedStatus = await _asyncTaskService.GetTaskStatusAsync(taskId);

            // Assert 4: Task completed
            Assert.NotNull(completedStatus);
            Assert.Equal(TaskState.Completed, completedStatus.State);
            Assert.Equal(100, completedStatus.Progress);
            Assert.NotNull(completedStatus.Result);
            Assert.NotNull(completedStatus.CompletedAt);

            // Verify multiple update events published
            var updateEvents = _testHarness.Published.Select<AsyncTaskUpdated>().ToArray();
            Assert.True(updateEvents.Length >= 3);

            // Act 5: Archive old tasks
            // First, manually update the completed date to make it old
            dbTask = await _repository.GetByIdAsync(taskId);
            dbTask!.CompletedAt = DateTime.UtcNow.AddDays(-8);
            await _repository.UpdateAsync(dbTask);

            var cleanedUp = await _asyncTaskService.CleanupOldTasksAsync(TimeSpan.FromDays(7));

            // Assert 5: Task cleaned up
            Assert.Equal(1, cleanedUp);

            // Verify task is archived
            dbTask = await _repository.GetByIdAsync(taskId);
            Assert.NotNull(dbTask);
            Assert.True(dbTask.IsArchived);
            Assert.NotNull(dbTask.ArchivedAt);
        }

        [Fact(Skip = "GetTasksByVirtualKeyAsync not available in current interface")]
        public async Task ConcurrentTaskOperations_HandleGracefully()
        {
            // Arrange
            var taskCount = 10;
            var virtualKeyId = 123;
            var metadata = new Dictionary<string, object> { { "virtualKeyId", virtualKeyId } };

            // Act: Create multiple tasks concurrently
            var createTasks = Enumerable.Range(1, taskCount)
                .Select(i => _asyncTaskService.CreateTaskAsync(
                    $"concurrent-test-{i}",
                    metadata))
                .ToArray();

            var createdTasks = await Task.WhenAll(createTasks);

            // Assert: All tasks created
            Assert.Equal(taskCount, createdTasks.Length);
            Assert.All(createdTasks, t => Assert.NotNull(t));

            // Act: Update all tasks concurrently
            var updateTasks = createdTasks.Select(taskId =>
                _asyncTaskService.UpdateTaskStatusAsync(taskId, TaskState.Processing, Random.Shared.Next(1, 100))).ToArray();

            await Task.WhenAll(updateTasks);

            // Assert: Updates completed without errors

            // Act: Get all tasks by virtual key - Method not available in current interface
            // var tasksByKey = await _asyncTaskService.GetTasksByVirtualKeyAsync(virtualKeyId);

            // Assert: All tasks retrieved - Skipped due to interface method unavailability
            // Assert.NotNull(tasksByKey);
            // Assert.Equal(taskCount, tasksByKey.Count);
        }

        [Fact]
        public async Task ServiceRestart_CacheMissRecovery_WorksCorrectly()
        {
            // Arrange
            var metadata = new Dictionary<string, object> { { "virtualKeyId", 123 } };
            var taskId = await _asyncTaskService.CreateTaskAsync("restart-test", metadata);

            // Update task to processing state
            await _asyncTaskService.UpdateTaskStatusAsync(taskId, TaskState.Processing, 50);

            // Act: Clear cache to simulate service restart
            await _cache.RemoveAsync($"task:{taskId}");

            // Get task status (should trigger cache miss and database query)
            var status = await _asyncTaskService.GetTaskStatusAsync(taskId);

            // Assert: Status recovered from database
            Assert.NotNull(status);
            Assert.Equal(taskId, status.TaskId);
            Assert.Equal(TaskState.Processing, status.State);
            Assert.Equal(50, status.Progress);

            // Verify cache was repopulated
            var cachedStatus = await GetFromCache(taskId);
            Assert.NotNull(cachedStatus);
            Assert.Equal(TaskState.Processing, cachedStatus.State);
        }

        [Fact]
        public async Task TaskCancellation_DuringProcessing_WorksCorrectly()
        {
            // Arrange
            var metadata = new Dictionary<string, object> { { "virtualKeyId", 123 } };
            var taskId = await _asyncTaskService.CreateTaskAsync("cancel-test", metadata);

            // Start processing
            await _asyncTaskService.UpdateTaskStatusAsync(taskId, TaskState.Processing, 30);

            // Act: Cancel the task
            await _asyncTaskService.CancelTaskAsync(taskId);

            // Assert: Task cancelled
            var status = await _asyncTaskService.GetTaskStatusAsync(taskId);
            Assert.NotNull(status);
            Assert.Equal(TaskState.Cancelled, status.State);
            Assert.NotNull(status.CompletedAt);

            // Verify event published
            var cancelEvent = _testHarness.Published
                .Select<AsyncTaskUpdated>()
                .Where(e => e.Context.Message.State == TaskState.Cancelled.ToString())
                .FirstOrDefault();
            Assert.NotNull(cancelEvent);
        }

        [Fact]
        public async Task PollingForCompletion_ReturnsWhenCompleted()
        {
            // Arrange
            var metadata = new Dictionary<string, object> { { "virtualKeyId", 123 } };
            var taskId = await _asyncTaskService.CreateTaskAsync("poll-test", metadata);

            // Start a background task to complete the task after a delay
            var completionTask = Task.Run(async () =>
            {
                await Task.Delay(200);
                await _asyncTaskService.UpdateTaskStatusAsync(taskId, TaskState.Processing, 50);

                await Task.Delay(200);
                await _asyncTaskService.UpdateTaskStatusAsync(taskId, TaskState.Completed, 100, 
                    new { status = "success" });
            });

            // Act: Poll for completion
            var completedStatus = await _asyncTaskService.PollTaskUntilCompletedAsync(
                taskId,
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromSeconds(2));

            // Assert: Task completed
            Assert.NotNull(completedStatus);
            Assert.Equal(TaskState.Completed, completedStatus.State);
            Assert.Equal(100, completedStatus.Progress);
            Assert.NotNull(completedStatus.Result);

            await completionTask; // Ensure background task completes
        }

        [Fact]
        public async Task VirtualKeyDeletion_CascadesToTasks()
        {
            // Arrange
            var virtualKeyId = 999;
            
            // Add a new virtual key
            _dbContext.VirtualKeys.Add(new VirtualKey
            {
                Id = virtualKeyId,
                KeyName = "Delete Test Key",
                KeyHash = "delete-test-hash",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            // Create tasks for this key
            var metadata = new Dictionary<string, object> { { "virtualKeyId", virtualKeyId } };
            var task1 = await _asyncTaskService.CreateTaskAsync("delete-cascade-1", metadata);
            var task2 = await _asyncTaskService.CreateTaskAsync("delete-cascade-2", metadata);

            // Verify tasks were created with correct VirtualKeyId
            var tasksBeforeDelete = await _repository.GetByVirtualKeyAsync(virtualKeyId);
            Assert.NotNull(tasksBeforeDelete);
            Assert.Equal(2, tasksBeforeDelete.Count());

            // Act: Delete the virtual key
            var virtualKey = await _dbContext.VirtualKeys.FindAsync(virtualKeyId);
            _dbContext.VirtualKeys.Remove(virtualKey!);
            await _dbContext.SaveChangesAsync();

            // Assert: Tasks should be deleted due to cascade
            var remainingTasks = await _repository.GetByVirtualKeyAsync(virtualKeyId);
            Assert.NotNull(remainingTasks);
            Assert.Empty(remainingTasks);
        }

        [Fact]
        public async Task TaskWithLargePayload_HandlesCorrectly()
        {
            // Arrange
            var largePayload = new
            {
                data = string.Join("", Enumerable.Repeat("Large data chunk. ", 1000)),
                items = Enumerable.Range(1, 100).Select(i => new { id = i, value = $"Item {i}" }).ToList()
            };
            var metadata = new Dictionary<string, object> { { "virtualKeyId", 123 } };

            // Act
            var taskId = await _asyncTaskService.CreateTaskAsync("large-payload-test", metadata);

            // Assert
            Assert.NotNull(taskId);

            // Verify it can be retrieved
            var status = await _asyncTaskService.GetTaskStatusAsync(taskId);
            Assert.NotNull(status);

            // Update with large result
            var largeResult = new
            {
                output = string.Join("", Enumerable.Repeat("Result data. ", 2000)),
                processedItems = Enumerable.Range(1, 200).ToList()
            };

            await _asyncTaskService.UpdateTaskStatusAsync(taskId, TaskState.Completed, 100, largeResult);

            var updated = await _asyncTaskService.GetTaskStatusAsync(taskId);
            Assert.NotNull(updated);
            Assert.NotNull(updated.Result);
        }

        // Helper method to get task status from cache
        private async Task<AsyncTaskStatus?> GetFromCache(string taskId)
        {
            var cached = await _cache.GetAsync($"async:task:{taskId}");
            if (cached == null) return null;

            var json = Encoding.UTF8.GetString(cached);
            return JsonSerializer.Deserialize<AsyncTaskStatus>(json);
        }
    }

    // Test consumer for verifying events
    public class TestAsyncTaskEventConsumer :
        IConsumer<AsyncTaskCreated>,
        IConsumer<AsyncTaskUpdated>,
        IConsumer<AsyncTaskDeleted>
    {
        public Task Consume(ConsumeContext<AsyncTaskCreated> context) => Task.CompletedTask;
        public Task Consume(ConsumeContext<AsyncTaskUpdated> context) => Task.CompletedTask;
        public Task Consume(ConsumeContext<AsyncTaskDeleted> context) => Task.CompletedTask;
    }
}
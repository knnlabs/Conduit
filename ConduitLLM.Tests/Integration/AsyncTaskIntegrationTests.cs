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

        public async Task InitializeAsync()
        {
            var services = new ServiceCollection();

            // Setup in-memory database
            var dbOptions = new DbContextOptionsBuilder<ConfigurationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            services.AddSingleton(dbOptions);
            services.AddDbContextFactory<ConfigurationDbContext>(options =>
                options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));

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
            _serviceProvider?.Dispose();
        }

        [Fact]
        public async Task FullTaskLifecycle_CreateUpdateCompleteArchive_WorksEndToEnd()
        {
            // Arrange
            var metadata = new Dictionary<string, object> { { "virtualKeyId", 123 } };
            var payload = new { videoPrompt = "A beautiful waterfall", duration = 5 };

            // Act 1: Create task
            var createdTask = await _asyncTaskService.CreateTaskAsync("video-generation", payload, metadata);

            // Assert 1: Task created
            Assert.NotNull(createdTask);
            Assert.Equal("video-generation", createdTask.Type);
            Assert.Equal(123, createdTask.VirtualKeyId);
            Assert.Equal((int)TaskState.Pending, createdTask.State);

            // Verify task in database
            var dbTask = await _repository.GetByIdAsync(createdTask.Id);
            Assert.NotNull(dbTask);
            Assert.Equal(createdTask.Id, dbTask.Id);

            // Verify task in cache
            var cachedStatus = await GetFromCache(createdTask.Id);
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
            var processingStatus = await _asyncTaskService.UpdateTaskStatusAsync(createdTask.Id, processingUpdate);

            // Assert 2: Task updated
            Assert.NotNull(processingStatus);
            Assert.Equal(TaskState.Processing, processingStatus.State);
            Assert.Equal(25, processingStatus.Progress);

            // Verify update in cache
            cachedStatus = await GetFromCache(createdTask.Id);
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
            await _asyncTaskService.UpdateTaskStatusAsync(createdTask.Id, progressUpdate);

            // Act 4: Complete task
            var completeUpdate = new AsyncTaskStatusUpdate
            {
                State = TaskState.Completed,
                Progress = 100,
                Result = new { videoUrl = "https://example.com/video.mp4", duration = 5 }
            };
            var completedStatus = await _asyncTaskService.UpdateTaskStatusAsync(createdTask.Id, completeUpdate);

            // Assert 4: Task completed
            Assert.NotNull(completedStatus);
            Assert.Equal(TaskState.Completed, completedStatus.State);
            Assert.Equal(100, completedStatus.Progress);
            Assert.NotNull(completedStatus.Result);
            Assert.NotNull(completedStatus.CompletedAt);

            // Verify multiple update events published
            var updateEvents = await _testHarness.Published.SelectAsync<AsyncTaskUpdated>().ToListAsync();
            Assert.True(updateEvents.Count >= 3);

            // Act 5: Archive old tasks
            // First, manually update the completed date to make it old
            dbTask = await _repository.GetByIdAsync(createdTask.Id);
            dbTask!.CompletedAt = DateTime.UtcNow.AddDays(-8);
            await _repository.UpdateAsync(dbTask);

            var (archived, deleted) = await _asyncTaskService.CleanupOldTasksAsync(
                TimeSpan.FromDays(7),
                TimeSpan.FromDays(30));

            // Assert 5: Task archived
            Assert.Equal(1, archived);
            Assert.Equal(0, deleted); // Not old enough to delete

            // Verify task is archived
            dbTask = await _repository.GetByIdAsync(createdTask.Id);
            Assert.NotNull(dbTask);
            Assert.True(dbTask.IsArchived);
            Assert.NotNull(dbTask.ArchivedAt);
        }

        [Fact]
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
                    new { index = i },
                    metadata))
                .ToArray();

            var createdTasks = await Task.WhenAll(createTasks);

            // Assert: All tasks created
            Assert.Equal(taskCount, createdTasks.Length);
            Assert.All(createdTasks, t => Assert.NotNull(t));
            Assert.All(createdTasks, t => Assert.Equal(virtualKeyId, t.VirtualKeyId));

            // Act: Update all tasks concurrently
            var updateTasks = createdTasks.Select(t =>
                _asyncTaskService.UpdateTaskStatusAsync(t.Id, new AsyncTaskStatusUpdate
                {
                    State = TaskState.Processing,
                    Progress = Random.Shared.Next(1, 100)
                })).ToArray();

            var updatedStatuses = await Task.WhenAll(updateTasks);

            // Assert: All updates succeeded
            Assert.All(updatedStatuses, s => Assert.NotNull(s));
            Assert.All(updatedStatuses, s => Assert.Equal(TaskState.Processing, s.State));

            // Act: Get all tasks by virtual key
            var tasksByKey = await _asyncTaskService.GetTasksByVirtualKeyAsync(virtualKeyId);

            // Assert: All tasks retrieved
            Assert.NotNull(tasksByKey);
            Assert.Equal(taskCount, tasksByKey.Count);
        }

        [Fact]
        public async Task ServiceRestart_CacheMissRecovery_WorksCorrectly()
        {
            // Arrange
            var metadata = new Dictionary<string, object> { { "virtualKeyId", 123 } };
            var task = await _asyncTaskService.CreateTaskAsync("restart-test", new { }, metadata);

            // Update task to processing state
            await _asyncTaskService.UpdateTaskStatusAsync(task.Id, new AsyncTaskStatusUpdate
            {
                State = TaskState.Processing,
                Progress = 50
            });

            // Act: Clear cache to simulate service restart
            await _cache.RemoveAsync($"task:{task.Id}");

            // Get task status (should trigger cache miss and database query)
            var status = await _asyncTaskService.GetTaskStatusAsync(task.Id);

            // Assert: Status recovered from database
            Assert.NotNull(status);
            Assert.Equal(task.Id, status.TaskId);
            Assert.Equal(TaskState.Processing, status.State);
            Assert.Equal(50, status.Progress);

            // Verify cache was repopulated
            var cachedStatus = await GetFromCache(task.Id);
            Assert.NotNull(cachedStatus);
            Assert.Equal(TaskState.Processing, cachedStatus.State);
        }

        [Fact]
        public async Task TaskCancellation_DuringProcessing_WorksCorrectly()
        {
            // Arrange
            var metadata = new Dictionary<string, object> { { "virtualKeyId", 123 } };
            var task = await _asyncTaskService.CreateTaskAsync("cancel-test", new { }, metadata);

            // Start processing
            await _asyncTaskService.UpdateTaskStatusAsync(task.Id, new AsyncTaskStatusUpdate
            {
                State = TaskState.Processing,
                Progress = 30
            });

            // Act: Cancel the task
            var cancelled = await _asyncTaskService.CancelTaskAsync(task.Id);

            // Assert: Task cancelled
            Assert.True(cancelled);

            var status = await _asyncTaskService.GetTaskStatusAsync(task.Id);
            Assert.NotNull(status);
            Assert.Equal(TaskState.Cancelled, status.State);
            Assert.NotNull(status.CompletedAt);

            // Verify event published
            var cancelEvent = await _testHarness.Published
                .SelectAsync<AsyncTaskUpdated>()
                .Where(e => e.Context.Message.State == (int)TaskState.Cancelled)
                .FirstOrDefaultAsync();
            Assert.NotNull(cancelEvent);
        }

        [Fact]
        public async Task PollingForCompletion_ReturnsWhenCompleted()
        {
            // Arrange
            var metadata = new Dictionary<string, object> { { "virtualKeyId", 123 } };
            var task = await _asyncTaskService.CreateTaskAsync("poll-test", new { }, metadata);

            // Start a background task to complete the task after a delay
            var completionTask = Task.Run(async () =>
            {
                await Task.Delay(200);
                await _asyncTaskService.UpdateTaskStatusAsync(task.Id, new AsyncTaskStatusUpdate
                {
                    State = TaskState.Processing,
                    Progress = 50
                });

                await Task.Delay(200);
                await _asyncTaskService.UpdateTaskStatusAsync(task.Id, new AsyncTaskStatusUpdate
                {
                    State = TaskState.Completed,
                    Progress = 100,
                    Result = new { status = "success" }
                });
            });

            // Act: Poll for completion
            var completedStatus = await _asyncTaskService.PollForCompletionAsync(
                task.Id,
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
            var task1 = await _asyncTaskService.CreateTaskAsync("delete-cascade-1", new { }, metadata);
            var task2 = await _asyncTaskService.CreateTaskAsync("delete-cascade-2", new { }, metadata);

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
            var task = await _asyncTaskService.CreateTaskAsync("large-payload-test", largePayload, metadata);

            // Assert
            Assert.NotNull(task);

            // Verify it can be retrieved
            var status = await _asyncTaskService.GetTaskStatusAsync(task.Id);
            Assert.NotNull(status);

            // Update with large result
            var largeResult = new
            {
                output = string.Join("", Enumerable.Repeat("Result data. ", 2000)),
                processedItems = Enumerable.Range(1, 200).ToList()
            };

            var updated = await _asyncTaskService.UpdateTaskStatusAsync(task.Id, new AsyncTaskStatusUpdate
            {
                State = TaskState.Completed,
                Result = largeResult
            });

            Assert.NotNull(updated);
            Assert.NotNull(updated.Result);
        }

        // Helper method to get task status from cache
        private async Task<AsyncTaskStatus?> GetFromCache(string taskId)
        {
            var cached = await _cache.GetAsync($"task:{taskId}");
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
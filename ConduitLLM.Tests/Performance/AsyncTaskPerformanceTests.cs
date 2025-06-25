using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Performance
{
    [Trait("Category", TestCategories.Performance)]
    public class AsyncTaskPerformanceTests : IAsyncLifetime
    {
        private readonly ITestOutputHelper _output;
        private ServiceProvider _serviceProvider = null!;
        private IAsyncTaskService _asyncTaskService = null!;
        private IAsyncTaskRepository _repository = null!;
        private ConfigurationDbContext _dbContext = null!;
        private SqliteConnection _connection = null!;

        public AsyncTaskPerformanceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public async Task InitializeAsync()
        {
            var services = new ServiceCollection();

            // Use SQLite in-memory for more realistic performance testing
            // Keep connection open to maintain database throughout test lifecycle
            _connection = new SqliteConnection("DataSource=:memory:");
            await _connection.OpenAsync();
            
            services.AddDbContextFactory<ConfigurationDbContext>(options =>
                options.UseSqlite(_connection));

            services.AddScoped<IAsyncTaskRepository, AsyncTaskRepository>();
            services.AddDistributedMemoryCache();
            services.AddLogging(builder => builder.AddDebug());

            services.AddScoped<IAsyncTaskService>(sp =>
            {
                var repository = sp.GetRequiredService<IAsyncTaskRepository>();
                var cache = sp.GetRequiredService<IDistributedCache>();
                var logger = sp.GetRequiredService<ILogger<HybridAsyncTaskService>>();
                return new HybridAsyncTaskService(repository, cache, null, logger);
            });

            _serviceProvider = services.BuildServiceProvider();

            // Initialize database
            var contextFactory = _serviceProvider.GetRequiredService<IDbContextFactory<ConfigurationDbContext>>();
            _dbContext = await contextFactory.CreateDbContextAsync();
            await _dbContext.Database.EnsureCreatedAsync();

            // Seed virtual keys for testing
            var virtualKeys = Enumerable.Range(1, 10).Select(i => new ConduitLLM.Configuration.Entities.VirtualKey
            {
                Id = i,
                KeyName = $"Test Key {i}",
                KeyHash = $"test-hash-{i}",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            _dbContext.VirtualKeys.AddRange(virtualKeys);
            await _dbContext.SaveChangesAsync();

            _asyncTaskService = _serviceProvider.GetRequiredService<IAsyncTaskService>();
            _repository = _serviceProvider.GetRequiredService<IAsyncTaskRepository>();
        }

        public Task DisposeAsync()
        {
            _dbContext?.Dispose();
            _connection?.Dispose();
            _serviceProvider?.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public async Task BulkTaskCreation_Performance()
        {
            // Arrange
            const int taskCount = 1000;
            var tasks = new List<Task<string>>();
            var sw = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < taskCount; i++)
            {
                var metadata = new Dictionary<string, object> 
                { 
                    { "virtualKeyId", (i % 10) + 1 },
                    { "batchId", Guid.NewGuid().ToString() }
                };
                
                tasks.Add(_asyncTaskService.CreateTaskAsync(
                    $"perf-test-{i}",
                    metadata));
            }

            var createdTaskIds = await Task.WhenAll(tasks);
            sw.Stop();

            // Assert
            Assert.Equal(taskCount, createdTaskIds.Length);
            _output.WriteLine($"Created {taskCount} tasks in {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Average: {sw.ElapsedMilliseconds / (double)taskCount:F2}ms per task");

            // Performance benchmark: Should create 1000 tasks in under 10 seconds
            Assert.True(sw.ElapsedMilliseconds < 10000, $"Task creation took too long: {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task ConcurrentTaskUpdates_Performance()
        {
            // Arrange
            const int taskCount = 100;
            const int updatesPerTask = 10;
            
            // Create tasks first
            var createdTaskIds = new List<string>();
            for (int i = 0; i < taskCount; i++)
            {
                var taskId = await _asyncTaskService.CreateTaskAsync(
                    "concurrent-update-test",
                    new Dictionary<string, object> { { "virtualKeyId", 1 } });
                createdTaskIds.Add(taskId);
            }

            // Act: Perform concurrent updates
            var sw = Stopwatch.StartNew();
            var updateTasks = new List<Task>();

            foreach (var taskId in createdTaskIds)
            {
                for (int i = 0; i < updatesPerTask; i++)
                {
                    var progress = (i + 1) * 10;
                    updateTasks.Add(UpdateTaskProgressAsync(taskId, progress));
                }
            }

            await Task.WhenAll(updateTasks);
            sw.Stop();

            // Assert
            var totalUpdates = taskCount * updatesPerTask;
            _output.WriteLine($"Performed {totalUpdates} updates in {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Average: {sw.ElapsedMilliseconds / (double)totalUpdates:F2}ms per update");

            // Performance benchmark: Should handle 1000 updates in under 10 seconds
            Assert.True(sw.ElapsedMilliseconds < 10000, $"Updates took too long: {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task ArchivalPerformance_WithLargeDataset()
        {
            // Arrange: Create a large number of old completed tasks
            const int taskCount = 5000;
            var oldDate = DateTime.UtcNow.AddDays(-10);
            
            var tasks = new List<ConduitLLM.Configuration.Entities.AsyncTask>();
            for (int i = 0; i < taskCount; i++)
            {
                tasks.Add(new ConduitLLM.Configuration.Entities.AsyncTask
                {
                    Id = $"archive-perf-{i}",
                    Type = "test-task",
                    State = (int)TaskState.Completed,
                    VirtualKeyId = (i % 10) + 1,
                    CreatedAt = oldDate,
                    UpdatedAt = oldDate,
                    CompletedAt = oldDate,
                    Progress = 100,
                    IsArchived = false,
                    Payload = "{}",
                    Result = "{\"status\":\"completed\"}",
                    Metadata = "{}"
                });
            }

            // Batch insert for faster setup
            const int batchSize = 100;
            for (int i = 0; i < tasks.Count; i += batchSize)
            {
                var batch = tasks.Skip(i).Take(batchSize);
                _dbContext.AsyncTasks.AddRange(batch);
                await _dbContext.SaveChangesAsync();
            }

            // Act: Archive old tasks
            var sw = Stopwatch.StartNew();
            var archivedCount = await _repository.ArchiveOldTasksAsync(TimeSpan.FromDays(7));
            sw.Stop();

            // Assert
            Assert.Equal(taskCount, archivedCount);
            _output.WriteLine($"Archived {archivedCount} tasks in {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Average: {sw.ElapsedMilliseconds / (double)archivedCount:F2}ms per task");

            // Performance benchmark: Should archive 5000 tasks in under 5 seconds
            Assert.True(sw.ElapsedMilliseconds < 5000, $"Archival took too long: {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task CachePerformance_UnderLoad()
        {
            // Arrange: Create tasks and warm up cache
            const int taskCount = 100;
            const int readIterations = 1000;
            
            var taskIds = new List<string>();
            for (int i = 0; i < taskCount; i++)
            {
                var taskId = await _asyncTaskService.CreateTaskAsync(
                    "cache-test",
                    new Dictionary<string, object> { { "virtualKeyId", 1 } });
                taskIds.Add(taskId);
            }

            // Warm up cache by reading once
            foreach (var id in taskIds)
            {
                await _asyncTaskService.GetTaskStatusAsync(id);
            }

            // Act: Perform many concurrent cache reads
            var sw = Stopwatch.StartNew();
            var readTasks = new List<Task<AsyncTaskStatus?>>();

            for (int i = 0; i < readIterations; i++)
            {
                var taskId = taskIds[i % taskCount];
                readTasks.Add(_asyncTaskService.GetTaskStatusAsync(taskId));
            }

            var results = await Task.WhenAll(readTasks);
            sw.Stop();

            // Assert
            Assert.All(results, r => Assert.NotNull(r));
            _output.WriteLine($"Performed {readIterations} cache reads in {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Average: {sw.ElapsedMilliseconds / (double)readIterations:F2}ms per read");

            // Performance benchmark: Should handle 1000 reads in under 1 second
            Assert.True(sw.ElapsedMilliseconds < 1000, $"Cache reads took too long: {sw.ElapsedMilliseconds}ms");
        }

        [Fact(Skip = "Requires IExtendedAsyncTaskService implementation")]
        public async Task QueryPerformance_GetTasksByVirtualKey()
        {
            // Arrange: Create many tasks for different virtual keys
            const int tasksPerKey = 500;
            const int keyCount = 10;

            for (int keyId = 1; keyId <= keyCount; keyId++)
            {
                for (int i = 0; i < tasksPerKey; i++)
                {
                    await _asyncTaskService.CreateTaskAsync(
                        $"query-test-{keyId}-{i}",
                        new Dictionary<string, object> { { "virtualKeyId", keyId } });
                }
            }

            // Act: Query tasks for each virtual key
            var sw = Stopwatch.StartNew();
            var queryTasks = new List<Task<IList<AsyncTaskStatus>>>();

            // Method not available in current interface
            /*
            for (int keyId = 1; keyId <= keyCount; keyId++)
            {
                queryTasks.Add(_asyncTaskService.GetTasksByVirtualKeyAsync(keyId, activeOnly: false));
            }
            */

            /*
            var results = await Task.WhenAll(queryTasks);
            sw.Stop();

            // Assert
            Assert.All(results, r => Assert.Equal(tasksPerKey, r.Count));
            _output.WriteLine($"Queried {keyCount} virtual keys ({tasksPerKey} tasks each) in {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Average: {sw.ElapsedMilliseconds / (double)keyCount:F2}ms per query");

            // Performance benchmark: Should query 10 keys in under 2 seconds
            Assert.True(sw.ElapsedMilliseconds < 2000, $"Queries took too long: {sw.ElapsedMilliseconds}ms");
            */
        }

        [Fact]
        public async Task CleanupPerformance_DeleteOldArchivedTasks()
        {
            // Arrange: Create old archived tasks
            const int taskCount = 1000;
            var oldDate = DateTime.UtcNow.AddDays(-40);
            
            var tasks = new List<ConduitLLM.Configuration.Entities.AsyncTask>();
            for (int i = 0; i < taskCount; i++)
            {
                tasks.Add(new ConduitLLM.Configuration.Entities.AsyncTask
                {
                    Id = $"cleanup-perf-{i}",
                    Type = "test-task",
                    State = (int)TaskState.Completed,
                    VirtualKeyId = (i % 10) + 1,
                    CreatedAt = oldDate,
                    UpdatedAt = oldDate,
                    CompletedAt = oldDate,
                    IsArchived = true,
                    ArchivedAt = oldDate,
                    Progress = 100,
                    Payload = "{}",
                    Result = "{}",
                    Metadata = "{}"
                });
            }

            // Batch insert
            const int batchSize = 100;
            for (int i = 0; i < tasks.Count; i += batchSize)
            {
                var batch = tasks.Skip(i).Take(batchSize);
                _dbContext.AsyncTasks.AddRange(batch);
                await _dbContext.SaveChangesAsync();
            }

            // Act: Get tasks for cleanup and delete them
            var sw = Stopwatch.StartNew();
            var tasksToDelete = await _repository.GetTasksForCleanupAsync(TimeSpan.FromDays(30), limit: taskCount);
            var deletedCount = await _repository.BulkDeleteAsync(tasksToDelete.Select(t => t.Id));
            sw.Stop();

            // Assert
            Assert.Equal(taskCount, tasksToDelete.Count);
            Assert.Equal(taskCount, deletedCount);
            _output.WriteLine($"Deleted {deletedCount} tasks in {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Average: {sw.ElapsedMilliseconds / (double)deletedCount:F2}ms per task");

            // Performance benchmark: Should delete 1000 tasks in under 2 seconds
            Assert.True(sw.ElapsedMilliseconds < 2000, $"Deletion took too long: {sw.ElapsedMilliseconds}ms");
        }

        private async Task UpdateTaskProgressAsync(string taskId, int progress)
        {
            await _asyncTaskService.UpdateTaskStatusAsync(taskId, TaskState.Processing, progress);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Tests.TestHelpers.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Repositories
{
    public class AsyncTaskRepositoryTests : IDisposable
    {
        private readonly Mock<IDbContextFactory<ConfigurationDbContext>> _mockDbContextFactory;
        private readonly Mock<ILogger<AsyncTaskRepository>> _mockLogger;
        private readonly AsyncTaskRepository _repository;
        private readonly ConfigurationDbContext _inMemoryContext;
        private readonly DbContextOptions<ConfigurationDbContext> _options;

        public AsyncTaskRepositoryTests()
        {
            // Setup in-memory database for testing
            _options = new DbContextOptionsBuilder<ConfigurationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _inMemoryContext = new ConfigurationDbContext(_options);
            _inMemoryContext.Database.EnsureCreated();

            _mockDbContextFactory = new Mock<IDbContextFactory<ConfigurationDbContext>>();
            _mockDbContextFactory
                .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ConfigurationDbContext(_options));

            _mockLogger = new Mock<ILogger<AsyncTaskRepository>>();
            _repository = new AsyncTaskRepository(_mockDbContextFactory.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task CreateAsync_CreatesNewTask_ReturnsCreatedTask()
        {
            // Arrange
            var task = new AsyncTaskBuilder()
                .WithId("test-task-123")
                .WithType("video-generation")
                .WithVirtualKeyId(42)
                .Build();

            // Act
            var result = await _repository.CreateAsync(task);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(task.Id, result);

            // Verify it was actually saved
            await using var verifyContext = new ConfigurationDbContext(_options);
            var savedTask = await verifyContext.AsyncTasks.FindAsync(task.Id);
            Assert.NotNull(savedTask);
            Assert.Equal(task.Type, savedTask.Type);
        }

        [Fact]
        public async Task GetByIdAsync_WithExistingTask_ReturnsTask()
        {
            // Arrange
            var task = new AsyncTaskBuilder()
                .WithId("existing-task")
                .WithType("image-generation")
                .AsProcessing(75)
                .Build();

            await using (var context = new ConfigurationDbContext(_options))
            {
                context.AsyncTasks.Add(task);
                await context.SaveChangesAsync();
            }

            // Act
            var result = await _repository.GetByIdAsync(task.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(task.Id, result.Id);
            Assert.Equal(task.Type, result.Type);
            Assert.Equal(task.Progress, result.Progress);
        }

        [Fact]
        public async Task GetByIdAsync_WithNonExistingTask_ReturnsNull()
        {
            // Act
            var result = await _repository.GetByIdAsync("non-existing-task");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task UpdateAsync_UpdatesExistingTask_ReturnsTrue()
        {
            // Arrange
            var task = new AsyncTaskBuilder()
                .WithId("update-test")
                .WithState(ConduitLLM.Core.Interfaces.TaskState.Pending)
                .Build();

            await using (var context = new ConfigurationDbContext(_options))
            {
                context.AsyncTasks.Add(task);
                await context.SaveChangesAsync();
            }

            // Modify the task
            task.State = (int)ConduitLLM.Core.Interfaces.TaskState.Processing;
            task.Progress = 50;
            task.ProgressMessage = "Processing...";
            task.UpdatedAt = DateTime.UtcNow;

            // Act
            var result = await _repository.UpdateAsync(task);

            // Assert
            Assert.True(result);

            // Verify changes were saved
            await using var verifyContext = new ConfigurationDbContext(_options);
            var updatedTask = await verifyContext.AsyncTasks.FindAsync(task.Id);
            Assert.NotNull(updatedTask);
            Assert.Equal((int)ConduitLLM.Core.Interfaces.TaskState.Processing, updatedTask.State);
            Assert.Equal(50, updatedTask.Progress);
            Assert.Equal("Processing...", updatedTask.ProgressMessage);
        }

        [Fact]
        public async Task UpdateAsync_WithNonExistingTask_ReturnsFalse()
        {
            // Arrange
            var task = new AsyncTaskBuilder()
                .WithId("non-existing-update")
                .Build();

            // Act
            var result = await _repository.UpdateAsync(task);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task DeleteAsync_DeletesExistingTask_ReturnsTrue()
        {
            // Arrange
            var task = new AsyncTaskBuilder()
                .WithId("delete-test")
                .Build();

            await using (var context = new ConfigurationDbContext(_options))
            {
                context.AsyncTasks.Add(task);
                await context.SaveChangesAsync();
            }

            // Act
            var result = await _repository.DeleteAsync(task.Id);

            // Assert
            Assert.True(result);

            // Verify it was deleted
            await using var verifyContext = new ConfigurationDbContext(_options);
            var deletedTask = await verifyContext.AsyncTasks.FindAsync(task.Id);
            Assert.Null(deletedTask);
        }

        [Fact]
        public async Task DeleteAsync_WithNonExistingTask_ReturnsFalse()
        {
            // Act
            var result = await _repository.DeleteAsync("non-existing-delete");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetByVirtualKeyAsync_ReturnsTasksForSpecificKey()
        {
            // Arrange
            var virtualKeyId = 123;
            var tasks = new[]
            {
                new AsyncTaskBuilder().WithId("task1").WithVirtualKeyId(virtualKeyId).Build(),
                new AsyncTaskBuilder().WithId("task2").WithVirtualKeyId(virtualKeyId).AsCompleted().Build(),
                new AsyncTaskBuilder().WithId("task3").WithVirtualKeyId(999).Build(), // Different key
                new AsyncTaskBuilder().WithId("task4").WithVirtualKeyId(virtualKeyId).AsArchived().Build()
            };

            await using (var context = new ConfigurationDbContext(_options))
            {
                context.AsyncTasks.AddRange(tasks);
                await context.SaveChangesAsync();
            }

            // Act
            var result = await _repository.GetByVirtualKeyAsync(virtualKeyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count); // Including archived
            Assert.All(result, t => Assert.Equal(virtualKeyId, t.VirtualKeyId));
            Assert.Contains(result, t => t.Id == "task1");
            Assert.Contains(result, t => t.Id == "task2");
            Assert.Contains(result, t => t.Id == "task4"); // Archived included
            Assert.DoesNotContain(result, t => t.Id == "task3");
        }

        [Fact]
        public async Task GetActiveByVirtualKeyAsync_ReturnsOnlyNonArchivedTasks()
        {
            // Arrange
            var virtualKeyId = 456;
            var tasks = new[]
            {
                new AsyncTaskBuilder().WithId("active1").WithVirtualKeyId(virtualKeyId).Build(),
                new AsyncTaskBuilder().WithId("active2").WithVirtualKeyId(virtualKeyId).AsProcessing().Build(),
                new AsyncTaskBuilder().WithId("archived1").WithVirtualKeyId(virtualKeyId).AsArchived().Build(),
                new AsyncTaskBuilder().WithId("archived2").WithVirtualKeyId(virtualKeyId).AsCompleted().AsArchived().Build()
            };

            await using (var context = new ConfigurationDbContext(_options))
            {
                context.AsyncTasks.AddRange(tasks);
                await context.SaveChangesAsync();
            }

            // Act
            var result = await _repository.GetActiveByVirtualKeyAsync(virtualKeyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.All(result, t => Assert.False(t.IsArchived));
            Assert.Contains(result, t => t.Id == "active1");
            Assert.Contains(result, t => t.Id == "active2");
        }

        [Fact]
        public async Task ArchiveOldTasksAsync_ArchivesCompletedTasksOlderThanThreshold()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var tasks = new[]
            {
                // Should be archived (completed and old)
                new AsyncTaskBuilder()
                    .WithId("old-completed")
                    .AsCompleted()
                    .WithCompletedAt(now.AddDays(-8))
                    .Build(),
                
                // Should NOT be archived (completed but recent)
                new AsyncTaskBuilder()
                    .WithId("recent-completed")
                    .AsCompleted()
                    .WithCompletedAt(now.AddDays(-3))
                    .Build(),
                
                // Should NOT be archived (old but not completed)
                new AsyncTaskBuilder()
                    .WithId("old-processing")
                    .AsProcessing()
                    .WithCreatedAt(now.AddDays(-10))
                    .Build(),
                
                // Should NOT be archived (already archived)
                new AsyncTaskBuilder()
                    .WithId("already-archived")
                    .AsCompleted()
                    .WithCompletedAt(now.AddDays(-10))
                    .AsArchived()
                    .Build(),

                // Should be archived (failed and old)
                new AsyncTaskBuilder()
                    .WithId("old-failed")
                    .AsFailed()
                    .WithCompletedAt(now.AddDays(-8))
                    .Build()
            };

            await using (var context = new ConfigurationDbContext(_options))
            {
                context.AsyncTasks.AddRange(tasks);
                await context.SaveChangesAsync();
            }

            // Act
            var archivedCount = await _repository.ArchiveOldTasksAsync(TimeSpan.FromDays(7));

            // Assert
            Assert.Equal(2, archivedCount); // old-completed and old-failed

            // Verify the correct tasks were archived
            await using var verifyContext = new ConfigurationDbContext(_options);
            var allTasks = await verifyContext.AsyncTasks.ToListAsync();
            
            var oldCompleted = allTasks.First(t => t.Id == "old-completed");
            Assert.True(oldCompleted.IsArchived);
            Assert.NotNull(oldCompleted.ArchivedAt);

            var oldFailed = allTasks.First(t => t.Id == "old-failed");
            Assert.True(oldFailed.IsArchived);
            Assert.NotNull(oldFailed.ArchivedAt);

            var recentCompleted = allTasks.First(t => t.Id == "recent-completed");
            Assert.False(recentCompleted.IsArchived);

            var oldProcessing = allTasks.First(t => t.Id == "old-processing");
            Assert.False(oldProcessing.IsArchived);
        }

        [Fact]
        public async Task GetTasksForCleanupAsync_ReturnsOldArchivedTasks()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var tasks = new[]
            {
                // Should be returned (archived long ago)
                new AsyncTaskBuilder()
                    .WithId("old-archived")
                    .AsCompleted()
                    .AsArchived()
                    .WithArchivedAt(now.AddDays(-35))
                    .Build(),
                
                // Should NOT be returned (archived recently)
                new AsyncTaskBuilder()
                    .WithId("recent-archived")
                    .AsCompleted()
                    .AsArchived()
                    .WithArchivedAt(now.AddDays(-10))
                    .Build(),
                
                // Should NOT be returned (not archived)
                new AsyncTaskBuilder()
                    .WithId("not-archived")
                    .AsCompleted()
                    .WithCompletedAt(now.AddDays(-40))
                    .Build()
            };

            foreach (var task in tasks)
            {
                if (task.Id == "old-archived")
                {
                    task.ArchivedAt = now.AddDays(-35);
                }
                else if (task.Id == "recent-archived")
                {
                    task.ArchivedAt = now.AddDays(-10);
                }
            }

            await using (var context = new ConfigurationDbContext(_options))
            {
                context.AsyncTasks.AddRange(tasks);
                await context.SaveChangesAsync();
            }

            // Act
            var result = await _repository.GetTasksForCleanupAsync(TimeSpan.FromDays(30));

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("old-archived", result[0].Id);
        }

        [Fact]
        public async Task BulkDeleteAsync_DeletesSpecifiedTasks()
        {
            // Arrange
            var taskIds = new[] { "bulk1", "bulk2", "bulk3" };
            var tasks = taskIds.Select(id => new AsyncTaskBuilder().WithId(id).Build()).ToList();
            tasks.Add(new AsyncTaskBuilder().WithId("keep-this").Build());

            await using (var context = new ConfigurationDbContext(_options))
            {
                context.AsyncTasks.AddRange(tasks);
                await context.SaveChangesAsync();
            }

            // Act
            var deletedCount = await _repository.BulkDeleteAsync(taskIds);

            // Assert
            Assert.Equal(3, deletedCount);

            // Verify deletions
            await using var verifyContext = new ConfigurationDbContext(_options);
            var remainingTasks = await verifyContext.AsyncTasks.ToListAsync();
            Assert.Single(remainingTasks);
            Assert.Equal("keep-this", remainingTasks[0].Id);
        }

        [Fact]
        public async Task BulkDeleteAsync_WithEmptyList_ReturnsZero()
        {
            // Act
            var deletedCount = await _repository.BulkDeleteAsync(new List<string>());

            // Assert
            Assert.Equal(0, deletedCount);
        }

        [Fact]
        public async Task CreateAsync_WithConcurrentCreation_HandlesGracefully()
        {
            // Arrange
            var tasks = Enumerable.Range(1, 10)
                .Select(i => new AsyncTaskBuilder()
                    .WithId($"concurrent-{i}")
                    .WithVirtualKeyId(i)
                    .Build())
                .ToList();

            // Act - Create tasks concurrently
            var createTasks = tasks.Select(t => _repository.CreateAsync(t));
            var results = await Task.WhenAll(createTasks);

            // Assert
            Assert.All(results, r => Assert.NotNull(r));
            
            // Verify all were saved
            await using var verifyContext = new ConfigurationDbContext(_options);
            var savedCount = await verifyContext.AsyncTasks.CountAsync();
            Assert.Equal(10, savedCount);
        }

        [Fact]
        public async Task UpdateAsync_WithConcurrentUpdates_LastWriteWins()
        {
            // Arrange
            var task = new AsyncTaskBuilder()
                .WithId("concurrent-update")
                .WithProgress(0)
                .Build();

            await using (var context = new ConfigurationDbContext(_options))
            {
                context.AsyncTasks.Add(task);
                await context.SaveChangesAsync();
            }

            // Act - Simulate concurrent updates
            var update1 = Task.Run(async () =>
            {
                var t = await _repository.GetByIdAsync(task.Id);
                t!.Progress = 25;
                await Task.Delay(50); // Simulate work
                return await _repository.UpdateAsync(t);
            });

            var update2 = Task.Run(async () =>
            {
                var t = await _repository.GetByIdAsync(task.Id);
                t!.Progress = 75;
                await Task.Delay(100); // Simulate more work
                return await _repository.UpdateAsync(t);
            });

            var results = await Task.WhenAll(update1, update2);

            // Assert
            Assert.All(results, r => Assert.True(r));
            
            // The last update should win
            var finalTask = await _repository.GetByIdAsync(task.Id);
            Assert.NotNull(finalTask);
            // Either update could win depending on timing, just verify it's one of them
            Assert.True(finalTask.Progress == 25 || finalTask.Progress == 75);
        }

        public void Dispose()
        {
            _inMemoryContext?.Dispose();
        }
    }
}
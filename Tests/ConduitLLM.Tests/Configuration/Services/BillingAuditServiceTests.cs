using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Services;
using System.Text.Json;

namespace ConduitLLM.Tests.Configuration.Services
{
    public class BillingAuditServiceTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly ConduitDbContext _dbContext;
        private readonly BillingAuditService _service;
        private readonly Mock<ILogger<BillingAuditService>> _mockLogger;
        private readonly SqliteConnection _connection;

        public BillingAuditServiceTests()
        {
            var services = new ServiceCollection();
            
            // Create and open a SQLite in-memory connection
            // This connection must be kept alive for the duration of the test
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            
            // Disable foreign key enforcement for tests to avoid needing parent records
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_keys = OFF";
                command.ExecuteNonQuery();
            }
            
            // Configure SQLite in-memory database
            // This solves the scoping issue - all scoped contexts will share the same database
            services.AddDbContext<ConduitDbContext>(options =>
                options.UseSqlite(_connection)
                    .EnableSensitiveDataLogging(),
                ServiceLifetime.Scoped);
            
            _mockLogger = new Mock<ILogger<BillingAuditService>>();
            
            services.AddSingleton<ILogger<BillingAuditService>>(_mockLogger.Object);
            
            _serviceProvider = services.BuildServiceProvider();
            _dbContext = _serviceProvider.GetRequiredService<ConduitDbContext>();
            
            // Create the database schema
            _dbContext.Database.EnsureCreated();
            
            _service = new BillingAuditService(_serviceProvider, _mockLogger.Object);
            
            // Start the service to enable timer-based flushing
            _service.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        [Fact]
        public async Task LogBillingEventAsync_ShouldQueueEvent()
        {
            // Arrange - Create a new service instance for this test
            var service = new BillingAuditService(_serviceProvider, _mockLogger.Object);
            await service.StartAsync(CancellationToken.None);
            
            // Add 100 events to trigger automatic flush (batch size is 100)
            for (int i = 0; i < 100; i++)
            {
                var auditEvent = new BillingAuditEvent
                {
                    EventType = BillingAuditEventType.UsageTracked,
                    VirtualKeyId = 123,
                    Model = "gpt-4",
                    CalculatedCost = 0.05m,
                    RequestId = $"test-request-{i}"
                };
                await service.LogBillingEventAsync(auditEvent);
            }
            
            // Give time for automatic batch flush
            await Task.Delay(100);
            
            // Assert - use a new scope to get the updated context since the service uses scoped contexts
            using var scope = _serviceProvider.CreateScope();
            var scopedContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

            var savedEvents = await scopedContext.BillingAuditEvents.ToListAsync();
            Assert.NotEmpty(savedEvents);
            Assert.Equal(100, savedEvents.Count);
            Assert.All(savedEvents, e => 
            {
                Assert.Equal(BillingAuditEventType.UsageTracked, e.EventType);
                Assert.Equal(123, e.VirtualKeyId);
                Assert.Equal("gpt-4", e.Model);
                Assert.Equal(0.05m, e.CalculatedCost);
            });
            
            // Cleanup
            await service.StopAsync(CancellationToken.None);
            service.Dispose();
        }

        [Fact]
        public void LogBillingEvent_FireAndForget_ShouldNotThrow()
        {
            // Arrange
            var auditEvent = new BillingAuditEvent
            {
                EventType = BillingAuditEventType.ZeroCostSkipped,
                VirtualKeyId = 456,
                Model = "gpt-3.5-turbo"
            };

            // Act & Assert - should not throw
            var exception = Record.Exception(() => _service.LogBillingEvent(auditEvent));
            Assert.Null(exception);
        }

        [Fact]
        public async Task BatchProcessing_ShouldFlushAt100Events()
        {
            // Arrange
            var events = new List<BillingAuditEvent>();
            for (int i = 0; i < 100; i++)
            {
                events.Add(new BillingAuditEvent
                {
                    EventType = BillingAuditEventType.UsageTracked,
                    VirtualKeyId = i,
                    Model = $"model-{i}",
                    RequestId = $"request-{i}"
                });
            }

            // Act
            foreach (var evt in events)
            {
                await _service.LogBillingEventAsync(evt);
            }
            
            // Give time for batch to flush (should be immediate at batch size)
            await Task.Delay(100);

            // Assert using scoped context
            using var scope = _serviceProvider.CreateScope();
            var scopedContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();
            var savedCount = await scopedContext.BillingAuditEvents.CountAsync();
            Assert.Equal(100, savedCount);
        }

        [Fact]
        public async Task StartAsync_ShouldStartFlushTimer()
        {
            // Arrange - Create a new service and logger mock for this test
            var mockLogger = new Mock<ILogger<BillingAuditService>>();
            var service = new BillingAuditService(_serviceProvider, mockLogger.Object);
            
            // Act
            await service.StartAsync(CancellationToken.None);
            
            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting BillingAuditService")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task StopAsync_ShouldFlushRemainingEvents()
        {
            // Arrange - Create a new service instance for this test
            var service = new BillingAuditService(_serviceProvider, _mockLogger.Object);
            await service.StartAsync(CancellationToken.None);
            
            var auditEvent = new BillingAuditEvent
            {
                EventType = BillingAuditEventType.UsageTracked,
                VirtualKeyId = 999,
                Model = "final-model"
            };
            
            await service.LogBillingEventAsync(auditEvent);
            
            // Act
            await service.StopAsync(CancellationToken.None);
            
            // Assert using scoped context
            using var scope = _serviceProvider.CreateScope();
            var scopedContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();
            var savedEvent = await scopedContext.BillingAuditEvents
                .FirstOrDefaultAsync(e => e.VirtualKeyId == 999);
            Assert.NotNull(savedEvent);
            Assert.Equal("final-model", savedEvent.Model);
            
            // Cleanup
            service.Dispose();
        }

        [Fact]
        public async Task FlushEventsAsync_ShouldRequeueEventsAfterTransientDatabaseFailure()
        {
            var auditEvent = new BillingAuditEvent
            {
                EventType = BillingAuditEventType.SpendUpdateFailed,
                VirtualKeyId = 1016,
                Model = "transient-failure-model",
                CalculatedCost = 0.25m
            };

            await _service.LogBillingEventAsync(auditEvent);

            _connection.Close();
            await _service.FlushEventsAsync();

            _connection.Open();
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_keys = OFF";
                command.ExecuteNonQuery();
            }
            using (var recoveryScope = _serviceProvider.CreateScope())
            {
                var recoveryContext = recoveryScope.ServiceProvider.GetRequiredService<ConduitDbContext>();
                await recoveryContext.Database.EnsureCreatedAsync();
            }
            await _service.FlushEventsAsync();

            using var scope = _serviceProvider.CreateScope();
            var scopedContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();
            var savedEvents = await scopedContext.BillingAuditEvents
                .Where(e => e.VirtualKeyId == 1016)
                .ToListAsync();

            var savedEvent = Assert.Single(savedEvents);
            Assert.Equal(BillingAuditEventType.SpendUpdateFailed, savedEvent.EventType);
            Assert.Equal(0.25m, savedEvent.CalculatedCost);
        }

        [Fact]
        public void LogBillingEvent_WithNullEvent_ShouldNotThrow()
        {
            // Act & Assert
            var exception = Record.Exception(() => _service.LogBillingEvent(null!));
            Assert.Null(exception);
        }

        [Fact]
        public async Task LogBillingEventAsync_WithNullEvent_ShouldThrow()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _service.LogBillingEventAsync(null!));
        }

        public void Dispose()
        {
            // Stop and flush the service before disposing
            _service?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            _service?.Dispose();
            _dbContext?.Dispose();
            _serviceProvider?.Dispose();
            _connection?.Close();
            _connection?.Dispose();
        }
    }
}

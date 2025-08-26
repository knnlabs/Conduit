using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        public BillingAuditServiceTests()
        {
            var services = new ServiceCollection();
            
            // Configure in-memory database - use same database name for all contexts
            // This ensures data persists across different scoped DbContext instances
            var databaseName = $"BillingAuditTestDb_{Guid.NewGuid()}";
            services.AddDbContext<ConduitDbContext>(options =>
                options.UseInMemoryDatabase(databaseName: databaseName)
                    .EnableSensitiveDataLogging(),
                ServiceLifetime.Scoped);
            
            _mockLogger = new Mock<ILogger<BillingAuditService>>();
            
            services.AddSingleton<ILogger<BillingAuditService>>(_mockLogger.Object);
            
            _serviceProvider = services.BuildServiceProvider();
            _dbContext = _serviceProvider.GetRequiredService<ConduitDbContext>();
            
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
        public async Task GetAuditEventsAsync_ShouldFilterByEventType()
        {
            // Arrange
            await SeedTestData();
            
            // Act
            var (events, totalCount) = await _service.GetAuditEventsAsync(
                from: DateTime.UtcNow.AddDays(-7),
                to: DateTime.UtcNow.AddDays(1),
                eventType: BillingAuditEventType.UsageTracked,
                virtualKeyId: null,
                pageNumber: 1,
                pageSize: 10);

            // Assert
            Assert.Equal(3, totalCount); // Only UsageTracked events
            Assert.All(events, e => Assert.Equal(BillingAuditEventType.UsageTracked, e.EventType));
        }

        [Fact]
        public async Task GetAuditEventsAsync_ShouldFilterByVirtualKeyId()
        {
            // Arrange
            await SeedTestData();
            
            // Act
            var (events, totalCount) = await _service.GetAuditEventsAsync(
                from: DateTime.UtcNow.AddDays(-7),
                to: DateTime.UtcNow.AddDays(1),
                eventType: null,
                virtualKeyId: 123,
                pageNumber: 1,
                pageSize: 10);

            // Assert
            Assert.Equal(2, totalCount); // Events for virtualKeyId 123
            Assert.All(events, e => Assert.Equal(123, e.VirtualKeyId));
        }

        [Fact]
        public async Task GetAuditEventsAsync_ShouldPaginateResults()
        {
            // Arrange
            await SeedTestData();
            
            // Act
            var (eventsPage1, totalCount1) = await _service.GetAuditEventsAsync(
                from: DateTime.UtcNow.AddDays(-7),
                to: DateTime.UtcNow.AddDays(1),
                eventType: null,
                virtualKeyId: null,
                pageNumber: 1,
                pageSize: 2);
                
            var (eventsPage2, totalCount2) = await _service.GetAuditEventsAsync(
                from: DateTime.UtcNow.AddDays(-7),
                to: DateTime.UtcNow.AddDays(1),
                eventType: null,
                virtualKeyId: null,
                pageNumber: 2,
                pageSize: 2);

            // Assert
            Assert.Equal(5, totalCount1); // Total events
            Assert.Equal(5, totalCount2); // Total should be same
            Assert.Equal(2, eventsPage1.Count);
            Assert.Equal(2, eventsPage2.Count);
            Assert.NotEqual(eventsPage1[0].Id, eventsPage2[0].Id); // Different events
        }

        [Fact]
        public async Task GetAuditSummaryAsync_ShouldCalculateCorrectStats()
        {
            // Arrange
            await SeedTestData();
            
            // Act
            var summary = await _service.GetAuditSummaryAsync(
                from: DateTime.UtcNow.AddDays(-7),
                to: DateTime.UtcNow.AddDays(1),
                virtualKeyId: null);

            // Assert
            Assert.Equal(5, summary.TotalEvents);
            Assert.Equal(3, summary.SuccessfulBillings);
            Assert.Equal(1, summary.ZeroCostSkipped);
            Assert.Equal(0, summary.EstimatedUsages);
            Assert.Equal(1, summary.MissingUsageData);
            Assert.Equal(0.15m, summary.TotalBilledAmount); // 0.05 + 0.05 + 0.05
            Assert.Equal(0m, summary.PotentialRevenueLoss); // No revenue loss in test data
        }

        [Fact]
        public async Task GetPotentialRevenueLossAsync_ShouldCalculateLoss()
        {
            // Arrange - use scoped context for seeding
            using (var scope = _serviceProvider.CreateScope())
            {
                var scopedContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();
                await scopedContext.BillingAuditEvents.AddRangeAsync(
                    new BillingAuditEvent
                    {
                        EventType = BillingAuditEventType.SpendUpdateFailed,
                        CalculatedCost = 0.10m,
                        Timestamp = DateTime.UtcNow.AddHours(-1)
                    },
                    new BillingAuditEvent
                    {
                        EventType = BillingAuditEventType.MissingCostConfig,
                        CalculatedCost = 0.20m,
                        Timestamp = DateTime.UtcNow.AddHours(-2)
                    }
                );
                await scopedContext.SaveChangesAsync();
            }
            
            // Act
            var loss = await _service.GetPotentialRevenueLossAsync(
                from: DateTime.UtcNow.AddDays(-1),
                to: DateTime.UtcNow.AddDays(1));

            // Assert
            Assert.Equal(0.30m, loss); // 0.10 + 0.20
        }

        [Fact]
        public async Task DetectAnomaliesAsync_ShouldDetectHighFailureRate()
        {
            // Arrange - Create events with >5% failure rate
            var events = new List<BillingAuditEvent>();
            
            // 90 successful events
            for (int i = 0; i < 90; i++)
            {
                events.Add(new BillingAuditEvent
                {
                    EventType = BillingAuditEventType.UsageTracked,
                    Timestamp = DateTime.UtcNow.AddMinutes(-i)
                });
            }
            
            // 10 failed events (10% failure rate)
            for (int i = 0; i < 10; i++)
            {
                events.Add(new BillingAuditEvent
                {
                    EventType = BillingAuditEventType.SpendUpdateFailed,
                    Timestamp = DateTime.UtcNow.AddMinutes(-i),
                    CalculatedCost = 0.01m
                });
            }
            
            // Use scoped context for seeding
            using (var scope = _serviceProvider.CreateScope())
            {
                var scopedContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();
                await scopedContext.BillingAuditEvents.AddRangeAsync(events);
                await scopedContext.SaveChangesAsync();
            }
            
            // Act
            var anomalies = await _service.DetectAnomaliesAsync(
                from: DateTime.UtcNow.AddHours(-2),
                to: DateTime.UtcNow.AddMinutes(1));

            // Assert
            Assert.NotEmpty(anomalies);
            var highFailureAnomaly = anomalies.FirstOrDefault(a => a.AnomalyType == "HighFailureRate");
            Assert.NotNull(highFailureAnomaly);
            Assert.Equal("High", highFailureAnomaly.Severity);
            Assert.True(highFailureAnomaly.EstimatedImpact > 0);
        }

        [Fact]
        public async Task DetectAnomaliesAsync_ShouldDetectMissingModelConfig()
        {
            // Arrange - Create multiple events for same model with missing config
            var events = new List<BillingAuditEvent>();
            for (int i = 0; i < 15; i++)
            {
                events.Add(new BillingAuditEvent
                {
                    EventType = BillingAuditEventType.MissingCostConfig,
                    Model = "new-model-xyz",
                    Timestamp = DateTime.UtcNow.AddMinutes(-i)
                });
            }
            
            // Use scoped context for seeding
            using (var scope = _serviceProvider.CreateScope())
            {
                var scopedContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();
                await scopedContext.BillingAuditEvents.AddRangeAsync(events);
                await scopedContext.SaveChangesAsync();
            }
            
            // Act
            var anomalies = await _service.DetectAnomaliesAsync(
                from: DateTime.UtcNow.AddHours(-1),
                to: DateTime.UtcNow.AddMinutes(1));

            // Assert
            var missingConfigAnomaly = anomalies.FirstOrDefault(a => a.AnomalyType == "MissingModelConfiguration");
            Assert.NotNull(missingConfigAnomaly);
            Assert.Contains("new-model-xyz", missingConfigAnomaly.Description);
            Assert.Equal("Medium", missingConfigAnomaly.Severity);
        }

        [Fact]
        public async Task StartAsync_ShouldStartFlushTimer()
        {
            // Act
            await _service.StartAsync(CancellationToken.None);
            
            // Assert
            _mockLogger.Verify(
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

        private async Task SeedTestData()
        {
            var events = new List<BillingAuditEvent>
            {
                new BillingAuditEvent
                {
                    EventType = BillingAuditEventType.UsageTracked,
                    VirtualKeyId = 123,
                    Model = "gpt-4",
                    CalculatedCost = 0.05m,
                    Timestamp = DateTime.UtcNow.AddHours(-1)
                },
                new BillingAuditEvent
                {
                    EventType = BillingAuditEventType.UsageTracked,
                    VirtualKeyId = 123,
                    Model = "gpt-4",
                    CalculatedCost = 0.05m,
                    Timestamp = DateTime.UtcNow.AddHours(-2)
                },
                new BillingAuditEvent
                {
                    EventType = BillingAuditEventType.UsageTracked,
                    VirtualKeyId = 456,
                    Model = "gpt-3.5-turbo",
                    CalculatedCost = 0.05m,
                    Timestamp = DateTime.UtcNow.AddHours(-3)
                },
                new BillingAuditEvent
                {
                    EventType = BillingAuditEventType.ZeroCostSkipped,
                    VirtualKeyId = 456,
                    Model = "free-model",
                    CalculatedCost = 0m,
                    Timestamp = DateTime.UtcNow.AddHours(-4)
                },
                new BillingAuditEvent
                {
                    EventType = BillingAuditEventType.MissingUsageData,
                    VirtualKeyId = 789,
                    Timestamp = DateTime.UtcNow.AddHours(-5)
                }
            };

            // Use a scoped context to ensure data is available to the service
            using var scope = _serviceProvider.CreateScope();
            var scopedContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();
            await scopedContext.BillingAuditEvents.AddRangeAsync(events);
            await scopedContext.SaveChangesAsync();
        }

        public void Dispose()
        {
            // Stop and flush the service before disposing
            _service?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            _service?.Dispose();
            _dbContext?.Dispose();
            _serviceProvider?.Dispose();
        }
    }
}
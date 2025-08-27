/*
 * BillingAuditService Performance Benchmarks
 * 
 * To run these benchmarks:
 * 1. Create a simple console app with BenchmarkDotNet:
 *    dotnet new console -n BillingBenchmarks
 *    cd BillingBenchmarks
 *    dotnet add package BenchmarkDotNet
 *    dotnet add reference ../ConduitLLM.Tests/ConduitLLM.Tests.csproj
 * 
 * 2. Create Program.cs with:
 *    using BenchmarkDotNet.Running;
 *    using ConduitLLM.Tests.Configuration.Services;
 *    
 *    BenchmarkRunner.Run<BillingAuditServiceBenchmarks>();
 * 
 * 3. Run benchmarks:
 *    dotnet run -c Release
 * 
 * Expected results:
 * - Sequential logging: >1000 events/second
 * - Concurrent logging: >2000 events/second (with parallelism)
 * - Query performance: <100ms for 100K events
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Services;

namespace ConduitLLM.Tests.Configuration.Services
{
    /// <summary>
    /// Performance benchmarks for BillingAuditService to validate high-volume throughput
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net90, warmupCount: 3, iterationCount: 5)]
    public class BillingAuditServiceBenchmarks : IDisposable
    {
        private ServiceProvider _serviceProvider;
        private BillingAuditService _service;
        private SqliteConnection _connection;
        private Mock<ILogger<BillingAuditService>> _mockLogger;

        [GlobalSetup]
        public void Setup()
        {
            var services = new ServiceCollection();
            
            // Create and open a SQLite in-memory connection
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            
            // Disable foreign key enforcement for tests
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_keys = OFF";
                command.ExecuteNonQuery();
            }
            
            // Configure SQLite in-memory database
            services.AddDbContext<ConduitDbContext>(options =>
                options.UseSqlite(_connection)
                    .EnableSensitiveDataLogging(false), // Disable for performance
                ServiceLifetime.Scoped);
            
            _mockLogger = new Mock<ILogger<BillingAuditService>>();
            services.AddSingleton<ILogger<BillingAuditService>>(_mockLogger.Object);
            
            _serviceProvider = services.BuildServiceProvider();
            
            // Create the database schema
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();
                dbContext.Database.EnsureCreated();
            }
            
            _service = new BillingAuditService(_serviceProvider, _mockLogger.Object);
            _service.StartAsync(default).GetAwaiter().GetResult();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            Dispose();
        }

        /// <summary>
        /// Benchmark logging 1000 events sequentially
        /// Target: >1000 events/second throughput
        /// </summary>
        [Benchmark]
        public async Task BulkLogEvents_1000Events_Sequential()
        {
            for (int i = 0; i < 1000; i++)
            {
                await _service.LogBillingEventAsync(new BillingAuditEvent
                {
                    EventType = BillingAuditEventType.UsageTracked,
                    VirtualKeyId = i % 100,
                    Model = $"gpt-4-{i % 10}",
                    CalculatedCost = 0.001m * (i % 10),
                    RequestId = $"perf-test-{i}",
                    Timestamp = DateTime.UtcNow.AddSeconds(-i)
                });
            }
            
            // Force flush remaining events
            await _service.StopAsync(default);
            
            // Restart for next iteration
            _service = new BillingAuditService(_serviceProvider, _mockLogger.Object);
            await _service.StartAsync(default);
        }

        /// <summary>
        /// Benchmark logging 1000 events concurrently
        /// Tests thread safety and concurrent throughput
        /// </summary>
        [Benchmark]
        public async Task BulkLogEvents_1000Events_Concurrent()
        {
            var tasks = new List<Task>();
            
            for (int i = 0; i < 1000; i++)
            {
                var index = i;
                tasks.Add(Task.Run(async () =>
                {
                    await _service.LogBillingEventAsync(new BillingAuditEvent
                    {
                        EventType = BillingAuditEventType.UsageTracked,
                        VirtualKeyId = index % 100,
                        Model = $"gpt-4-{index % 10}",
                        CalculatedCost = 0.001m * (index % 10),
                        RequestId = $"concurrent-test-{index}",
                        Timestamp = DateTime.UtcNow.AddSeconds(-index)
                    });
                }));
            }
            
            await Task.WhenAll(tasks);
            
            // Force flush remaining events
            await _service.StopAsync(default);
            
            // Restart for next iteration
            _service = new BillingAuditService(_serviceProvider, _mockLogger.Object);
            await _service.StartAsync(default);
        }

        /// <summary>
        /// Benchmark concurrent logging from multiple threads
        /// Simulates real-world multi-threaded API usage
        /// </summary>
        [Benchmark]
        public async Task ConcurrentLogging_MultipleThreads()
        {
            const int threadCount = 10;
            const int eventsPerThread = 100;
            
            var tasks = new Task[threadCount];
            
            for (int t = 0; t < threadCount; t++)
            {
                var threadId = t;
                tasks[t] = Task.Run(async () =>
                {
                    for (int i = 0; i < eventsPerThread; i++)
                    {
                        await _service.LogBillingEventAsync(new BillingAuditEvent
                        {
                            EventType = BillingAuditEventType.UsageTracked,
                            VirtualKeyId = threadId * 100 + i,
                            Model = $"model-thread-{threadId}",
                            CalculatedCost = 0.001m * i,
                            RequestId = $"thread-{threadId}-event-{i}",
                            Timestamp = DateTime.UtcNow
                        });
                        
                        // Simulate some processing time
                        if (i % 10 == 0)
                            await Task.Delay(1);
                    }
                });
            }
            
            await Task.WhenAll(tasks);
            
            // Force flush
            await _service.StopAsync(default);
            
            // Restart for next iteration
            _service = new BillingAuditService(_serviceProvider, _mockLogger.Object);
            await _service.StartAsync(default);
        }

        /// <summary>
        /// Benchmark query performance with large dataset
        /// Tests retrieval speed with 100K+ events
        /// </summary>
        [Benchmark]
        public async Task QueryPerformance_LargeDataset()
        {
            // Seed large dataset (only once per benchmark run)
            if (!await HasLargeDataset())
            {
                await SeedLargeDataset();
            }
            
            // Perform various query operations
            var fromDate = DateTime.UtcNow.AddDays(-7);
            var toDate = DateTime.UtcNow;
            
            // Query by event type
            var (events1, count1) = await _service.GetAuditEventsAsync(
                from: fromDate,
                to: toDate,
                eventType: BillingAuditEventType.UsageTracked,
                virtualKeyId: null,
                pageNumber: 1,
                pageSize: 100);
            
            // Query by virtual key
            var (events2, count2) = await _service.GetAuditEventsAsync(
                from: fromDate,
                to: toDate,
                eventType: null,
                virtualKeyId: 50,
                pageNumber: 1,
                pageSize: 100);
            
            // Get summary statistics
            var summary = await _service.GetAuditSummaryAsync(
                from: fromDate,
                to: toDate,
                virtualKeyId: null);
            
            // Detect anomalies
            var anomalies = await _service.DetectAnomaliesAsync(
                from: fromDate,
                to: toDate);
        }

        private async Task<bool> HasLargeDataset()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();
            var count = await dbContext.BillingAuditEvents.CountAsync();
            return count >= 10000;
        }

        private async Task SeedLargeDataset()
        {
            const int batchSize = 1000;
            const int totalEvents = 10000;
            
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();
            
            for (int batch = 0; batch < totalEvents / batchSize; batch++)
            {
                var events = new List<BillingAuditEvent>();
                
                for (int i = 0; i < batchSize; i++)
                {
                    var index = batch * batchSize + i;
                    var eventType = (BillingAuditEventType)((index % 11) + 1);
                    
                    events.Add(new BillingAuditEvent
                    {
                        EventType = eventType,
                        VirtualKeyId = index % 100,
                        Model = $"model-{index % 20}",
                        CalculatedCost = eventType == BillingAuditEventType.UsageTracked ? 0.001m * (index % 100) : 0m,
                        RequestId = $"seed-{index}",
                        Timestamp = DateTime.UtcNow.AddHours(-index),
                        ProviderType = $"provider-{index % 5}",
                        HttpStatusCode = 200 + (index % 300),
                        IsEstimated = index % 10 == 0
                    });
                }
                
                await dbContext.BillingAuditEvents.AddRangeAsync(events);
                await dbContext.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Benchmark fire-and-forget logging performance
        /// Tests the synchronous logging method used for non-critical paths
        /// </summary>
        [Benchmark]
        public void FireAndForget_1000Events()
        {
            for (int i = 0; i < 1000; i++)
            {
                _service.LogBillingEvent(new BillingAuditEvent
                {
                    EventType = BillingAuditEventType.ZeroCostSkipped,
                    VirtualKeyId = i % 50,
                    Model = $"free-model-{i % 5}",
                    CalculatedCost = 0m,
                    RequestId = $"fire-forget-{i}"
                });
            }
            
            // Force flush
            _service.StopAsync(default).GetAwaiter().GetResult();
            
            // Restart for next iteration
            _service = new BillingAuditService(_serviceProvider, _mockLogger.Object);
            _service.StartAsync(default).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            _service?.StopAsync(default).GetAwaiter().GetResult();
            _service?.Dispose();
            _serviceProvider?.Dispose();
            _connection?.Close();
            _connection?.Dispose();
        }
    }
}
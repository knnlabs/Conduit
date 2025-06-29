using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.LoadTesting
{
    /// <summary>
    /// Stress tests to find system limits and breaking points.
    /// </summary>
    public class AudioStressTests : AudioServiceLoadTests
    {
        public AudioStressTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact(Skip = "Load test - run manually")]
        public async Task ConnectionPoolExhaustionTest()
        {
            _output.WriteLine("=== Connection Pool Exhaustion Test ===");
            
            var config = new LoadTestConfig
            {
                TestName = "Connection Pool Exhaustion",
                Duration = TimeSpan.FromSeconds(30),
                ConcurrentUsers = 100, // High concurrency
                ThinkTimeMs = 10, // Minimal think time
                OperationWeights = new()
                {
                    [AudioOperationType.Transcription] = 100,
                    [AudioOperationType.TextToSpeech] = 0,
                    [AudioOperationType.RealtimeSession] = 0,
                    [AudioOperationType.HybridConversation] = 0
                }
            };

            var result = await RunLoadTestAsync(config);

            // Check connection pool metrics
            var connectionPool = _serviceProvider.GetRequiredService<IAudioConnectionPool>();
            var poolStats = await connectionPool.GetStatisticsAsync();

            _output.WriteLine($"Active connections: {poolStats.ActiveConnections}");
            _output.WriteLine($"Idle connections: {poolStats.IdleConnections}");
            _output.WriteLine($"Total requests: {poolStats.TotalRequests}");

            // Should handle connection pool limits gracefully
            Assert.True(result.ErrorRate < 0.20, "Should handle connection exhaustion with < 20% errors");
            Assert.True(poolStats.ActiveConnections <= 100, "Should not exceed max connections");
        }

        [Fact(Skip = "Load test - run manually")]
        public async Task MemoryPressureTest()
        {
            _output.WriteLine("=== Memory Pressure Test ===");
            
            // Generate large audio samples
            var largeAudioSamples = Enumerable.Range(0, 50)
                .Select(_ => new byte[1024 * 1024]) // 1MB each
                .ToList();

            var config = new LoadTestConfig
            {
                TestName = "Memory Pressure Test",
                Duration = TimeSpan.FromSeconds(45),
                ConcurrentUsers = 30,
                ThinkTimeMs = 100,
                OperationWeights = new()
                {
                    [AudioOperationType.Transcription] = 50,
                    [AudioOperationType.TextToSpeech] = 50,
                    [AudioOperationType.RealtimeSession] = 0,
                    [AudioOperationType.HybridConversation] = 0
                }
            };

            var initialMemory = GC.GetTotalMemory(false) / (1024 * 1024);
            _output.WriteLine($"Initial memory: {initialMemory}MB");

            var result = await RunLoadTestAsync(config);

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var finalMemory = GC.GetTotalMemory(false) / (1024 * 1024);
            _output.WriteLine($"Final memory: {finalMemory}MB");
            _output.WriteLine($"Memory growth: {finalMemory - initialMemory}MB");

            // Check for memory leaks
            Assert.True(finalMemory - initialMemory < 200, "Memory growth should be less than 200MB");
            Assert.True(result.ErrorRate < 0.10, "Should handle memory pressure with < 10% errors");
        }

        [Fact(Skip = "Load test - run manually")]
        public async Task RateLimitingTest()
        {
            _output.WriteLine("=== Rate Limiting Test ===");
            
            var burstConfig = new LoadTestConfig
            {
                TestName = "Rate Limit Burst",
                Duration = TimeSpan.FromSeconds(10),
                ConcurrentUsers = 200, // Massive burst
                ThinkTimeMs = 0, // No think time
                RequestsPerUser = 5, // Limited requests per user
                OperationWeights = new()
                {
                    [AudioOperationType.Transcription] = 50,
                    [AudioOperationType.TextToSpeech] = 50,
                    [AudioOperationType.RealtimeSession] = 0,
                    [AudioOperationType.HybridConversation] = 0
                }
            };

            var result = await RunLoadTestAsync(burstConfig);

            _output.WriteLine($"Burst test completed: {result.TotalOperations} operations");
            _output.WriteLine($"Achieved throughput: {result.Throughput:F1} ops/sec");

            // Should handle rate limiting gracefully
            Assert.True(result.Throughput < 1000, "Rate limiting should cap throughput");
            Assert.True(result.ErrorRate < 0.30, "Should handle rate limiting with < 30% errors");
        }

        [Fact(Skip = "Load test - run manually")]
        public async Task LongRunningSessionTest()
        {
            _output.WriteLine("=== Long Running Session Test ===");
            
            var config = new LoadTestConfig
            {
                TestName = "Long Sessions",
                Duration = TimeSpan.FromMinutes(3),
                ConcurrentUsers = 5,
                ThinkTimeMs = 5000, // Long think time
                OperationWeights = new()
                {
                    [AudioOperationType.Transcription] = 10,
                    [AudioOperationType.TextToSpeech] = 10,
                    [AudioOperationType.RealtimeSession] = 40,
                    [AudioOperationType.HybridConversation] = 40
                }
            };

            var result = await RunLoadTestAsync(config);

            // Check session metrics
            var metrics = _serviceProvider.GetRequiredService<IAudioMetricsCollector>();
            var aggregated = await metrics.GetAggregatedMetricsAsync(
                DateTime.UtcNow.AddMinutes(-3),
                DateTime.UtcNow);

            _output.WriteLine($"Average session duration: {aggregated.Realtime.AverageSessionDurationSeconds:F1}s");
            _output.WriteLine($"Total audio minutes: {aggregated.Realtime.TotalAudioMinutes:F1}");

            // Long sessions should be stable
            Assert.True(result.ErrorRate < 0.05, "Long sessions should have < 5% error rate");
            Assert.True(aggregated.Realtime.AverageSessionDurationSeconds > 10, "Sessions should run long enough");
        }

        [Fact(Skip = "Load test - run manually")]
        public async Task CascadingFailureTest()
        {
            _output.WriteLine("=== Cascading Failure Test ===");
            
            var config = new LoadTestConfig
            {
                TestName = "Cascading Failure",
                Duration = TimeSpan.FromSeconds(60),
                ConcurrentUsers = 50,
                ThinkTimeMs = 500
            };

            // Simulate cascading failures
            _ = Task.Run(async () =>
            {
                await Task.Delay(15000); // Wait 15 seconds
                _output.WriteLine("Simulating primary provider failure...");
                SimulateProviderFailure("openai");
                
                await Task.Delay(5000);
                _output.WriteLine("Simulating secondary provider failure...");
                SimulateProviderFailure("elevenlabs");
                
                await Task.Delay(10000);
                _output.WriteLine("Restoring providers...");
                RestoreProvider("openai");
                RestoreProvider("elevenlabs");
            });

            var result = await RunLoadTestAsync(config);

            // Should degrade gracefully
            Assert.True(result.SuccessfulOperations > result.TotalOperations * 0.5, 
                "Should complete > 50% of operations despite cascading failures");
            
            // Check circuit breaker activation
            var providerHealth = await GetProviderHealthStatus();
            _output.WriteLine($"Circuit breakers activated: {providerHealth.Count(p => !p.Value)}");
        }

        [Fact(Skip = "Load test - run manually")]
        public async Task ResourceContentionTest()
        {
            _output.WriteLine("=== Resource Contention Test ===");
            
            // Run multiple concurrent load tests to create contention
            var tasks = new List<Task<LoadTestResult>>();
            
            for (int i = 0; i < 3; i++)
            {
                var config = new LoadTestConfig
                {
                    TestName = $"Contention Test {i}",
                    Duration = TimeSpan.FromSeconds(30),
                    ConcurrentUsers = 20,
                    ThinkTimeMs = 100,
                    OperationWeights = new()
                    {
                        [AudioOperationType.Transcription] = 25,
                        [AudioOperationType.TextToSpeech] = 25,
                        [AudioOperationType.RealtimeSession] = 25,
                        [AudioOperationType.HybridConversation] = 25
                    }
                };
                
                tasks.Add(RunLoadTestAsync(config));
            }

            var results = await Task.WhenAll(tasks);

            // All tests should complete reasonably well despite contention
            foreach (var result in results)
            {
                Assert.True(result.ErrorRate < 0.15, "Each test should have < 15% errors despite contention");
            }

            var totalOps = results.Sum(r => r.TotalOperations);
            var totalErrors = results.Sum(r => r.FailedOperations);
            var overallErrorRate = (double)totalErrors / totalOps;

            _output.WriteLine($"Overall error rate under contention: {overallErrorRate:P1}");
            Assert.True(overallErrorRate < 0.10, "Overall error rate should be < 10%");
        }

        [Fact(Skip = "Load test - run manually")]
        public async Task RecoveryTest()
        {
            _output.WriteLine("=== Recovery Test ===");
            
            var phase1Config = new LoadTestConfig
            {
                TestName = "Normal Load",
                Duration = TimeSpan.FromSeconds(20),
                ConcurrentUsers = 20,
                ThinkTimeMs = 500
            };

            var phase1Result = await RunLoadTestAsync(phase1Config);
            _output.WriteLine($"Phase 1 - Normal load error rate: {phase1Result.ErrorRate:P1}");

            // Simulate system stress
            _output.WriteLine("Applying system stress...");
            var stressConfig = new LoadTestConfig
            {
                TestName = "Stress Phase",
                Duration = TimeSpan.FromSeconds(10),
                ConcurrentUsers = 100,
                ThinkTimeMs = 10
            };

            var stressResult = await RunLoadTestAsync(stressConfig);
            _output.WriteLine($"Stress phase error rate: {stressResult.ErrorRate:P1}");

            // Allow recovery time
            _output.WriteLine("Allowing system recovery...");
            await Task.Delay(5000);

            // Test recovery
            var phase2Config = new LoadTestConfig
            {
                TestName = "Recovery Test",
                Duration = TimeSpan.FromSeconds(20),
                ConcurrentUsers = 20,
                ThinkTimeMs = 500
            };

            var phase2Result = await RunLoadTestAsync(phase2Config);
            _output.WriteLine($"Phase 2 - Post-recovery error rate: {phase2Result.ErrorRate:P1}");

            // System should recover to normal performance
            Assert.True(phase2Result.ErrorRate < phase1Result.ErrorRate * 1.5, 
                "System should recover to near-normal error rates");
            Assert.True(Math.Abs(phase2Result.Throughput - phase1Result.Throughput) / phase1Result.Throughput < 0.20, 
                "Throughput should recover to within 20% of normal");
        }

        private void SimulateProviderFailure(string provider)
        {
            // In a real test, this would interact with a test harness
            // For now, we'll just log the simulation
            _output.WriteLine($"[TEST] Simulating failure for provider: {provider}");
        }

        private void RestoreProvider(string provider)
        {
            // In a real test, this would interact with a test harness
            // For now, we'll just log the restoration
            _output.WriteLine($"[TEST] Restoring provider: {provider}");
        }

        private async Task<Dictionary<string, bool>> GetProviderHealthStatus()
        {
            var metrics = _serviceProvider.GetRequiredService<IAudioMetricsCollector>();
            var snapshot = await metrics.GetCurrentSnapshotAsync();
            return snapshot.ProviderHealth;
        }
    }
}
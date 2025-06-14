using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.LoadTesting
{
    /// <summary>
    /// Base class for audio service load tests.
    /// </summary>
    public abstract class AudioLoadTestBase
    {
        protected readonly ITestOutputHelper _output;
        protected readonly IServiceProvider _serviceProvider;
        protected readonly ILogger<AudioLoadTestBase> _logger;
        protected readonly LoadTestMetrics _metrics = new();

        protected AudioLoadTestBase(ITestOutputHelper output)
        {
            _output = output;
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
            _logger = _serviceProvider.GetRequiredService<ILogger<AudioLoadTestBase>>();
        }

        protected abstract void ConfigureServices(IServiceCollection services);

        /// <summary>
        /// Runs a load test with the specified configuration.
        /// </summary>
        protected async Task<LoadTestResult> RunLoadTestAsync(LoadTestConfig config)
        {
            _output.WriteLine($"Starting load test: {config.TestName}");
            _output.WriteLine($"Duration: {config.Duration}");
            _output.WriteLine($"Concurrent users: {config.ConcurrentUsers}");
            _output.WriteLine($"Requests per user: {config.RequestsPerUser}");

            _metrics.Reset();
            var stopwatch = Stopwatch.StartNew();
            var cancellationTokenSource = new CancellationTokenSource(config.Duration);
            var tasks = new List<Task>();

            // Start user simulation tasks
            for (int i = 0; i < config.ConcurrentUsers; i++)
            {
                var userId = i;
                tasks.Add(SimulateUserAsync(userId, config, cancellationTokenSource.Token));
            }

            // Wait for all tasks to complete or timeout
            await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Calculate results
            var result = CalculateResults(stopwatch.Elapsed);

            // Print summary
            PrintTestSummary(config, result);

            return result;
        }

        /// <summary>
        /// Simulates a single user making requests.
        /// </summary>
        private async Task SimulateUserAsync(
            int userId,
            LoadTestConfig config,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("User {UserId} started", userId);
            var random = new Random(userId);
            var requestCount = 0;

            try
            {
                while (!cancellationToken.IsCancellationRequested &&
                       (config.RequestsPerUser == -1 || requestCount < config.RequestsPerUser))
                {
                    var operationType = SelectOperation(random, config.OperationWeights);
                    await ExecuteOperationAsync(userId, operationType, cancellationToken);

                    requestCount++;

                    // Apply think time
                    if (config.ThinkTimeMs > 0)
                    {
                        var thinkTime = random.Next(config.ThinkTimeMs / 2, config.ThinkTimeMs * 3 / 2);
                        await Task.Delay(thinkTime, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when test duration expires
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "User {UserId} encountered an error", userId);
                _metrics.RecordError(ex.GetType().Name);
            }

            _logger.LogDebug("User {UserId} completed {RequestCount} requests", userId, requestCount);
        }

        /// <summary>
        /// Executes a single operation and records metrics.
        /// </summary>
        private async Task ExecuteOperationAsync(
            int userId,
            AudioOperationType operationType,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var success = false;
            string? errorType = null;

            try
            {
                switch (operationType)
                {
                    case AudioOperationType.Transcription:
                        await ExecuteTranscriptionAsync(userId, cancellationToken);
                        break;

                    case AudioOperationType.TextToSpeech:
                        await ExecuteTextToSpeechAsync(userId, cancellationToken);
                        break;

                    case AudioOperationType.RealtimeSession:
                        await ExecuteRealtimeSessionAsync(userId, cancellationToken);
                        break;

                    case AudioOperationType.HybridConversation:
                        await ExecuteHybridConversationAsync(userId, cancellationToken);
                        break;
                }

                success = true;
            }
            catch (Exception ex)
            {
                errorType = ex.GetType().Name;
                _logger.LogDebug(ex, "Operation {OperationType} failed for user {UserId}",
                    operationType, userId);
            }
            finally
            {
                stopwatch.Stop();
                _metrics.RecordOperation(operationType, stopwatch.Elapsed, success, errorType);
            }
        }

        /// <summary>
        /// Executes a transcription operation.
        /// </summary>
        protected abstract Task ExecuteTranscriptionAsync(int userId, CancellationToken cancellationToken);

        /// <summary>
        /// Executes a text-to-speech operation.
        /// </summary>
        protected abstract Task ExecuteTextToSpeechAsync(int userId, CancellationToken cancellationToken);

        /// <summary>
        /// Executes a real-time session operation.
        /// </summary>
        protected abstract Task ExecuteRealtimeSessionAsync(int userId, CancellationToken cancellationToken);

        /// <summary>
        /// Executes a hybrid conversation operation.
        /// </summary>
        protected abstract Task ExecuteHybridConversationAsync(int userId, CancellationToken cancellationToken);

        /// <summary>
        /// Selects an operation type based on weights.
        /// </summary>
        private AudioOperationType SelectOperation(Random random, Dictionary<AudioOperationType, int> weights)
        {
            var totalWeight = weights.Values.Sum();
            var randomValue = random.Next(totalWeight);
            var currentWeight = 0;

            foreach (var kvp in weights)
            {
                currentWeight += kvp.Value;
                if (randomValue < currentWeight)
                {
                    return kvp.Key;
                }
            }

            return AudioOperationType.Transcription; // Default
        }

        /// <summary>
        /// Calculates load test results from collected metrics.
        /// </summary>
        private LoadTestResult CalculateResults(TimeSpan duration)
        {
            var operations = _metrics.GetAllOperations();
            var result = new LoadTestResult
            {
                Duration = duration,
                TotalOperations = operations.Count,
                SuccessfulOperations = operations.Count(o => o.Success),
                FailedOperations = operations.Count(o => !o.Success)
            };

            // Calculate per-operation type metrics
            foreach (var operationType in Enum.GetValues<AudioOperationType>())
            {
                var typeOperations = operations.Where(o => o.OperationType == operationType).ToList();
                if (typeOperations.Any())
                {
                    var latencies = typeOperations
                        .Where(o => o.Success)
                        .Select(o => o.Latency.TotalMilliseconds)
                        .OrderBy(l => l)
                        .ToList();

                    result.OperationMetrics[operationType] = new OperationMetrics
                    {
                        TotalCount = typeOperations.Count,
                        SuccessCount = typeOperations.Count(o => o.Success),
                        FailureCount = typeOperations.Count(o => !o.Success),
                        AverageLatencyMs = latencies.Any() ? latencies.Average() : 0,
                        MinLatencyMs = latencies.Any() ? latencies.Min() : 0,
                        MaxLatencyMs = latencies.Any() ? latencies.Max() : 0,
                        P50LatencyMs = GetPercentile(latencies, 0.50),
                        P95LatencyMs = GetPercentile(latencies, 0.95),
                        P99LatencyMs = GetPercentile(latencies, 0.99),
                        ErrorBreakdown = typeOperations
                            .Where(o => !o.Success && !string.IsNullOrEmpty(o.ErrorType))
                            .GroupBy(o => o.ErrorType!)
                            .ToDictionary(g => g.Key, g => g.Count())
                    };
                }
            }

            // Calculate overall metrics
            result.Throughput = result.TotalOperations / duration.TotalSeconds;
            result.ErrorRate = result.TotalOperations > 0
                ? (double)result.FailedOperations / result.TotalOperations
                : 0;

            return result;
        }

        /// <summary>
        /// Gets the percentile value from a sorted list.
        /// </summary>
        private double GetPercentile(List<double> sortedValues, double percentile)
        {
            if (!sortedValues.Any()) return 0;

            var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
            return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
        }

        /// <summary>
        /// Prints a summary of the test results.
        /// </summary>
        private void PrintTestSummary(LoadTestConfig config, LoadTestResult result)
        {
            _output.WriteLine("\n=== Load Test Results ===");
            _output.WriteLine($"Test Name: {config.TestName}");
            _output.WriteLine($"Duration: {result.Duration}");
            _output.WriteLine($"Total Operations: {result.TotalOperations:N0}");
            _output.WriteLine($"Successful: {result.SuccessfulOperations:N0} ({result.SuccessfulOperations * 100.0 / Math.Max(1, result.TotalOperations):F1}%)");
            _output.WriteLine($"Failed: {result.FailedOperations:N0} ({result.ErrorRate * 100:F1}%)");
            _output.WriteLine($"Throughput: {result.Throughput:F1} ops/sec");

            foreach (var kvp in result.OperationMetrics)
            {
                var metrics = kvp.Value;
                _output.WriteLine($"\n{kvp.Key} Operations:");
                _output.WriteLine($"  Count: {metrics.TotalCount:N0} (Success: {metrics.SuccessCount:N0}, Failure: {metrics.FailureCount:N0})");
                _output.WriteLine($"  Latency - Avg: {metrics.AverageLatencyMs:F1}ms, P50: {metrics.P50LatencyMs:F1}ms, P95: {metrics.P95LatencyMs:F1}ms, P99: {metrics.P99LatencyMs:F1}ms");

                if (metrics.ErrorBreakdown.Any())
                {
                    _output.WriteLine("  Errors:");
                    foreach (var error in metrics.ErrorBreakdown.OrderByDescending(e => e.Value))
                    {
                        _output.WriteLine($"    {error.Key}: {error.Value}");
                    }
                }
            }
        }

        /// <summary>
        /// Disposes the service provider.
        /// </summary>
        public void Dispose()
        {
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    /// <summary>
    /// Load test configuration.
    /// </summary>
    public class LoadTestConfig
    {
        public string TestName { get; set; } = "Load Test";
        public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(1);
        public int ConcurrentUsers { get; set; } = 10;
        public int RequestsPerUser { get; set; } = -1; // -1 means unlimited
        public int ThinkTimeMs { get; set; } = 1000;
        public Dictionary<AudioOperationType, int> OperationWeights { get; set; } = new()
        {
            [AudioOperationType.Transcription] = 40,
            [AudioOperationType.TextToSpeech] = 40,
            [AudioOperationType.RealtimeSession] = 10,
            [AudioOperationType.HybridConversation] = 10
        };
    }

    /// <summary>
    /// Audio operation types for load testing.
    /// </summary>
    public enum AudioOperationType
    {
        Transcription,
        TextToSpeech,
        RealtimeSession,
        HybridConversation
    }

    /// <summary>
    /// Load test result.
    /// </summary>
    public class LoadTestResult
    {
        public TimeSpan Duration { get; set; }
        public int TotalOperations { get; set; }
        public int SuccessfulOperations { get; set; }
        public int FailedOperations { get; set; }
        public double Throughput { get; set; }
        public double ErrorRate { get; set; }
        public Dictionary<AudioOperationType, OperationMetrics> OperationMetrics { get; set; } = new();
    }

    /// <summary>
    /// Metrics for a specific operation type.
    /// </summary>
    public class OperationMetrics
    {
        public int TotalCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public double AverageLatencyMs { get; set; }
        public double MinLatencyMs { get; set; }
        public double MaxLatencyMs { get; set; }
        public double P50LatencyMs { get; set; }
        public double P95LatencyMs { get; set; }
        public double P99LatencyMs { get; set; }
        public Dictionary<string, int> ErrorBreakdown { get; set; } = new();
    }

    /// <summary>
    /// Thread-safe metrics collector.
    /// </summary>
    public class LoadTestMetrics
    {
        private readonly ConcurrentBag<OperationRecord> _operations = new();

        public void RecordOperation(
            AudioOperationType operationType,
            TimeSpan latency,
            bool success,
            string? errorType = null)
        {
            _operations.Add(new OperationRecord
            {
                OperationType = operationType,
                Timestamp = DateTime.UtcNow,
                Latency = latency,
                Success = success,
                ErrorType = errorType
            });
        }

        public void RecordError(string errorType)
        {
            _operations.Add(new OperationRecord
            {
                OperationType = AudioOperationType.Transcription,
                Timestamp = DateTime.UtcNow,
                Latency = TimeSpan.Zero,
                Success = false,
                ErrorType = errorType
            });
        }

        public List<OperationRecord> GetAllOperations()
        {
            return _operations.ToList();
        }

        public void Reset()
        {
            _operations.Clear();
        }
    }

    /// <summary>
    /// Record of a single operation.
    /// </summary>
    public class OperationRecord
    {
        public AudioOperationType OperationType { get; set; }
        public DateTime Timestamp { get; set; }
        public TimeSpan Latency { get; set; }
        public bool Success { get; set; }
        public string? ErrorType { get; set; }
    }
}

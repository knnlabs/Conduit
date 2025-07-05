using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Comprehensive metrics collector for image generation operations.
    /// </summary>
    public class ImageGenerationMetricsCollector : IImageGenerationMetricsCollector
    {
        private readonly ILogger<ImageGenerationMetricsCollector> _logger;
        private readonly IImageGenerationMetricsService _metricsService;
        private readonly ImageGenerationMetricsOptions _options;
        
        // In-memory metrics storage
        private readonly ConcurrentDictionary<string, GenerationOperation> _activeOperations = new();
        private readonly ConcurrentQueue<CompletedOperation> _completedOperations = new();
        private readonly ConcurrentDictionary<string, ProviderMetrics> _providerMetrics = new();
        private readonly ConcurrentDictionary<string, QueueMetricsData> _queueMetrics = new();
        private readonly ConcurrentQueue<ResourceUtilizationData> _resourceHistory = new();
        private readonly ConcurrentDictionary<int, VirtualKeyMetrics> _virtualKeyMetrics = new();
        
        // Metrics counters
        private long _totalGenerations;
        private long _successfulGenerations;
        private long _failedGenerations;
        private long _totalCostCents; // Store cost as cents to use with Interlocked
        private long _totalImages;
        
        private readonly Timer _cleanupTimer;
        private readonly SemaphoreSlim _metricsSemaphore = new(1);

        public ImageGenerationMetricsCollector(
            ILogger<ImageGenerationMetricsCollector> logger,
            IImageGenerationMetricsService metricsService,
            IOptions<ImageGenerationMetricsOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
            _options = options?.Value ?? new ImageGenerationMetricsOptions();
            
            // Start cleanup timer
            _cleanupTimer = new Timer(CleanupOldMetrics, null, 
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        public void RecordGenerationStart(string operationId, string provider, string model, int imageCount, int virtualKeyId)
        {
            var operation = new GenerationOperation
            {
                OperationId = operationId,
                Provider = provider,
                Model = model,
                ImageCount = imageCount,
                VirtualKeyId = virtualKeyId,
                StartTime = DateTime.UtcNow,
                Stopwatch = Stopwatch.StartNew()
            };
            
            _activeOperations[operationId] = operation;
            
            // Update provider metrics
            var providerKey = $"{provider}:{model}";
            _providerMetrics.AddOrUpdate(providerKey,
                new ProviderMetrics { ActiveRequests = 1 },
                (_, m) => { m.ActiveRequests++; return m; });
            
            Interlocked.Increment(ref _totalGenerations);
            
            _logger.LogDebug("Started tracking image generation {OperationId} for {Provider}/{Model}", 
                operationId, provider, model);
        }

        public void RecordGenerationComplete(string operationId, bool success, int imagesGenerated, decimal cost, string? error = null)
        {
            if (!_activeOperations.TryRemove(operationId, out var operation))
            {
                _logger.LogWarning("Attempted to complete unknown operation {OperationId}", operationId);
                return;
            }
            
            operation.Stopwatch.Stop();
            operation.EndTime = DateTime.UtcNow;
            operation.Success = success;
            operation.ImagesGenerated = imagesGenerated;
            operation.Cost = cost;
            operation.Error = error;
            
            // Record to metrics service
            var metric = new ImageGenerationMetrics
            {
                Provider = operation.Provider,
                Model = operation.Model,
                Success = success,
                TotalGenerationTimeMs = operation.Stopwatch.ElapsedMilliseconds,
                ImageCount = imagesGenerated,
                EstimatedCost = cost,
                StartedAt = operation.StartTime,
                CompletedAt = DateTime.UtcNow,
                VirtualKeyId = operation.VirtualKeyId,
                ErrorCode = error
            };
            
            _ = _metricsService.RecordMetricAsync(metric);
            
            // Update counters
            if (success)
            {
                Interlocked.Increment(ref _successfulGenerations);
                Interlocked.Add(ref _totalImages, imagesGenerated);
            }
            else
            {
                Interlocked.Increment(ref _failedGenerations);
            }
            
            // Update provider metrics
            var providerKey = $"{operation.Provider}:{operation.Model}";
            _providerMetrics.AddOrUpdate(providerKey,
                new ProviderMetrics { ActiveRequests = 0 },
                (_, m) => 
                {
                    m.ActiveRequests = Math.Max(0, m.ActiveRequests - 1);
                    m.TotalRequests++;
                    if (success) m.SuccessfulRequests++;
                    else m.FailedRequests++;
                    m.TotalResponseTimeMs += operation.Stopwatch.ElapsedMilliseconds;
                    m.ResponseTimes.Add(operation.Stopwatch.ElapsedMilliseconds);
                    if (!success) m.ConsecutiveFailures++;
                    else m.ConsecutiveFailures = 0;
                    m.LastUpdateTime = DateTime.UtcNow;
                    return m;
                });
            
            // Store completed operation
            var completed = new CompletedOperation
            {
                OperationId = operationId,
                Provider = operation.Provider,
                Model = operation.Model,
                Success = success,
                ResponseTimeMs = operation.Stopwatch.ElapsedMilliseconds,
                ImagesGenerated = imagesGenerated,
                Cost = cost,
                Timestamp = operation.EndTime,
                VirtualKeyId = operation.VirtualKeyId,
                Error = error
            };
            
            _completedOperations.Enqueue(completed);
            
            // Update virtual key metrics
            RecordVirtualKeyUsage(operation.VirtualKeyId, imagesGenerated, cost, 0);
            
            // Add to total cost (convert to cents for thread-safe operation)
            var costInCents = (long)(cost * 100);
            Interlocked.Add(ref _totalCostCents, costInCents);
            
            _logger.LogDebug("Completed tracking image generation {OperationId} - Success: {Success}, Time: {Time}ms", 
                operationId, success, operation.Stopwatch.ElapsedMilliseconds);
        }

        public void RecordProviderPerformance(string provider, string model, double responseTimeMs, double queueTimeMs)
        {
            var providerKey = $"{provider}:{model}";
            _providerMetrics.AddOrUpdate(providerKey,
                new ProviderMetrics 
                { 
                    TotalResponseTimeMs = responseTimeMs,
                    TotalQueueTimeMs = queueTimeMs,
                    ResponseTimes = new List<double> { responseTimeMs }
                },
                (_, m) => 
                {
                    m.TotalResponseTimeMs += responseTimeMs;
                    m.TotalQueueTimeMs += queueTimeMs;
                    m.ResponseTimes.Add(responseTimeMs);
                    return m;
                });
        }

        public void RecordImageDownload(string provider, double downloadTimeMs, long imageSizeBytes, bool success)
        {
            var providerKey = $"{provider}:download";
            _providerMetrics.AddOrUpdate(providerKey,
                new ProviderMetrics 
                { 
                    TotalDownloadTimeMs = downloadTimeMs,
                    TotalDownloadBytes = imageSizeBytes,
                    DownloadCount = 1,
                    FailedDownloads = success ? 0 : 1
                },
                (_, m) => 
                {
                    m.TotalDownloadTimeMs += downloadTimeMs;
                    m.TotalDownloadBytes += imageSizeBytes;
                    m.DownloadCount++;
                    if (!success) m.FailedDownloads++;
                    return m;
                });
        }

        public void RecordStorageOperation(string storageType, string operationType, double durationMs, long sizeBytes, bool success)
        {
            // This could be expanded to track storage-specific metrics
            _logger.LogDebug("Storage operation: {Type}/{Operation} - {Duration}ms, {Size} bytes, Success: {Success}", 
                storageType, operationType, durationMs, sizeBytes, success);
        }

        public void RecordQueueMetrics(string queueName, int depth, double oldestItemAgeMs)
        {
            _queueMetrics[queueName] = new QueueMetricsData
            {
                QueueName = queueName,
                Depth = depth,
                OldestItemAgeMs = oldestItemAgeMs,
                Timestamp = DateTime.UtcNow
            };
        }

        public void RecordResourceUtilization(double cpuPercent, double memoryMb, int activeGenerations, int threadPoolThreads)
        {
            var data = new ResourceUtilizationData
            {
                CpuPercent = cpuPercent,
                MemoryMb = memoryMb,
                ActiveGenerations = activeGenerations,
                ThreadPoolThreads = threadPoolThreads,
                Timestamp = DateTime.UtcNow
            };
            
            _resourceHistory.Enqueue(data);
            
            // Keep only recent history
            while (_resourceHistory.Count > _options.MaxResourceHistorySize)
            {
                _resourceHistory.TryDequeue(out _);
            }
        }

        public void RecordVirtualKeyUsage(int virtualKeyId, int imagesGenerated, decimal cost, decimal remainingBudget)
        {
            _virtualKeyMetrics.AddOrUpdate(virtualKeyId,
                new VirtualKeyMetrics 
                { 
                    VirtualKeyId = virtualKeyId,
                    TotalImages = imagesGenerated,
                    TotalCost = cost,
                    RemainingBudget = remainingBudget,
                    LastUsed = DateTime.UtcNow
                },
                (_, m) => 
                {
                    m.TotalImages += imagesGenerated;
                    m.TotalCost += cost;
                    m.RemainingBudget = remainingBudget;
                    m.LastUsed = DateTime.UtcNow;
                    return m;
                });
        }

        public void RecordProviderHealth(string provider, double healthScore, bool isHealthy, string? lastError = null)
        {
            var providerKey = provider;
            _providerMetrics.AddOrUpdate(providerKey,
                new ProviderMetrics 
                { 
                    HealthScore = healthScore,
                    IsHealthy = isHealthy,
                    LastError = lastError,
                    LastHealthCheck = DateTime.UtcNow
                },
                (_, m) => 
                {
                    m.HealthScore = healthScore;
                    m.IsHealthy = isHealthy;
                    if (!string.IsNullOrEmpty(lastError))
                        m.LastError = lastError;
                    m.LastHealthCheck = DateTime.UtcNow;
                    return m;
                });
        }

        public void RecordModelMetrics(string model, string imageSize, string quality, double generationTimeMs)
        {
            // This could be expanded to track model-specific performance patterns
            _logger.LogDebug("Model metrics: {Model} - Size: {Size}, Quality: {Quality}, Time: {Time}ms", 
                model, imageSize, quality, generationTimeMs);
        }

        public async Task<ImageGenerationMetricsSnapshot> GetMetricsSnapshotAsync(CancellationToken cancellationToken = default)
        {
            await _metricsSemaphore.WaitAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;
                var oneHourAgo = now.AddHours(-1);
                
                // Calculate metrics from completed operations
                var recentOperations = _completedOperations
                    .Where(o => o.Timestamp >= oneHourAgo)
                    .ToList();
                
                var snapshot = new ImageGenerationMetricsSnapshot
                {
                    Timestamp = now,
                    ActiveGenerations = _activeOperations.Count,
                    GenerationsPerMinute = recentOperations.Count / 60.0,
                    SuccessRate = recentOperations.Any() ? 
                        recentOperations.Count(o => o.Success) / (double)recentOperations.Count : 0,
                    TotalCostLastHour = recentOperations.Sum(o => o.Cost),
                    TotalImagesLastHour = recentOperations.Sum(o => o.ImagesGenerated),
                    ProviderStatuses = new Dictionary<string, ProviderStatus>(),
                    QueueMetrics = new QueueMetrics(),
                    ResourceMetrics = new ResourceMetrics(),
                    ErrorCounts = new Dictionary<string, int>()
                };
                
                // Calculate response time percentiles
                if (recentOperations.Any())
                {
                    var responseTimes = recentOperations.Select(o => o.ResponseTimeMs).OrderBy(t => t).ToList();
                    snapshot.AverageResponseTimeMs = responseTimes.Average();
                    snapshot.P95ResponseTimeMs = GetPercentile(responseTimes, 0.95);
                }
                
                // Build provider statuses
                foreach (var kvp in _providerMetrics)
                {
                    var parts = kvp.Key.Split(':');
                    var provider = parts[0];
                    var metrics = kvp.Value;
                    
                    if (!snapshot.ProviderStatuses.ContainsKey(provider))
                    {
                        snapshot.ProviderStatuses[provider] = new ProviderStatus
                        {
                            IsHealthy = metrics.IsHealthy,
                            HealthScore = metrics.HealthScore,
                            ActiveRequests = metrics.ActiveRequests,
                            ConsecutiveFailures = metrics.ConsecutiveFailures,
                            LastError = metrics.LastError
                        };
                    }
                    
                    if (metrics.ResponseTimes.Any())
                    {
                        snapshot.ProviderStatuses[provider].AverageResponseTimeMs = 
                            metrics.ResponseTimes.Average();
                    }
                    
                    if (metrics.SuccessfulRequests > 0)
                    {
                        snapshot.ProviderStatuses[provider].LastSuccessAt = metrics.LastUpdateTime;
                    }
                    
                    if (metrics.FailedRequests > 0 && !string.IsNullOrEmpty(metrics.LastError))
                    {
                        snapshot.ProviderStatuses[provider].LastFailureAt = metrics.LastUpdateTime;
                    }
                }
                
                // Build queue metrics
                if (_queueMetrics.Any())
                {
                    snapshot.QueueMetrics.TotalDepth = _queueMetrics.Values.Sum(q => q.Depth);
                    snapshot.QueueMetrics.MaxWaitTimeMs = _queueMetrics.Values
                        .Where(q => q.OldestItemAgeMs > 0)
                        .Select(q => q.OldestItemAgeMs)
                        .DefaultIfEmpty(0)
                        .Max();
                }
                
                // Build resource metrics
                if (_resourceHistory.Any())
                {
                    var recentResources = _resourceHistory.Where(r => r.Timestamp >= oneHourAgo).ToList();
                    if (recentResources.Any())
                    {
                        snapshot.ResourceMetrics.CpuUsagePercent = recentResources.Average(r => r.CpuPercent);
                        snapshot.ResourceMetrics.MemoryUsageMb = recentResources.Average(r => r.MemoryMb);
                        snapshot.ResourceMetrics.ThreadPoolThreads = (int)recentResources.Average(r => r.ThreadPoolThreads);
                    }
                }
                
                // Build error counts
                var errors = recentOperations.Where(o => !o.Success && !string.IsNullOrEmpty(o.Error));
                foreach (var error in errors)
                {
                    var errorType = ClassifyError(error.Error!);
                    snapshot.ErrorCounts[errorType] = snapshot.ErrorCounts.GetValueOrDefault(errorType, 0) + 1;
                }
                
                return snapshot;
            }
            finally
            {
                _metricsSemaphore.Release();
            }
        }

        public async Task<ProviderMetricsSummary> GetProviderMetricsAsync(string provider, int timeWindowMinutes = 60, CancellationToken cancellationToken = default)
        {
            await _metricsSemaphore.WaitAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;
                var startTime = now.AddMinutes(-timeWindowMinutes);
                
                var providerOperations = _completedOperations
                    .Where(o => o.Provider == provider && o.Timestamp >= startTime)
                    .ToList();
                
                var summary = new ProviderMetricsSummary
                {
                    ProviderName = provider,
                    PeriodStart = startTime,
                    PeriodEnd = now,
                    TotalRequests = providerOperations.Count,
                    SuccessfulRequests = providerOperations.Count(o => o.Success),
                    FailedRequests = providerOperations.Count(o => !o.Success),
                    TotalCost = providerOperations.Sum(o => o.Cost),
                    TotalImages = providerOperations.Sum(o => o.ImagesGenerated)
                };
                
                if (summary.TotalRequests > 0)
                {
                    summary.SuccessRate = summary.SuccessfulRequests / (double)summary.TotalRequests;
                    
                    var responseTimes = providerOperations.Select(o => o.ResponseTimeMs).OrderBy(t => t).ToList();
                    summary.AverageResponseTimeMs = responseTimes.Average();
                    summary.P50ResponseTimeMs = GetPercentile(responseTimes, 0.50);
                    summary.P95ResponseTimeMs = GetPercentile(responseTimes, 0.95);
                    summary.P99ResponseTimeMs = GetPercentile(responseTimes, 0.99);
                }
                
                if (summary.TotalImages > 0)
                {
                    summary.AverageCostPerImage = summary.TotalCost / summary.TotalImages;
                }
                
                // Build error breakdown
                var errors = providerOperations.Where(o => !o.Success && !string.IsNullOrEmpty(o.Error));
                foreach (var error in errors)
                {
                    var errorType = ClassifyError(error.Error!);
                    summary.ErrorBreakdown[errorType] = summary.ErrorBreakdown.GetValueOrDefault(errorType, 0) + 1;
                }
                
                // Build model breakdown
                var modelGroups = providerOperations.GroupBy(o => o.Model);
                foreach (var modelGroup in modelGroups)
                {
                    var modelMetrics = new ModelMetrics
                    {
                        ModelName = modelGroup.Key,
                        RequestCount = modelGroup.Count(),
                        TotalCost = modelGroup.Sum(o => o.Cost),
                        TotalImages = modelGroup.Sum(o => o.ImagesGenerated)
                    };
                    
                    if (modelGroup.Any())
                    {
                        modelMetrics.AverageResponseTimeMs = modelGroup.Average(o => o.ResponseTimeMs);
                    }
                    
                    summary.ModelBreakdown[modelGroup.Key] = modelMetrics;
                }
                
                return summary;
            }
            finally
            {
                _metricsSemaphore.Release();
            }
        }

        public async Task<SlaComplianceSummary> GetSlaComplianceAsync(int timeWindowHours = 24, CancellationToken cancellationToken = default)
        {
            await _metricsSemaphore.WaitAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;
                var startTime = now.AddHours(-timeWindowHours);
                
                var periodOperations = _completedOperations
                    .Where(o => o.Timestamp >= startTime)
                    .ToList();
                
                var summary = new SlaComplianceSummary
                {
                    PeriodStart = startTime,
                    PeriodEnd = now,
                    TotalRequests = periodOperations.Count,
                    SuccessfulRequests = periodOperations.Count(o => o.Success),
                    FailedRequests = periodOperations.Count(o => !o.Success),
                    Violations = new List<SlaViolation>()
                };
                
                if (summary.TotalRequests > 0)
                {
                    // Calculate availability
                    summary.AvailabilityPercent = (summary.SuccessfulRequests / (double)summary.TotalRequests) * 100;
                    summary.MeetsAvailabilitySla = summary.AvailabilityPercent >= _options.SlaTargets.MinAvailabilityPercent;
                    
                    // Calculate P95 response time
                    var responseTimes = periodOperations.Select(o => o.ResponseTimeMs).OrderBy(t => t).ToList();
                    summary.P95ResponseTimeMs = GetPercentile(responseTimes, 0.95);
                    summary.MeetsResponseTimeSla = summary.P95ResponseTimeMs <= _options.SlaTargets.MaxP95ResponseTimeMs;
                    
                    // Calculate error rate
                    summary.ErrorRatePercent = (summary.FailedRequests / (double)summary.TotalRequests) * 100;
                    summary.MeetsErrorRateSla = summary.ErrorRatePercent <= _options.SlaTargets.MaxErrorRatePercent;
                    
                    // Check for violations
                    if (!summary.MeetsAvailabilitySla)
                    {
                        summary.Violations.Add(new SlaViolation
                        {
                            Timestamp = now,
                            ViolationType = "Availability",
                            Description = "Availability below SLA threshold",
                            ActualValue = summary.AvailabilityPercent,
                            ThresholdValue = _options.SlaTargets.MinAvailabilityPercent
                        });
                    }
                    
                    if (!summary.MeetsResponseTimeSla)
                    {
                        summary.Violations.Add(new SlaViolation
                        {
                            Timestamp = now,
                            ViolationType = "ResponseTime",
                            Description = "P95 response time above SLA threshold",
                            ActualValue = summary.P95ResponseTimeMs,
                            ThresholdValue = _options.SlaTargets.MaxP95ResponseTimeMs
                        });
                    }
                    
                    if (!summary.MeetsErrorRateSla)
                    {
                        summary.Violations.Add(new SlaViolation
                        {
                            Timestamp = now,
                            ViolationType = "ErrorRate",
                            Description = "Error rate above SLA threshold",
                            ActualValue = summary.ErrorRatePercent,
                            ThresholdValue = _options.SlaTargets.MaxErrorRatePercent
                        });
                    }
                }
                
                return summary;
            }
            finally
            {
                _metricsSemaphore.Release();
            }
        }

        private double GetPercentile(List<double> sortedValues, double percentile)
        {
            if (!sortedValues.Any()) return 0;
            
            var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
            index = Math.Max(0, Math.Min(index, sortedValues.Count - 1));
            return sortedValues[index];
        }

        private string ClassifyError(string error)
        {
            if (error.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                return "Timeout";
            if (error.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
                return "RateLimit";
            if (error.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("authentication", StringComparison.OrdinalIgnoreCase))
                return "Authentication";
            if (error.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("connection", StringComparison.OrdinalIgnoreCase))
                return "Network";
            if (error.Contains("invalid", StringComparison.OrdinalIgnoreCase))
                return "Validation";
            if (error.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("budget", StringComparison.OrdinalIgnoreCase))
                return "Quota";
            
            return "Other";
        }

        private void CleanupOldMetrics(object? state)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddHours(-_options.MetricsRetentionHours);
                
                // Clean up completed operations
                var operationsToKeep = new List<CompletedOperation>();
                while (_completedOperations.TryDequeue(out var operation))
                {
                    if (operation.Timestamp >= cutoffTime)
                    {
                        operationsToKeep.Add(operation);
                    }
                }
                
                foreach (var operation in operationsToKeep)
                {
                    _completedOperations.Enqueue(operation);
                }
                
                // Clean up provider metrics response times
                foreach (var metrics in _providerMetrics.Values)
                {
                    if (metrics.ResponseTimes.Count > _options.MaxResponseTimeHistorySize)
                    {
                        metrics.ResponseTimes = metrics.ResponseTimes
                            .Skip(metrics.ResponseTimes.Count - _options.MaxResponseTimeHistorySize)
                            .ToList();
                    }
                }
                
                _logger.LogDebug("Cleaned up old metrics. Retained {Count} operations", operationsToKeep.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old metrics");
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _metricsSemaphore?.Dispose();
        }

        private class GenerationOperation
        {
            public string OperationId { get; set; } = string.Empty;
            public string Provider { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
            public int ImageCount { get; set; }
            public int VirtualKeyId { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public Stopwatch Stopwatch { get; set; } = new();
            public bool Success { get; set; }
            public int ImagesGenerated { get; set; }
            public decimal Cost { get; set; }
            public string? Error { get; set; }
        }

        private class CompletedOperation
        {
            public string OperationId { get; set; } = string.Empty;
            public string Provider { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
            public bool Success { get; set; }
            public double ResponseTimeMs { get; set; }
            public int ImagesGenerated { get; set; }
            public decimal Cost { get; set; }
            public DateTime Timestamp { get; set; }
            public int VirtualKeyId { get; set; }
            public string? Error { get; set; }
        }

        private class ProviderMetrics
        {
            public int ActiveRequests { get; set; }
            public int TotalRequests { get; set; }
            public int SuccessfulRequests { get; set; }
            public int FailedRequests { get; set; }
            public double TotalResponseTimeMs { get; set; }
            public double TotalQueueTimeMs { get; set; }
            public List<double> ResponseTimes { get; set; } = new();
            public int ConsecutiveFailures { get; set; }
            public DateTime LastUpdateTime { get; set; }
            public double HealthScore { get; set; } = 1.0;
            public bool IsHealthy { get; set; } = true;
            public string? LastError { get; set; }
            public DateTime? LastHealthCheck { get; set; }
            public long TotalDownloadBytes { get; set; }
            public double TotalDownloadTimeMs { get; set; }
            public int DownloadCount { get; set; }
            public int FailedDownloads { get; set; }
        }

        private class QueueMetricsData
        {
            public string QueueName { get; set; } = string.Empty;
            public int Depth { get; set; }
            public double OldestItemAgeMs { get; set; }
            public DateTime Timestamp { get; set; }
        }

        private class ResourceUtilizationData
        {
            public double CpuPercent { get; set; }
            public double MemoryMb { get; set; }
            public int ActiveGenerations { get; set; }
            public int ThreadPoolThreads { get; set; }
            public DateTime Timestamp { get; set; }
        }

        private class VirtualKeyMetrics
        {
            public int VirtualKeyId { get; set; }
            public int TotalImages { get; set; }
            public decimal TotalCost { get; set; }
            public decimal RemainingBudget { get; set; }
            public DateTime LastUsed { get; set; }
        }
    }

    /// <summary>
    /// Configuration options for image generation metrics.
    /// </summary>
    public class ImageGenerationMetricsOptions
    {
        public int MetricsRetentionHours { get; set; } = 24;
        public int MaxResponseTimeHistorySize { get; set; } = 1000;
        public int MaxResourceHistorySize { get; set; } = 100;
        
        public SlaTargetOptions SlaTargets { get; set; } = new();
    }

    /// <summary>
    /// SLA target configuration.
    /// </summary>
    public class SlaTargetOptions
    {
        public double MinAvailabilityPercent { get; set; } = 99.9;
        public double MaxP95ResponseTimeMs { get; set; } = 45000; // 45 seconds
        public double MaxErrorRatePercent { get; set; } = 1.0;
    }
}
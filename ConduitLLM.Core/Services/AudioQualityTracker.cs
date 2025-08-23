using System.Collections.Concurrent;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Tracks and analyzes audio quality metrics including confidence scores and accuracy.
    /// </summary>
    public class AudioQualityTracker : IAudioQualityTracker
    {
        private readonly ILogger<AudioQualityTracker> _logger;
        private readonly IAudioMetricsCollector _metricsCollector;
        private readonly ConcurrentDictionary<string, QualityMetrics> _providerQualityMetrics = new();
        private readonly ConcurrentDictionary<string, QualityMetrics> _modelQualityMetrics = new();
        private readonly ConcurrentDictionary<string, LanguageQualityMetrics> _languageQualityMetrics = new();
        private readonly Timer _analysisTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioQualityTracker"/> class.
        /// </summary>
        public AudioQualityTracker(
            ILogger<AudioQualityTracker> logger,
            IAudioMetricsCollector metricsCollector)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));

            // Start periodic analysis
            _analysisTimer = new Timer(
                AnalyzeQualityTrends,
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5));
        }

        /// <inheritdoc />
        public Task TrackTranscriptionQualityAsync(AudioQualityMetric metric)
        {
            try
            {
                // Update provider quality metrics
                var providerMetrics = _providerQualityMetrics.GetOrAdd(
                    metric.Provider,
                    _ => new QualityMetrics());
                providerMetrics.UpdateMetrics(metric.Confidence, metric.AccuracyScore);

                // Update model quality metrics
                if (!string.IsNullOrEmpty(metric.Model))
                {
                    var modelMetrics = _modelQualityMetrics.GetOrAdd(
                        metric.Model,
                        _ => new QualityMetrics());
                    modelMetrics.UpdateMetrics(metric.Confidence, metric.AccuracyScore);
                }

                // Update language quality metrics
                if (!string.IsNullOrEmpty(metric.Language))
                {
                    var languageMetrics = _languageQualityMetrics.GetOrAdd(
                        metric.Language,
                        _ => new LanguageQualityMetrics());
                    languageMetrics.UpdateMetrics(metric.Confidence, metric.WordErrorRate);
                }

                // Check for quality issues
                if (metric.Confidence < 0.7)
                {
                    _logger.LogWarning(
                        "Low confidence transcription: Provider={Provider}, Confidence={Confidence}, Language={Language}",
                        metric.Provider, metric.Confidence, metric.Language);
                }

                if (metric.WordErrorRate > 0.15) // 15% WER threshold
                {
                    _logger.LogWarning(
                        "High word error rate: Provider={Provider}, WER={WER}%, Language={Language}",
                        metric.Provider, metric.WordErrorRate * 100, metric.Language);
                }

                // Record to main metrics collector as well
                var transcriptionMetric = new TranscriptionMetric
                {
                    Provider = metric.Provider,
                    VirtualKey = metric.VirtualKey,
                    Success = true,
                    DurationMs = metric.ProcessingDurationMs,
                    Confidence = metric.Confidence,
                    DetectedLanguage = metric.Language,
                    AudioDurationSeconds = metric.AudioDurationSeconds,
                    FileSizeBytes = 0, // Not tracked in quality metric
                    WordCount = 0, // Not tracked in quality metric
                    Tags = new Dictionary<string, string>
                    {
                        ["quality.tracked"] = "true",
                        ["quality.wer"] = metric.WordErrorRate?.ToString("F3") ?? "unknown",
                        ["quality.accuracy"] = metric.AccuracyScore?.ToString("F3") ?? "unknown"
                    }
                };

                return _metricsCollector.RecordTranscriptionMetricAsync(transcriptionMetric);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking transcription quality");
                return Task.CompletedTask;
            }
        }

        /// <inheritdoc />
        public Task<AudioQualityReport> GetQualityReportAsync(
            DateTime startTime,
            DateTime endTime,
            string? provider = null)
        {
            var report = new AudioQualityReport
            {
                Period = new DateTimeRange { Start = startTime, End = endTime },
                ProviderQuality = GetProviderQualityStats(provider),
                ModelQuality = GetModelQualityStats(),
                LanguageQuality = GetLanguageQualityStats(),
                QualityTrends = CalculateQualityTrends(),
                Recommendations = GenerateRecommendations()
            };

            return Task.FromResult(report);
        }

        /// <inheritdoc />
        public Task<QualityThresholds> GetQualityThresholdsAsync(string provider)
        {
            // These thresholds could be configured per provider
            return Task.FromResult(new QualityThresholds
            {
                MinimumConfidence = 0.8,
                MaximumWordErrorRate = 0.1, // 10%
                MinimumAccuracy = 0.9,
                OptimalConfidence = 0.95,
                OptimalWordErrorRate = 0.05, // 5%
                OptimalAccuracy = 0.97
            });
        }

        /// <inheritdoc />
        public Task<bool> IsQualityAcceptableAsync(
            string provider,
            double confidence,
            double? wordErrorRate = null)
        {
            var thresholds = GetQualityThresholdsAsync(provider).Result;
            
            var confidenceOk = confidence >= thresholds.MinimumConfidence;
            var werOk = !wordErrorRate.HasValue || wordErrorRate.Value <= thresholds.MaximumWordErrorRate;

            return Task.FromResult(confidenceOk && werOk);
        }

        private Dictionary<string, ProviderQualityStats> GetProviderQualityStats(string? provider)
        {
            var stats = new Dictionary<string, ProviderQualityStats>();

            var providers = provider != null 
                ? new[] { provider } 
                : _providerQualityMetrics.Keys.ToArray();

            foreach (var p in providers)
            {
                if (_providerQualityMetrics.TryGetValue(p, out var metrics))
                {
                    stats[p] = new ProviderQualityStats
                    {
                        Provider = p,
                        AverageConfidence = metrics.GetAverageConfidence(),
                        MinimumConfidence = metrics.MinConfidence,
                        MaximumConfidence = metrics.MaxConfidence,
                        ConfidenceStdDev = metrics.GetConfidenceStdDev(),
                        AverageAccuracy = metrics.GetAverageAccuracy(),
                        SampleCount = metrics.SampleCount,
                        LowConfidenceRate = metrics.GetLowConfidenceRate(0.7),
                        HighConfidenceRate = metrics.GetHighConfidenceRate(0.95)
                    };
                }
            }

            return stats;
        }

        private Dictionary<string, ModelQualityStats> GetModelQualityStats()
        {
            var stats = new Dictionary<string, ModelQualityStats>();

            foreach (var kvp in _modelQualityMetrics)
            {
                var metrics = kvp.Value;
                stats[kvp.Key] = new ModelQualityStats
                {
                    Model = kvp.Key,
                    AverageConfidence = metrics.GetAverageConfidence(),
                    AverageAccuracy = metrics.GetAverageAccuracy(),
                    SampleCount = metrics.SampleCount,
                    PerformanceRating = CalculatePerformanceRating(metrics)
                };
            }

            return stats;
        }

        private Dictionary<string, LanguageQualityStats> GetLanguageQualityStats()
        {
            var stats = new Dictionary<string, LanguageQualityStats>();

            foreach (var kvp in _languageQualityMetrics)
            {
                var metrics = kvp.Value;
                stats[kvp.Key] = new LanguageQualityStats
                {
                    Language = kvp.Key,
                    AverageConfidence = metrics.GetAverageConfidence(),
                    AverageWordErrorRate = metrics.GetAverageWordErrorRate(),
                    SampleCount = metrics.SampleCount,
                    QualityScore = CalculateLanguageQualityScore(metrics)
                };
            }

            return stats;
        }

        private List<QualityTrend> CalculateQualityTrends()
        {
            var trends = new List<QualityTrend>();

            foreach (var kvp in _providerQualityMetrics)
            {
                var trend = kvp.Value.CalculateTrend();
                if (trend != AudioQualityTrendDirection.Stable)
                {
                    trends.Add(new QualityTrend
                    {
                        Provider = kvp.Key,
                        Metric = "Confidence",
                        Direction = trend,
                        ChangePercent = kvp.Value.GetTrendChangePercent()
                    });
                }
            }

            return trends;
        }

        private List<QualityRecommendation> GenerateRecommendations()
        {
            var recommendations = new List<QualityRecommendation>();

            // Check for providers with consistently low confidence
            foreach (var kvp in _providerQualityMetrics)
            {
                var avgConfidence = kvp.Value.GetAverageConfidence();
                if (avgConfidence < 0.8)
                {
                    recommendations.Add(new QualityRecommendation
                    {
                        Type = RecommendationType.ProviderSwitch,
                        Severity = RecommendationSeverity.High,
                        Provider = kvp.Key,
                        Message = $"Provider {kvp.Key} has low average confidence ({avgConfidence:P1}). Consider switching to a higher quality provider.",
                        Impact = "Improved transcription accuracy"
                    });
                }
            }

            // Check for languages with high error rates
            foreach (var kvp in _languageQualityMetrics)
            {
                var avgWer = kvp.Value.GetAverageWordErrorRate();
                if (avgWer > 0.15)
                {
                    recommendations.Add(new QualityRecommendation
                    {
                        Type = RecommendationType.ModelUpgrade,
                        Severity = RecommendationSeverity.Medium,
                        Language = kvp.Key,
                        Message = $"Language {kvp.Key} has high error rate ({avgWer:P1}). Consider using a specialized model for this language.",
                        Impact = "Reduced word error rate"
                    });
                }
            }

            return recommendations;
        }

        private double CalculatePerformanceRating(QualityMetrics metrics)
        {
            var confidenceScore = metrics.GetAverageConfidence();
            var accuracyScore = metrics.GetAverageAccuracy();
            var consistencyScore = 1.0 - (metrics.GetConfidenceStdDev() / 0.5); // Normalize std dev

            return (confidenceScore * 0.4 + accuracyScore * 0.4 + consistencyScore * 0.2);
        }

        private double CalculateLanguageQualityScore(LanguageQualityMetrics metrics)
        {
            var confidenceScore = metrics.GetAverageConfidence();
            var werScore = 1.0 - metrics.GetAverageWordErrorRate(); // Invert WER

            return (confidenceScore * 0.6 + werScore * 0.4);
        }

        private void AnalyzeQualityTrends(object? state)
        {
            try
            {
                // Clean up old metrics
                var cutoff = DateTime.UtcNow.AddHours(-24);
                foreach (var metrics in _providerQualityMetrics.Values)
                {
                    metrics.CleanupOldSamples(cutoff);
                }

                // Log significant quality changes
                foreach (var kvp in _providerQualityMetrics)
                {
                    var trend = kvp.Value.CalculateTrend();
                    if (trend == AudioQualityTrendDirection.Declining)
                    {
                        _logger.LogWarning(
                            "Quality declining for provider {Provider}: Confidence dropped {Change:P1}",
                            kvp.Key, Math.Abs(kvp.Value.GetTrendChangePercent()));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing quality trends");
            }
        }

        /// <summary>
        /// Disposes the quality tracker.
        /// </summary>
        public void Dispose()
        {
            _analysisTimer?.Dispose();
        }
    }

    /// <summary>
    /// Internal class for tracking quality metrics.
    /// </summary>
    internal class QualityMetrics
    {
        private readonly ConcurrentBag<TimestampedSample> _confidenceSamples = new();
        private readonly ConcurrentBag<TimestampedSample> _accuracySamples = new();
        private readonly object _lock = new();
        
        public double MinConfidence { get; private set; } = 1.0;
        public double MaxConfidence { get; private set; } = 0.0;
        public long SampleCount => _confidenceSamples.Count();

        public void UpdateMetrics(double? confidence, double? accuracy)
        {
            var timestamp = DateTime.UtcNow;

            if (confidence.HasValue)
            {
                _confidenceSamples.Add(new TimestampedSample { Value = confidence.Value, Timestamp = timestamp });
                
                lock (_lock)
                {
                    MinConfidence = Math.Min(MinConfidence, confidence.Value);
                    MaxConfidence = Math.Max(MaxConfidence, confidence.Value);
                }
            }

            if (accuracy.HasValue)
            {
                _accuracySamples.Add(new TimestampedSample { Value = accuracy.Value, Timestamp = timestamp });
            }
        }

        public double GetAverageConfidence()
        {
            var samples = _confidenceSamples.ToList();
            return samples.Count() > 0 ? samples.Average(s => s.Value) : 0;
        }

        public double GetAverageAccuracy()
        {
            var samples = _accuracySamples.ToList();
            return samples.Count() > 0 ? samples.Average(s => s.Value) : 0;
        }

        public double GetConfidenceStdDev()
        {
            var samples = _confidenceSamples.Select(s => s.Value).ToList();
            if (samples.Count() < 2) return 0;

            var avg = samples.Average();
            var sum = samples.Sum(d => Math.Pow(d - avg, 2));
            return Math.Sqrt(sum / (samples.Count() - 1));
        }

        public double GetLowConfidenceRate(double threshold)
        {
            var samples = _confidenceSamples.ToList();
            if (samples.Count() == 0) return 0;

            var lowCount = samples.Count(s => s.Value < threshold);
            return (double)lowCount / samples.Count();
        }

        public double GetHighConfidenceRate(double threshold)
        {
            var samples = _confidenceSamples.ToList();
            if (samples.Count() == 0) return 0;

            var highCount = samples.Count(s => s.Value >= threshold);
            return (double)highCount / samples.Count();
        }

        public AudioQualityTrendDirection CalculateTrend()
        {
            var samples = _confidenceSamples
                .OrderBy(s => s.Timestamp)
                .ToList();

            if (samples.Count() < 10) return AudioQualityTrendDirection.Stable;

            var recentAvg = samples.TakeLast(5).Average(s => s.Value);
            var olderAvg = samples.Take(5).Average(s => s.Value);

            var change = (recentAvg - olderAvg) / olderAvg;

            if (change > 0.05) return AudioQualityTrendDirection.Improving;
            if (change < -0.05) return AudioQualityTrendDirection.Declining;
            return AudioQualityTrendDirection.Stable;
        }

        public double GetTrendChangePercent()
        {
            var samples = _confidenceSamples
                .OrderBy(s => s.Timestamp)
                .ToList();

            if (samples.Count() < 10) return 0;

            var recentAvg = samples.TakeLast(5).Average(s => s.Value);
            var olderAvg = samples.Take(5).Average(s => s.Value);

            return (recentAvg - olderAvg) / olderAvg;
        }

        public void CleanupOldSamples(DateTime cutoff)
        {
            var toKeep = _confidenceSamples.Where(s => s.Timestamp >= cutoff).ToList();
            _confidenceSamples.Clear();
            foreach (var sample in toKeep)
            {
                _confidenceSamples.Add(sample);
            }

            var accuracyToKeep = _accuracySamples.Where(s => s.Timestamp >= cutoff).ToList();
            _accuracySamples.Clear();
            foreach (var sample in accuracyToKeep)
            {
                _accuracySamples.Add(sample);
            }
        }
    }

    /// <summary>
    /// Language-specific quality metrics.
    /// </summary>
    internal class LanguageQualityMetrics : QualityMetrics
    {
        private readonly ConcurrentBag<TimestampedSample> _werSamples = new();

        public new void UpdateMetrics(double? confidence, double? wordErrorRate)
        {
            base.UpdateMetrics(confidence, null);

            if (wordErrorRate.HasValue)
            {
                _werSamples.Add(new TimestampedSample 
                { 
                    Value = wordErrorRate.Value, 
                    Timestamp = DateTime.UtcNow 
                });
            }
        }

        public double GetAverageWordErrorRate()
        {
            var samples = _werSamples.ToList();
            return samples.Count() > 0 ? samples.Average(s => s.Value) : 0;
        }
    }

    /// <summary>
    /// Timestamped sample for trend analysis.
    /// </summary>
    internal class TimestampedSample
    {
        public double Value { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for tracking and analyzing audio quality metrics.
    /// </summary>
    public interface IAudioQualityTracker
    {
        /// <summary>
        /// Tracks transcription quality metrics.
        /// </summary>
        /// <param name="metric">The quality metric to track.</param>
        Task TrackTranscriptionQualityAsync(AudioQualityMetric metric);

        /// <summary>
        /// Gets a quality report for a time period.
        /// </summary>
        /// <param name="startTime">Start time for the report.</param>
        /// <param name="endTime">End time for the report.</param>
        /// <param name="provider">Optional provider filter.</param>
        /// <returns>Audio quality report.</returns>
        Task<AudioQualityReport> GetQualityReportAsync(
            DateTime startTime,
            DateTime endTime,
            string? provider = null);

        /// <summary>
        /// Gets quality thresholds for a provider.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <returns>Quality thresholds.</returns>
        Task<QualityThresholds> GetQualityThresholdsAsync(string provider);

        /// <summary>
        /// Checks if quality metrics are acceptable.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="confidence">The confidence score.</param>
        /// <param name="wordErrorRate">Optional word error rate.</param>
        /// <returns>True if quality is acceptable.</returns>
        Task<bool> IsQualityAcceptableAsync(
            string provider,
            double confidence,
            double? wordErrorRate = null);
    }

    /// <summary>
    /// Audio quality metric.
    /// </summary>
    public class AudioQualityMetric
    {
        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the provider name.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the model used.
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Gets or sets the virtual key.
        /// </summary>
        public string VirtualKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the confidence score (0-1).
        /// </summary>
        public double? Confidence { get; set; }

        /// <summary>
        /// Gets or sets the word error rate (0-1).
        /// </summary>
        public double? WordErrorRate { get; set; }

        /// <summary>
        /// Gets or sets the accuracy score (0-1).
        /// </summary>
        public double? AccuracyScore { get; set; }

        /// <summary>
        /// Gets or sets the language.
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Gets or sets the audio duration in seconds.
        /// </summary>
        public double AudioDurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the processing duration in milliseconds.
        /// </summary>
        public double ProcessingDurationMs { get; set; }

        /// <summary>
        /// Gets or sets quality metadata.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Audio quality report.
    /// </summary>
    public class AudioQualityReport
    {
        /// <summary>
        /// Gets or sets the report period.
        /// </summary>
        public DateTimeRange Period { get; set; } = new();

        /// <summary>
        /// Gets or sets provider quality statistics.
        /// </summary>
        public Dictionary<string, ProviderQualityStats> ProviderQuality { get; set; } = new();

        /// <summary>
        /// Gets or sets model quality statistics.
        /// </summary>
        public Dictionary<string, ModelQualityStats> ModelQuality { get; set; } = new();

        /// <summary>
        /// Gets or sets language quality statistics.
        /// </summary>
        public Dictionary<string, LanguageQualityStats> LanguageQuality { get; set; } = new();

        /// <summary>
        /// Gets or sets quality trends.
        /// </summary>
        public List<QualityTrend> QualityTrends { get; set; } = new();

        /// <summary>
        /// Gets or sets recommendations.
        /// </summary>
        public List<QualityRecommendation> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Provider quality statistics.
    /// </summary>
    public class ProviderQualityStats
    {
        /// <summary>
        /// Gets or sets the provider name.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets average confidence.
        /// </summary>
        public double AverageConfidence { get; set; }

        /// <summary>
        /// Gets or sets minimum confidence.
        /// </summary>
        public double MinimumConfidence { get; set; }

        /// <summary>
        /// Gets or sets maximum confidence.
        /// </summary>
        public double MaximumConfidence { get; set; }

        /// <summary>
        /// Gets or sets confidence standard deviation.
        /// </summary>
        public double ConfidenceStdDev { get; set; }

        /// <summary>
        /// Gets or sets average accuracy.
        /// </summary>
        public double AverageAccuracy { get; set; }

        /// <summary>
        /// Gets or sets sample count.
        /// </summary>
        public long SampleCount { get; set; }

        /// <summary>
        /// Gets or sets low confidence rate.
        /// </summary>
        public double LowConfidenceRate { get; set; }

        /// <summary>
        /// Gets or sets high confidence rate.
        /// </summary>
        public double HighConfidenceRate { get; set; }
    }

    /// <summary>
    /// Model quality statistics.
    /// </summary>
    public class ModelQualityStats
    {
        /// <summary>
        /// Gets or sets the model name.
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets average confidence.
        /// </summary>
        public double AverageConfidence { get; set; }

        /// <summary>
        /// Gets or sets average accuracy.
        /// </summary>
        public double AverageAccuracy { get; set; }

        /// <summary>
        /// Gets or sets sample count.
        /// </summary>
        public long SampleCount { get; set; }

        /// <summary>
        /// Gets or sets performance rating (0-1).
        /// </summary>
        public double PerformanceRating { get; set; }
    }

    /// <summary>
    /// Language quality statistics.
    /// </summary>
    public class LanguageQualityStats
    {
        /// <summary>
        /// Gets or sets the language code.
        /// </summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets average confidence.
        /// </summary>
        public double AverageConfidence { get; set; }

        /// <summary>
        /// Gets or sets average word error rate.
        /// </summary>
        public double AverageWordErrorRate { get; set; }

        /// <summary>
        /// Gets or sets sample count.
        /// </summary>
        public long SampleCount { get; set; }

        /// <summary>
        /// Gets or sets quality score (0-1).
        /// </summary>
        public double QualityScore { get; set; }
    }

    /// <summary>
    /// Quality trend information.
    /// </summary>
    public class QualityTrend
    {
        /// <summary>
        /// Gets or sets the provider.
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Gets or sets the metric name.
        /// </summary>
        public string Metric { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the trend direction.
        /// </summary>
        public AudioQualityTrendDirection Direction { get; set; }

        /// <summary>
        /// Gets or sets the change percentage.
        /// </summary>
        public double ChangePercent { get; set; }
    }


    /// <summary>
    /// Quality recommendation.
    /// </summary>
    public class QualityRecommendation
    {
        /// <summary>
        /// Gets or sets the recommendation type.
        /// </summary>
        public RecommendationType Type { get; set; }

        /// <summary>
        /// Gets or sets the severity.
        /// </summary>
        public RecommendationSeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets the affected provider.
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Gets or sets the affected language.
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Gets or sets the recommendation message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the expected impact.
        /// </summary>
        public string? Impact { get; set; }
    }

    /// <summary>
    /// Recommendation type.
    /// </summary>
    public enum RecommendationType
    {
        /// <summary>
        /// Switch to a different provider.
        /// </summary>
        ProviderSwitch,

        /// <summary>
        /// Upgrade to a better model.
        /// </summary>
        ModelUpgrade,

        /// <summary>
        /// Adjust configuration settings.
        /// </summary>
        ConfigurationChange,

        /// <summary>
        /// Improve audio preprocessing.
        /// </summary>
        PreprocessingImprovement
    }

    /// <summary>
    /// Recommendation severity.
    /// </summary>
    public enum RecommendationSeverity
    {
        /// <summary>
        /// Low severity.
        /// </summary>
        Low,

        /// <summary>
        /// Medium severity.
        /// </summary>
        Medium,

        /// <summary>
        /// High severity.
        /// </summary>
        High
    }

    /// <summary>
    /// Quality thresholds for acceptable performance.
    /// </summary>
    public class QualityThresholds
    {
        /// <summary>
        /// Gets or sets minimum acceptable confidence.
        /// </summary>
        public double MinimumConfidence { get; set; }

        /// <summary>
        /// Gets or sets maximum acceptable word error rate.
        /// </summary>
        public double MaximumWordErrorRate { get; set; }

        /// <summary>
        /// Gets or sets minimum acceptable accuracy.
        /// </summary>
        public double MinimumAccuracy { get; set; }

        /// <summary>
        /// Gets or sets optimal confidence level.
        /// </summary>
        public double OptimalConfidence { get; set; }

        /// <summary>
        /// Gets or sets optimal word error rate.
        /// </summary>
        public double OptimalWordErrorRate { get; set; }

        /// <summary>
        /// Gets or sets optimal accuracy level.
        /// </summary>
        public double OptimalAccuracy { get; set; }
    }
}
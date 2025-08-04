using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs.Audio
{
    /// <summary>
    /// DTO for audio usage log entry.
    /// </summary>
    public class AudioUsageDto
    {
        /// <summary>
        /// Unique identifier for the usage log.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Virtual key used for the request.
        /// </summary>
        public string VirtualKey { get; set; } = string.Empty;

        /// <summary>
        /// Provider ID that handled the request.
        /// </summary>
        public int ProviderId { get; set; }

        /// <summary>
        /// Type of audio operation.
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Model used for the operation.
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Request identifier for correlation.
        /// </summary>
        public string? RequestId { get; set; }

        /// <summary>
        /// Session ID for real-time sessions.
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// Duration in seconds (for audio operations).
        /// </summary>
        public double? DurationSeconds { get; set; }

        /// <summary>
        /// Character count (for TTS operations).
        /// </summary>
        public int? CharacterCount { get; set; }

        /// <summary>
        /// Input tokens (for real-time with LLM).
        /// </summary>
        public int? InputTokens { get; set; }

        /// <summary>
        /// Output tokens (for real-time with LLM).
        /// </summary>
        public int? OutputTokens { get; set; }

        /// <summary>
        /// Calculated cost in USD.
        /// </summary>
        public decimal Cost { get; set; }

        /// <summary>
        /// Language code used.
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Voice ID used (for TTS/realtime).
        /// </summary>
        public string? Voice { get; set; }

        /// <summary>
        /// HTTP status code of the response.
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// Error message if operation failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Client IP address.
        /// </summary>
        public string? IpAddress { get; set; }

        /// <summary>
        /// User agent string.
        /// </summary>
        public string? UserAgent { get; set; }

        /// <summary>
        /// Additional metadata as JSON.
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// When the usage occurred.
        /// </summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// DTO for audio usage summary statistics.
    /// </summary>
    public class AudioUsageSummaryDto
    {
        /// <summary>
        /// Start date of the summary period.
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// End date of the summary period.
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Total number of audio operations.
        /// </summary>
        public int TotalOperations { get; set; }

        /// <summary>
        /// Number of successful operations.
        /// </summary>
        public int SuccessfulOperations { get; set; }

        /// <summary>
        /// Number of failed operations.
        /// </summary>
        public int FailedOperations { get; set; }

        /// <summary>
        /// Total cost in USD.
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Total duration in seconds for audio operations.
        /// </summary>
        public double TotalDurationSeconds { get; set; }

        /// <summary>
        /// Total character count for TTS operations.
        /// </summary>
        public long TotalCharacters { get; set; }

        /// <summary>
        /// Total input tokens for real-time operations.
        /// </summary>
        public long TotalInputTokens { get; set; }

        /// <summary>
        /// Total output tokens for real-time operations.
        /// </summary>
        public long TotalOutputTokens { get; set; }

        /// <summary>
        /// Breakdown by operation type.
        /// </summary>
        public List<OperationTypeBreakdown> OperationBreakdown { get; set; } = new();

        /// <summary>
        /// Breakdown by provider.
        /// </summary>
        public List<ProviderBreakdown> ProviderBreakdown { get; set; } = new();

        /// <summary>
        /// Breakdown by virtual key.
        /// </summary>
        public List<VirtualKeyBreakdown> VirtualKeyBreakdown { get; set; } = new();
    }

    /// <summary>
    /// Breakdown of usage by operation type.
    /// </summary>
    public class OperationTypeBreakdown
    {
        /// <summary>
        /// Operation type name.
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Number of operations.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Total cost for this operation type.
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Average cost per operation.
        /// </summary>
        public decimal AverageCost { get; set; }
    }

    /// <summary>
    /// Breakdown of usage by provider.
    /// </summary>
    public class ProviderBreakdown
    {
        /// <summary>
        /// Provider ID.
        /// </summary>
        public int ProviderId { get; set; }

        /// <summary>
        /// Provider name.
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Number of operations.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Total cost for this provider.
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Success rate percentage.
        /// </summary>
        public double SuccessRate { get; set; }
    }

    /// <summary>
    /// Breakdown of usage by virtual key.
    /// </summary>
    public class VirtualKeyBreakdown
    {
        /// <summary>
        /// Virtual key hash.
        /// </summary>
        public string VirtualKey { get; set; } = string.Empty;

        /// <summary>
        /// Virtual key name (if available).
        /// </summary>
        public string? KeyName { get; set; }

        /// <summary>
        /// Number of operations.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Total cost for this key.
        /// </summary>
        public decimal TotalCost { get; set; }
    }

    /// <summary>
    /// Query parameters for audio usage.
    /// </summary>
    public class AudioUsageQueryDto
    {
        /// <summary>
        /// Filter by virtual key.
        /// </summary>
        public string? VirtualKey { get; set; }

        /// <summary>
        /// Filter by provider ID.
        /// </summary>
        public int? ProviderId { get; set; }

        /// <summary>
        /// Filter by operation type.
        /// </summary>
        public string? OperationType { get; set; }

        /// <summary>
        /// Start date for the query.
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// End date for the query.
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Page number (1-based).
        /// </summary>
        [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0")]
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size.
        /// </summary>
        [Range(1, 1000, ErrorMessage = "PageSize must be between 1 and 1000")]
        public int PageSize { get; set; } = 50;

        /// <summary>
        /// Include only failed operations.
        /// </summary>
        public bool OnlyErrors { get; set; } = false;
    }

    /// <summary>
    /// DTO for audio key usage statistics.
    /// </summary>
    public class AudioKeyUsageDto
    {
        /// <summary>
        /// Virtual key identifier.
        /// </summary>
        public string VirtualKey { get; set; } = string.Empty;

        /// <summary>
        /// Name of the virtual key.
        /// </summary>
        public string KeyName { get; set; } = string.Empty;

        /// <summary>
        /// Total number of audio operations.
        /// </summary>
        public int TotalOperations { get; set; }

        /// <summary>
        /// Total cost in USD.
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Total duration in seconds.
        /// </summary>
        public double TotalDurationSeconds { get; set; }

        /// <summary>
        /// Last usage timestamp.
        /// </summary>
        public DateTime? LastUsed { get; set; }

        /// <summary>
        /// Success rate percentage.
        /// </summary>
        public double SuccessRate { get; set; }
    }

    /// <summary>
    /// DTO for audio provider usage statistics.
    /// </summary>
    public class AudioProviderUsageDto
    {
        /// <summary>
        /// Provider ID.
        /// </summary>
        public int ProviderId { get; set; }

        /// <summary>
        /// Provider name.
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Total number of operations.
        /// </summary>
        public int TotalOperations { get; set; }

        /// <summary>
        /// Number of transcription operations.
        /// </summary>
        public int TranscriptionCount { get; set; }

        /// <summary>
        /// Number of text-to-speech operations.
        /// </summary>
        public int TextToSpeechCount { get; set; }

        /// <summary>
        /// Number of real-time sessions.
        /// </summary>
        public int RealtimeSessionCount { get; set; }

        /// <summary>
        /// Total cost in USD.
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Average response time in milliseconds.
        /// </summary>
        public double AverageResponseTime { get; set; }

        /// <summary>
        /// Success rate percentage.
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Most used model.
        /// </summary>
        public string? MostUsedModel { get; set; }
    }

    /// <summary>
    /// DTO for daily usage trend data.
    /// </summary>
    public class DailyUsageTrend
    {
        /// <summary>
        /// Date of the usage.
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Number of operations on this date.
        /// </summary>
        public int OperationCount { get; set; }

        /// <summary>
        /// Total cost for this date.
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Total duration in seconds for this date.
        /// </summary>
        public double TotalDurationSeconds { get; set; }

        /// <summary>
        /// Number of unique virtual keys used.
        /// </summary>
        public int UniqueKeys { get; set; }

        /// <summary>
        /// Number of unique providers used.
        /// </summary>
        public int UniqueProviders { get; set; }

        /// <summary>
        /// Success rate for this date.
        /// </summary>
        public double SuccessRate { get; set; }
    }
}

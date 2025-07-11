using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ConduitLLM.AdminClient.Models
{
    /// <summary>
    /// Represents a request to create or update an audio provider configuration.
    /// </summary>
    public class AudioProviderConfigRequest
    {
        /// <summary>
        /// Gets or sets the name of the audio provider.
        /// </summary>
        [Required]
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the base URL for the audio provider API.
        /// </summary>
        [Required]
        [JsonPropertyName("baseUrl")]
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the API key for authentication.
        /// </summary>
        [Required]
        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether this provider is enabled.
        /// </summary>
        [JsonPropertyName("isEnabled")]
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the supported operation types.
        /// </summary>
        [JsonPropertyName("supportedOperations")]
        public List<string> SupportedOperations { get; set; } = new();

        /// <summary>
        /// Gets or sets additional configuration settings.
        /// </summary>
        [JsonPropertyName("settings")]
        public Dictionary<string, object>? Settings { get; set; }

        /// <summary>
        /// Gets or sets the priority/weight of this provider.
        /// </summary>
        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 1;

        /// <summary>
        /// Gets or sets the timeout in seconds for requests to this provider.
        /// </summary>
        [JsonPropertyName("timeoutSeconds")]
        public int TimeoutSeconds { get; set; } = 30;
    }

    /// <summary>
    /// Represents an audio provider configuration.
    /// </summary>
    public class AudioProviderConfigDto : AudioProviderConfigRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier for the provider configuration.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the configuration was created.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the configuration was last updated.
        /// </summary>
        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the last time the provider was tested.
        /// </summary>
        [JsonPropertyName("lastTestedAt")]
        public DateTime? LastTestedAt { get; set; }

        /// <summary>
        /// Gets or sets whether the last test was successful.
        /// </summary>
        [JsonPropertyName("lastTestSuccessful")]
        public bool? LastTestSuccessful { get; set; }

        /// <summary>
        /// Gets or sets the result message from the last test.
        /// </summary>
        [JsonPropertyName("lastTestMessage")]
        public string? LastTestMessage { get; set; }
    }

    /// <summary>
    /// Represents a request to create or update audio cost configuration.
    /// </summary>
    public class AudioCostConfigRequest
    {
        /// <summary>
        /// Gets or sets the audio provider identifier.
        /// </summary>
        [Required]
        [JsonPropertyName("providerId")]
        public string ProviderId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the operation type (e.g., "speech-to-text", "text-to-speech").
        /// </summary>
        [Required]
        [JsonPropertyName("operationType")]
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the model name.
        /// </summary>
        [JsonPropertyName("modelName")]
        public string? ModelName { get; set; }

        /// <summary>
        /// Gets or sets the cost per unit.
        /// </summary>
        [Required]
        [JsonPropertyName("costPerUnit")]
        public decimal CostPerUnit { get; set; }

        /// <summary>
        /// Gets or sets the unit type (e.g., "minute", "character", "request").
        /// </summary>
        [Required]
        [JsonPropertyName("unitType")]
        public string UnitType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the currency code.
        /// </summary>
        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "USD";

        /// <summary>
        /// Gets or sets whether this cost configuration is active.
        /// </summary>
        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets when this cost configuration becomes effective.
        /// </summary>
        [JsonPropertyName("effectiveFrom")]
        public DateTime? EffectiveFrom { get; set; }

        /// <summary>
        /// Gets or sets when this cost configuration expires.
        /// </summary>
        [JsonPropertyName("effectiveTo")]
        public DateTime? EffectiveTo { get; set; }
    }

    /// <summary>
    /// Represents an audio cost configuration.
    /// </summary>
    public class AudioCostConfigDto : AudioCostConfigRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier for the cost configuration.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the configuration was created.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the configuration was last updated.
        /// </summary>
        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Represents audio usage information.
    /// </summary>
    public class AudioUsageDto
    {
        /// <summary>
        /// Gets or sets the unique identifier for the usage entry.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the virtual key that was used.
        /// </summary>
        [JsonPropertyName("virtualKey")]
        public string VirtualKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the audio provider that was used.
        /// </summary>
        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the operation type.
        /// </summary>
        [JsonPropertyName("operationType")]
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the model that was used.
        /// </summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        /// <summary>
        /// Gets or sets the number of units consumed.
        /// </summary>
        [JsonPropertyName("unitsConsumed")]
        public decimal UnitsConsumed { get; set; }

        /// <summary>
        /// Gets or sets the unit type.
        /// </summary>
        [JsonPropertyName("unitType")]
        public string UnitType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the cost incurred.
        /// </summary>
        [JsonPropertyName("cost")]
        public decimal Cost { get; set; }

        /// <summary>
        /// Gets or sets the currency.
        /// </summary>
        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "USD";

        /// <summary>
        /// Gets or sets when the usage occurred.
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the duration of the audio processing in seconds.
        /// </summary>
        [JsonPropertyName("durationSeconds")]
        public double? DurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the size of the audio file in bytes.
        /// </summary>
        [JsonPropertyName("fileSizeBytes")]
        public long? FileSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets additional metadata about the usage.
        /// </summary>
        [JsonPropertyName("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Represents audio usage summary information.
    /// </summary>
    public class AudioUsageSummaryDto
    {
        /// <summary>
        /// Gets or sets the start date of the summary period.
        /// </summary>
        [JsonPropertyName("startDate")]
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Gets or sets the end date of the summary period.
        /// </summary>
        [JsonPropertyName("endDate")]
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Gets or sets the total number of requests.
        /// </summary>
        [JsonPropertyName("totalRequests")]
        public int TotalRequests { get; set; }

        /// <summary>
        /// Gets or sets the total cost.
        /// </summary>
        [JsonPropertyName("totalCost")]
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Gets or sets the currency.
        /// </summary>
        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "USD";

        /// <summary>
        /// Gets or sets the total duration processed in seconds.
        /// </summary>
        [JsonPropertyName("totalDurationSeconds")]
        public double TotalDurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the total file size processed in bytes.
        /// </summary>
        [JsonPropertyName("totalFileSizeBytes")]
        public long TotalFileSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets usage breakdown by virtual key.
        /// </summary>
        [JsonPropertyName("usageByKey")]
        public List<AudioKeyUsageDto> UsageByKey { get; set; } = new();

        /// <summary>
        /// Gets or sets usage breakdown by provider.
        /// </summary>
        [JsonPropertyName("usageByProvider")]
        public List<AudioProviderUsageDto> UsageByProvider { get; set; } = new();

        /// <summary>
        /// Gets or sets usage breakdown by operation type.
        /// </summary>
        [JsonPropertyName("usageByOperation")]
        public List<AudioOperationUsageDto> UsageByOperation { get; set; } = new();
    }

    /// <summary>
    /// Represents audio usage breakdown by virtual key.
    /// </summary>
    public class AudioKeyUsageDto
    {
        /// <summary>
        /// Gets or sets the virtual key.
        /// </summary>
        [JsonPropertyName("virtualKey")]
        public string VirtualKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of requests.
        /// </summary>
        [JsonPropertyName("requestCount")]
        public int RequestCount { get; set; }

        /// <summary>
        /// Gets or sets the total cost.
        /// </summary>
        [JsonPropertyName("totalCost")]
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Gets or sets the total duration in seconds.
        /// </summary>
        [JsonPropertyName("totalDurationSeconds")]
        public double TotalDurationSeconds { get; set; }
    }

    /// <summary>
    /// Represents audio usage breakdown by provider.
    /// </summary>
    public class AudioProviderUsageDto
    {
        /// <summary>
        /// Gets or sets the provider name.
        /// </summary>
        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of requests.
        /// </summary>
        [JsonPropertyName("requestCount")]
        public int RequestCount { get; set; }

        /// <summary>
        /// Gets or sets the total cost.
        /// </summary>
        [JsonPropertyName("totalCost")]
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Gets or sets the total duration in seconds.
        /// </summary>
        [JsonPropertyName("totalDurationSeconds")]
        public double TotalDurationSeconds { get; set; }
    }

    /// <summary>
    /// Represents audio usage breakdown by operation type.
    /// </summary>
    public class AudioOperationUsageDto
    {
        /// <summary>
        /// Gets or sets the operation type.
        /// </summary>
        [JsonPropertyName("operationType")]
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of requests.
        /// </summary>
        [JsonPropertyName("requestCount")]
        public int RequestCount { get; set; }

        /// <summary>
        /// Gets or sets the total cost.
        /// </summary>
        [JsonPropertyName("totalCost")]
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Gets or sets the total duration in seconds.
        /// </summary>
        [JsonPropertyName("totalDurationSeconds")]
        public double TotalDurationSeconds { get; set; }
    }

    /// <summary>
    /// Represents a real-time audio session.
    /// </summary>
    public class RealtimeSessionDto
    {
        /// <summary>
        /// Gets or sets the unique session identifier.
        /// </summary>
        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the virtual key being used.
        /// </summary>
        [JsonPropertyName("virtualKey")]
        public string VirtualKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the provider being used.
        /// </summary>
        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the operation type.
        /// </summary>
        [JsonPropertyName("operationType")]
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the model being used.
        /// </summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        /// <summary>
        /// Gets or sets when the session started.
        /// </summary>
        [JsonPropertyName("startedAt")]
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// Gets or sets the current status of the session.
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current metrics for the session.
        /// </summary>
        [JsonPropertyName("metrics")]
        public RealtimeSessionMetricsDto? Metrics { get; set; }
    }

    /// <summary>
    /// Represents real-time session metrics.
    /// </summary>
    public class RealtimeSessionMetricsDto
    {
        /// <summary>
        /// Gets or sets the duration of the session in seconds.
        /// </summary>
        [JsonPropertyName("durationSeconds")]
        public double DurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the number of requests processed.
        /// </summary>
        [JsonPropertyName("requestsProcessed")]
        public int RequestsProcessed { get; set; }

        /// <summary>
        /// Gets or sets the total cost so far.
        /// </summary>
        [JsonPropertyName("totalCost")]
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Gets or sets the average response time in milliseconds.
        /// </summary>
        [JsonPropertyName("averageResponseTimeMs")]
        public double AverageResponseTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the current throughput in requests per minute.
        /// </summary>
        [JsonPropertyName("throughputRpm")]
        public double ThroughputRpm { get; set; }
    }

    /// <summary>
    /// Represents the result of testing an audio provider.
    /// </summary>
    public class AudioProviderTestResult
    {
        /// <summary>
        /// Gets or sets whether the test was successful.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the test result message.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the response time in milliseconds.
        /// </summary>
        [JsonPropertyName("responseTimeMs")]
        public double? ResponseTimeMs { get; set; }

        /// <summary>
        /// Gets or sets when the test was performed.
        /// </summary>
        [JsonPropertyName("testedAt")]
        public DateTime TestedAt { get; set; }

        /// <summary>
        /// Gets or sets additional test details.
        /// </summary>
        [JsonPropertyName("details")]
        public Dictionary<string, object>? Details { get; set; }
    }

    /// <summary>
    /// Common audio operation types.
    /// </summary>
    public static class AudioOperationTypes
    {
        /// <summary>
        /// Speech-to-text operation.
        /// </summary>
        public const string SpeechToText = "speech-to-text";

        /// <summary>
        /// Text-to-speech operation.
        /// </summary>
        public const string TextToSpeech = "text-to-speech";

        /// <summary>
        /// Audio transcription operation.
        /// </summary>
        public const string Transcription = "transcription";

        /// <summary>
        /// Audio translation operation.
        /// </summary>
        public const string Translation = "translation";
    }

    /// <summary>
    /// Common audio unit types.
    /// </summary>
    public static class AudioUnitTypes
    {
        /// <summary>
        /// Cost per minute of audio.
        /// </summary>
        public const string Minute = "minute";

        /// <summary>
        /// Cost per second of audio.
        /// </summary>
        public const string Second = "second";

        /// <summary>
        /// Cost per character processed.
        /// </summary>
        public const string Character = "character";

        /// <summary>
        /// Cost per request.
        /// </summary>
        public const string Request = "request";

        /// <summary>
        /// Cost per byte processed.
        /// </summary>
        public const string Byte = "byte";
    }
}
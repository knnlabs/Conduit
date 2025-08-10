using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ConduitLLM.Providers.Replicate.Models
{
    /// <summary>
    /// Request model for creating a prediction via Replicate API.
    /// </summary>
    /// <remarks>
    /// Based on https://replicate.com/docs/reference/http#predictions.create
    /// </remarks>
    public class ReplicatePredictionRequest
    {
        /// <summary>
        /// Gets or sets the ID or slug of the model version to use for the prediction.
        /// </summary>
        /// <example>"stability-ai/sdxl:39ed52f2a78e934b3ba6e2a89f5b1c712de7dfea535525255b1aa35c5565e08b"</example>
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the input values for the model.
        /// </summary>
        [JsonPropertyName("input")]
        public Dictionary<string, object> Input { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets the webhook URL that will receive a POST request when the prediction completes.
        /// </summary>
        [JsonPropertyName("webhook")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Webhook { get; set; }

        /// <summary>
        /// Gets or sets the webhook events to subscribe to.
        /// </summary>
        [JsonPropertyName("webhook_events_filter")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? WebhookEventsFilter { get; set; }
    }

    /// <summary>
    /// Response model for a prediction created via Replicate API.
    /// </summary>
    /// <remarks>
    /// Based on https://replicate.com/docs/reference/http#predictions.get
    /// </remarks>
    public class ReplicatePredictionResponse
    {
        /// <summary>
        /// Gets or sets the unique identifier for the prediction.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current status of the prediction.
        /// </summary>
        /// <remarks>
        /// Possible values: "starting", "processing", "succeeded", "failed", "canceled"
        /// </remarks>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the input values used for the prediction.
        /// </summary>
        [JsonPropertyName("input")]
        public Dictionary<string, object>? Input { get; set; }

        /// <summary>
        /// Gets or sets the output values from the prediction.
        /// </summary>
        [JsonPropertyName("output")]
        public object? Output { get; set; }

        /// <summary>
        /// Gets or sets the error message if the prediction failed.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        /// <summary>
        /// Gets or sets the URLs for related resources.
        /// </summary>
        [JsonPropertyName("urls")]
        public Dictionary<string, string>? Urls { get; set; }

        /// <summary>
        /// Gets or sets the metrics for the prediction.
        /// </summary>
        [JsonPropertyName("metrics")]
        public ReplicateMetrics? Metrics { get; set; }

        /// <summary>
        /// Gets or sets the version information used for the prediction.
        /// </summary>
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        /// <summary>
        /// Gets or sets when the prediction was created.
        /// </summary>
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the prediction was last updated.
        /// </summary>
        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the prediction was completed.
        /// </summary>
        [JsonPropertyName("completed_at")]
        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>
    /// Metrics for a Replicate prediction.
    /// </summary>
    public class ReplicateMetrics
    {
        /// <summary>
        /// Gets or sets when the prediction was started.
        /// </summary>
        [JsonPropertyName("predict_time")]
        public double? PredictTime { get; set; }
    }

    /// <summary>
    /// Response model for listing predictions via Replicate API.
    /// </summary>
    public class ReplicatePredictionsListResponse
    {
        /// <summary>
        /// Gets or sets the predictions.
        /// </summary>
        [JsonPropertyName("results")]
        public List<ReplicatePredictionResponse>? Results { get; set; }

        /// <summary>
        /// Gets or sets the URL for the next page of results.
        /// </summary>
        [JsonPropertyName("next")]
        public string? Next { get; set; }

        /// <summary>
        /// Gets or sets the URL for the previous page of results.
        /// </summary>
        [JsonPropertyName("previous")]
        public string? Previous { get; set; }
    }

    /// <summary>
    /// Chat-specific model for mapping to Replicate Llama chat format.
    /// </summary>
    public class ReplicateLlamaChatMessage
    {
        /// <summary>
        /// Gets or sets the role of the message sender.
        /// </summary>
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the content of the message.
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response model for a model collection from Replicate API.
    /// </summary>
    public class ReplicateModelsResponse
    {
        /// <summary>
        /// Gets or sets the list of models.
        /// </summary>
        [JsonPropertyName("results")]
        public List<ReplicateModel>? Results { get; set; }

        /// <summary>
        /// Gets or sets the URL for the next page of results.
        /// </summary>
        [JsonPropertyName("next")]
        public string? Next { get; set; }

        /// <summary>
        /// Gets or sets the URL for the previous page of results.
        /// </summary>
        [JsonPropertyName("previous")]
        public string? Previous { get; set; }
    }

    /// <summary>
    /// Model information from Replicate API.
    /// </summary>
    public class ReplicateModel
    {
        /// <summary>
        /// Gets or sets the URL of the model.
        /// </summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        /// <summary>
        /// Gets or sets the owner/username of the model.
        /// </summary>
        [JsonPropertyName("owner")]
        public string? Owner { get; set; }

        /// <summary>
        /// Gets or sets the name of the model.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the description of the model.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the visibility of the model.
        /// </summary>
        [JsonPropertyName("visibility")]
        public string? Visibility { get; set; }

        /// <summary>
        /// Gets or sets the GitHub URL of the model.
        /// </summary>
        [JsonPropertyName("github_url")]
        public string? GitHubUrl { get; set; }

        /// <summary>
        /// Gets or sets the paper URL of the model.
        /// </summary>
        [JsonPropertyName("paper_url")]
        public string? PaperUrl { get; set; }

        /// <summary>
        /// Gets or sets the license URL of the model.
        /// </summary>
        [JsonPropertyName("license_url")]
        public string? LicenseUrl { get; set; }

        /// <summary>
        /// Gets or sets when the model was created.
        /// </summary>
        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }

        /// <summary>
        /// Gets the full slug identifier for the model.
        /// </summary>
        [JsonIgnore]
        public string Slug => $"{Owner}/{Name}";
    }

    /// <summary>
    /// Version information for a Replicate model.
    /// </summary>
    public class ReplicateModelVersion
    {
        /// <summary>
        /// Gets or sets the ID of the version.
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        /// <summary>
        /// Gets or sets when the version was created.
        /// </summary>
        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the cog version used for this model version.
        /// </summary>
        [JsonPropertyName("cog_version")]
        public string? CogVersion { get; set; }

        /// <summary>
        /// Gets or sets the input schema for this model version.
        /// </summary>
        [JsonPropertyName("openapi_schema")]
        public Dictionary<string, object>? OpenApiSchema { get; set; }
    }

    /// <summary>
    /// Response model for listing versions of a model.
    /// </summary>
    public class ReplicateModelVersionsResponse
    {
        /// <summary>
        /// Gets or sets the list of versions.
        /// </summary>
        [JsonPropertyName("results")]
        public List<ReplicateModelVersion>? Results { get; set; }

        /// <summary>
        /// Gets or sets the URL for the next page of results.
        /// </summary>
        [JsonPropertyName("next")]
        public string? Next { get; set; }

        /// <summary>
        /// Gets or sets the URL for the previous page of results.
        /// </summary>
        [JsonPropertyName("previous")]
        public string? Previous { get; set; }
    }
}

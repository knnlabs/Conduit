using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.Configuration.Entities;

/// <summary>
/// Represents cost configuration for a specific model or model pattern in the system.
/// This entity stores pricing information for different operations (input/output tokens, embeddings, images).
/// </summary>
/// <remarks>
/// ModelCost entities are used for cost calculation and budget tracking, with support for wildcard patterns
/// to match model names. The pricing information is used to calculate costs for each request processed
/// through the system, enabling detailed cost reporting and budget management.
/// </remarks>
public class ModelCost
{
    /// <summary>
    /// Gets or sets the unique identifier for the model cost entry.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the model identification pattern, which can include wildcards.
    /// </summary>
    /// <remarks>
    /// Examples: "openai/gpt-4o", "anthropic.claude-3*", "*-embedding-*"
    /// The pattern is used to match against model names for cost calculation,
    /// with support for * wildcard to match multiple models with similar names.
    /// </remarks>
    [Required]
    [MaxLength(255)]
    public string ModelIdPattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the cost per input token for chat/completion requests.
    /// </summary>
    /// <remarks>
    /// This represents the cost in USD for processing each input token.
    /// Stored with high precision (decimal 18,10) to accommodate very small per-token costs.
    /// </remarks>
    [Column(TypeName = "decimal(18, 10)")]
    public decimal InputTokenCost { get; set; } = 0;

    /// <summary>
    /// Gets or sets the cost per output token for chat/completion requests.
    /// </summary>
    /// <remarks>
    /// This represents the cost in USD for generating each output token.
    /// Stored with high precision (decimal 18,10) to accommodate very small per-token costs.
    /// </remarks>
    [Column(TypeName = "decimal(18, 10)")]
    public decimal OutputTokenCost { get; set; } = 0;

    /// <summary>
    /// Gets or sets the cost per token for embedding requests, if applicable.
    /// </summary>
    /// <remarks>
    /// This represents the cost in USD for processing each token in embedding requests.
    /// Nullable because not all models support embedding operations.
    /// Stored with high precision (decimal 18,10) to accommodate very small per-token costs.
    /// </remarks>
    [Column(TypeName = "decimal(18, 10)")]
    public decimal? EmbeddingTokenCost { get; set; }

    /// <summary>
    /// Gets or sets the cost per image for image generation requests, if applicable.
    /// </summary>
    /// <remarks>
    /// This represents the cost in USD for generating each image.
    /// Nullable because not all models support image generation.
    /// Stored with moderate precision (decimal 18,4) as image costs are typically higher than token costs.
    /// </remarks>
    [Column(TypeName = "decimal(18, 4)")]
    public decimal? ImageCostPerImage { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp of this cost record.
    /// </summary>
    /// <remarks>
    /// Automatically set to UTC time when a new record is created.
    /// </remarks>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the last update timestamp of this cost record.
    /// </summary>
    /// <remarks>
    /// Should be updated whenever the cost record is modified.
    /// </remarks>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional description for this model cost entry (for backward compatibility)
    /// </summary>
    [NotMapped]
    public string? Description { get; set; }

    /// <summary>
    /// Optional priority value for this model cost entry (for backward compatibility)
    /// </summary>
    [NotMapped]
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets the cost per minute for audio transcription (speech-to-text), if applicable.
    /// </summary>
    /// <remarks>
    /// This represents the cost in USD for processing each minute of audio input.
    /// Nullable because not all models support audio transcription.
    /// Stored with moderate precision (decimal 18,4) for audio processing costs.
    /// </remarks>
    [Column(TypeName = "decimal(18, 4)")]
    public decimal? AudioCostPerMinute { get; set; }

    /// <summary>
    /// Gets or sets the cost per 1000 characters for text-to-speech synthesis, if applicable.
    /// </summary>
    /// <remarks>
    /// This represents the cost in USD for synthesizing speech from each 1000 characters of text.
    /// Nullable because not all models support text-to-speech.
    /// Stored with moderate precision (decimal 18,4) for TTS costs.
    /// </remarks>
    [Column(TypeName = "decimal(18, 4)")]
    public decimal? AudioCostPerKCharacters { get; set; }

    /// <summary>
    /// Gets or sets the cost per minute for real-time audio input, if applicable.
    /// </summary>
    /// <remarks>
    /// This represents the cost in USD for processing each minute of real-time audio input.
    /// Used for conversational AI and real-time voice interactions.
    /// Nullable because not all models support real-time audio.
    /// Stored with moderate precision (decimal 18,4) for audio streaming costs.
    /// </remarks>
    [Column(TypeName = "decimal(18, 4)")]
    public decimal? AudioInputCostPerMinute { get; set; }

    /// <summary>
    /// Gets or sets the cost per minute for real-time audio output, if applicable.
    /// </summary>
    /// <remarks>
    /// This represents the cost in USD for generating each minute of real-time audio output.
    /// Used for conversational AI and real-time voice interactions.
    /// Nullable because not all models support real-time audio.
    /// Stored with moderate precision (decimal 18,4) for audio streaming costs.
    /// </remarks>
    [Column(TypeName = "decimal(18, 4)")]
    public decimal? AudioOutputCostPerMinute { get; set; }

    /// <summary>
    /// Gets or sets the base cost per second for video generation, if applicable.
    /// </summary>
    /// <remarks>
    /// This represents the base cost in USD for generating each second of video.
    /// The actual cost may be adjusted based on resolution using VideoResolutionMultipliers.
    /// Nullable because not all models support video generation.
    /// Stored with moderate precision (decimal 18,4) for video generation costs.
    /// </remarks>
    [Column(TypeName = "decimal(18, 4)")]
    public decimal? VideoCostPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the resolution-based cost multipliers for video generation.
    /// </summary>
    /// <remarks>
    /// JSON object containing resolution-to-multiplier mappings.
    /// Example: {"720p": 1.0, "1080p": 1.5, "4k": 2.5}
    /// The base VideoCostPerSecond is multiplied by these values based on the requested resolution.
    /// Stored as JSON text in the database.
    /// </remarks>
    public string? VideoResolutionMultipliers { get; set; }

    /// <summary>
    /// Gets or sets the cost multiplier for batch processing operations, if applicable.
    /// </summary>
    /// <remarks>
    /// This represents a cost reduction factor for batch API usage.
    /// Example: 0.5 means 50% discount (half price), 0.6 means 40% discount.
    /// Applied to the standard token costs when requests are processed through batch APIs.
    /// Nullable because not all models support batch processing.
    /// Stored with moderate precision (decimal 18,4) for percentage-based multipliers.
    /// </remarks>
    [Column(TypeName = "decimal(18, 4)")]
    public decimal? BatchProcessingMultiplier { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports batch processing.
    /// </summary>
    /// <remarks>
    /// Indicates if the model has batch API capabilities for discounted processing.
    /// When true, requests can be processed through batch endpoints with the BatchProcessingMultiplier discount applied.
    /// Default is false for backward compatibility.
    /// </remarks>
    public bool SupportsBatchProcessing { get; set; }

    /// <summary>
    /// Gets or sets the quality-based cost multipliers for image generation.
    /// </summary>
    /// <remarks>
    /// JSON object containing quality-to-multiplier mappings.
    /// Example: {"standard": 1.0, "hd": 2.0}
    /// The base ImageCostPerImage is multiplied by these values based on the requested quality.
    /// Stored as JSON text in the database.
    /// </remarks>
    public string? ImageQualityMultipliers { get; set; }

    /// <summary>
    /// Gets or sets the cost per cached input token for prompt caching, if applicable.
    /// </summary>
    /// <remarks>
    /// This represents the cost in USD for processing each cached input token (reading from cache).
    /// Used by providers like Anthropic Claude and Google Gemini that offer prompt caching.
    /// Typically much lower than standard input token costs (e.g., 10% of regular cost).
    /// Nullable because not all models support prompt caching.
    /// Stored with high precision (decimal 18,10) to accommodate very small per-token costs.
    /// </remarks>
    [Column(TypeName = "decimal(18, 10)")]
    public decimal? CachedInputTokenCost { get; set; }

    /// <summary>
    /// Gets or sets the cost per token for writing to the prompt cache, if applicable.
    /// </summary>
    /// <remarks>
    /// This represents the cost in USD for writing tokens to the prompt cache.
    /// Used by providers like Anthropic Claude and Google Gemini that offer prompt caching.
    /// Typically higher than cached read costs but may be lower than standard input costs.
    /// The write cost is incurred when new content is added to the cache.
    /// Nullable because not all models support prompt caching.
    /// Stored with high precision (decimal 18,10) to accommodate very small per-token costs.
    /// </remarks>
    [Column(TypeName = "decimal(18, 10)")]
    public decimal? CachedInputWriteCost { get; set; }

    /// <summary>
    /// Gets or sets the cost per search unit for reranking models, if applicable.
    /// </summary>
    /// <remarks>
    /// This represents the cost in USD per 1000 search units.
    /// Used by reranking models like Cohere Rerank that charge per search unit rather than per token.
    /// A search unit typically consists of 1 query + up to 100 documents to be ranked.
    /// Documents over 500 tokens are split into chunks, each counting as a separate document.
    /// Nullable because not all models use search unit pricing.
    /// Stored with moderate precision (decimal 18,8) to accommodate search unit costs.
    /// </remarks>
    [Column(TypeName = "decimal(18, 8)")]
    public decimal? CostPerSearchUnit { get; set; }

    /// <summary>
    /// Gets or sets the cost per inference step for image generation models, if applicable.
    /// </summary>
    /// <remarks>
    /// This represents the cost in USD for each inference step during image generation.
    /// Used by providers like Fireworks that charge based on the number of iterative refinement steps.
    /// Different models require different numbers of steps to generate an image.
    /// Example: FLUX.1[schnell] uses 4 steps × $0.00035/step = $0.0014 per image.
    /// Example: SDXL typically uses 30 steps × $0.00013/step = $0.0039 per image.
    /// Nullable because not all image models use step-based pricing.
    /// Stored with moderate precision (decimal 18,8) to accommodate per-step costs.
    /// </remarks>
    [Column(TypeName = "decimal(18, 8)")]
    public decimal? CostPerInferenceStep { get; set; }

    /// <summary>
    /// Gets or sets the default number of inference steps for this model.
    /// </summary>
    /// <remarks>
    /// Indicates the standard number of iterative refinement steps this model uses for image generation.
    /// Used when the client request doesn't specify a custom step count.
    /// Different models have different optimal step counts for quality vs speed tradeoffs.
    /// Example: FLUX.1[schnell] uses 4 steps for fast generation, SDXL uses 30 steps for higher quality.
    /// Nullable because not all models use step-based generation or have configurable steps.
    /// </remarks>
    public int? DefaultInferenceSteps { get; set; }
}

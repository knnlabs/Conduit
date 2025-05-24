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
}

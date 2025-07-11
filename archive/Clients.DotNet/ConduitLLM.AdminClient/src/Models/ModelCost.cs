using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.AdminClient.Models;

/// <summary>
/// Represents a model cost configuration.
/// </summary>
public class ModelCostDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the model cost.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the model ID pattern (supports wildcards).
    /// </summary>
    [Required]
    public string ModelIdPattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the cost per input token.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal InputTokenCost { get; set; }

    /// <summary>
    /// Gets or sets the cost per output token.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal OutputTokenCost { get; set; }

    /// <summary>
    /// Gets or sets the cost per embedding token.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? EmbeddingTokenCost { get; set; }

    /// <summary>
    /// Gets or sets the cost per generated image.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? ImageCostPerImage { get; set; }

    /// <summary>
    /// Gets or sets the cost per minute of audio processing.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? AudioCostPerMinute { get; set; }

    /// <summary>
    /// Gets or sets the cost per thousand characters for audio.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? AudioCostPerKCharacters { get; set; }

    /// <summary>
    /// Gets or sets the cost per minute of audio input.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? AudioInputCostPerMinute { get; set; }

    /// <summary>
    /// Gets or sets the cost per minute of audio output.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? AudioOutputCostPerMinute { get; set; }

    /// <summary>
    /// Gets or sets the description of the model cost.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the priority for pattern matching (higher = preferred).
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets when the model cost was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the model cost was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Represents a request to create a model cost configuration.
/// </summary>
public class CreateModelCostRequest
{
    /// <summary>
    /// Gets or sets the model ID pattern (supports wildcards).
    /// </summary>
    [Required]
    public string ModelIdPattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the cost per input token.
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public decimal InputTokenCost { get; set; }

    /// <summary>
    /// Gets or sets the cost per output token.
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public decimal OutputTokenCost { get; set; }

    /// <summary>
    /// Gets or sets the cost per embedding token.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? EmbeddingTokenCost { get; set; }

    /// <summary>
    /// Gets or sets the cost per generated image.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? ImageCostPerImage { get; set; }

    /// <summary>
    /// Gets or sets the cost per minute of audio processing.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? AudioCostPerMinute { get; set; }

    /// <summary>
    /// Gets or sets the cost per thousand characters for audio.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? AudioCostPerKCharacters { get; set; }

    /// <summary>
    /// Gets or sets the cost per minute of audio input.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? AudioInputCostPerMinute { get; set; }

    /// <summary>
    /// Gets or sets the cost per minute of audio output.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? AudioOutputCostPerMinute { get; set; }

    /// <summary>
    /// Gets or sets the description of the model cost.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the priority for pattern matching (higher = preferred).
    /// </summary>
    public int? Priority { get; set; }
}

/// <summary>
/// Represents a request to update a model cost configuration.
/// </summary>
public class UpdateModelCostRequest
{
    /// <summary>
    /// Gets or sets the model ID pattern (supports wildcards).
    /// </summary>
    public string? ModelIdPattern { get; set; }

    /// <summary>
    /// Gets or sets the cost per input token.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? InputTokenCost { get; set; }

    /// <summary>
    /// Gets or sets the cost per output token.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? OutputTokenCost { get; set; }

    /// <summary>
    /// Gets or sets the cost per embedding token.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? EmbeddingTokenCost { get; set; }

    /// <summary>
    /// Gets or sets the cost per generated image.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? ImageCostPerImage { get; set; }

    /// <summary>
    /// Gets or sets the cost per minute of audio processing.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? AudioCostPerMinute { get; set; }

    /// <summary>
    /// Gets or sets the cost per thousand characters for audio.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? AudioCostPerKCharacters { get; set; }

    /// <summary>
    /// Gets or sets the cost per minute of audio input.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? AudioInputCostPerMinute { get; set; }

    /// <summary>
    /// Gets or sets the cost per minute of audio output.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? AudioOutputCostPerMinute { get; set; }

    /// <summary>
    /// Gets or sets the description of the model cost.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the priority for pattern matching (higher = preferred).
    /// </summary>
    public int? Priority { get; set; }
}

/// <summary>
/// Represents filter criteria for querying model costs.
/// </summary>
public class ModelCostFilters : FilterOptions
{
    /// <summary>
    /// Gets or sets the model pattern filter.
    /// </summary>
    public string? ModelPattern { get; set; }

    /// <summary>
    /// Gets or sets the provider name filter.
    /// </summary>
    public string? ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the minimum input token cost filter.
    /// </summary>
    public decimal? MinInputCost { get; set; }

    /// <summary>
    /// Gets or sets the maximum input token cost filter.
    /// </summary>
    public decimal? MaxInputCost { get; set; }

    /// <summary>
    /// Gets or sets the minimum output token cost filter.
    /// </summary>
    public decimal? MinOutputCost { get; set; }

    /// <summary>
    /// Gets or sets the maximum output token cost filter.
    /// </summary>
    public decimal? MaxOutputCost { get; set; }
}

/// <summary>
/// Represents cost overview information for a time period.
/// </summary>
public class CostOverviewDto
{
    /// <summary>
    /// Gets or sets the start date of the overview period.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date of the overview period.
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Gets or sets the total cost for the period.
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Gets or sets the total number of requests.
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Gets or sets the total input tokens processed.
    /// </summary>
    public long TotalInputTokens { get; set; }

    /// <summary>
    /// Gets or sets the total output tokens generated.
    /// </summary>
    public long TotalOutputTokens { get; set; }

    /// <summary>
    /// Gets or sets the cost breakdown by model.
    /// </summary>
    public IEnumerable<ModelCostBreakdown> ModelBreakdown { get; set; } = new List<ModelCostBreakdown>();

    /// <summary>
    /// Gets or sets the cost breakdown by provider.
    /// </summary>
    public IEnumerable<ProviderCostBreakdown> ProviderBreakdown { get; set; } = new List<ProviderCostBreakdown>();
}

/// <summary>
/// Represents cost breakdown information for a specific model.
/// </summary>
public class ModelCostBreakdown
{
    /// <summary>
    /// Gets or sets the model ID.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total cost for this model.
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Gets or sets the number of requests for this model.
    /// </summary>
    public long RequestCount { get; set; }

    /// <summary>
    /// Gets or sets the input tokens for this model.
    /// </summary>
    public long InputTokens { get; set; }

    /// <summary>
    /// Gets or sets the output tokens for this model.
    /// </summary>
    public long OutputTokens { get; set; }
}

/// <summary>
/// Represents cost breakdown information for a specific provider.
/// </summary>
public class ProviderCostBreakdown
{
    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total cost for this provider.
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Gets or sets the number of requests for this provider.
    /// </summary>
    public long RequestCount { get; set; }

    /// <summary>
    /// Gets or sets the input tokens for this provider.
    /// </summary>
    public long InputTokens { get; set; }

    /// <summary>
    /// Gets or sets the output tokens for this provider.
    /// </summary>
    public long OutputTokens { get; set; }
}

/// <summary>
/// Represents a request to import bulk model costs.
/// </summary>
public class ImportModelCostsRequest
{
    /// <summary>
    /// Gets or sets the model costs to import.
    /// </summary>
    [Required]
    public IEnumerable<CreateModelCostRequest> ModelCosts { get; set; } = new List<CreateModelCostRequest>();

    /// <summary>
    /// Gets or sets whether to replace existing costs.
    /// </summary>
    public bool? ReplaceExisting { get; set; }
}

/// <summary>
/// Represents the result of importing model costs.
/// </summary>
public class ImportModelCostsResult
{
    /// <summary>
    /// Gets or sets the number of costs imported.
    /// </summary>
    public int Imported { get; set; }

    /// <summary>
    /// Gets or sets the number of costs updated.
    /// </summary>
    public int Updated { get; set; }

    /// <summary>
    /// Gets or sets the number of costs skipped.
    /// </summary>
    public int Skipped { get; set; }

    /// <summary>
    /// Gets or sets any errors that occurred during import.
    /// </summary>
    public IEnumerable<string> Errors { get; set; } = new List<string>();
}
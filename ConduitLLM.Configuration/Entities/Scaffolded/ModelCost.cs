using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class ModelCost
{
    public int Id { get; set; }

    public string ModelIdPattern { get; set; } = null!;

    public decimal InputTokenCost { get; set; }

    public decimal OutputTokenCost { get; set; }

    public decimal? EmbeddingTokenCost { get; set; }

    public decimal? ImageCostPerImage { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public decimal? AudioCostPerMinute { get; set; }

    public decimal? AudioCostPerKcharacters { get; set; }

    public decimal? AudioInputCostPerMinute { get; set; }

    public decimal? AudioOutputCostPerMinute { get; set; }

    public decimal? VideoCostPerSecond { get; set; }

    public string? VideoResolutionMultipliers { get; set; }

    public decimal? BatchProcessingMultiplier { get; set; }

    public bool SupportsBatchProcessing { get; set; }

    public string? ImageQualityMultipliers { get; set; }

    public decimal? CachedInputTokenCost { get; set; }

    public decimal? CachedInputWriteCost { get; set; }

    public decimal? CostPerSearchUnit { get; set; }

    public decimal? CostPerInferenceStep { get; set; }

    public int? DefaultInferenceSteps { get; set; }
}

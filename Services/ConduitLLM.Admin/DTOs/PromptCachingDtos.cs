using System.ComponentModel.DataAnnotations;

using ConduitLLM.Core.Models;

namespace ConduitLLM.Admin.DTOs;

public sealed class PromptCachingConfigDto
{
    public int SchemaVersion { get; set; } = 3;
    public bool Enabled { get; set; }
    public List<PromptCachingRule> Rules { get; set; } = new();
}

public sealed class UpdatePromptCachingConfigDto
{
    [Range(3, 3)]
    public int SchemaVersion { get; set; } = 3;
    public bool Enabled { get; set; }
    [Required]
    public List<PromptCachingRule> Rules { get; set; } = new();
}

public sealed class PromptCachingAnalyticsDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int Requests { get; set; }
    public int EligibleMisses { get; set; }
    public int ReadEvents { get; set; }
    public int WriteEvents { get; set; }
    public int UnknownOutcomes { get; set; }
    public long CachedTokens { get; set; }
    public decimal GrossSavings { get; set; }
    public decimal WritePremium { get; set; }
    public decimal NetSavings => GrossSavings - WritePremium;
    public double? HitLatencyMs { get; set; }
    public double? MissLatencyMs { get; set; }
    public int AffinityReuse { get; set; }
    public int Failovers { get; set; }
    public List<PromptCachingProviderDistributionDto> ProviderDistribution { get; set; } = new();
}

public sealed class PromptCachingProviderDistributionDto
{
    public string Provider { get; set; } = "unknown";
    public int? MappingId { get; set; }
    public int Requests { get; set; }
}

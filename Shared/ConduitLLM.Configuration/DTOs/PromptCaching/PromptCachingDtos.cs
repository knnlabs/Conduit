using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs.PromptCaching;

public sealed class PromptCachingConfigDto
{
    public int SchemaVersion { get; set; } = 3;
    public bool Enabled { get; set; }
    public List<PromptCachingRuleDto> Rules { get; set; } = new();
}

public sealed class UpdatePromptCachingConfigDto
{
    [Range(3, 3)]
    public int SchemaVersion { get; set; } = 3;
    public bool Enabled { get; set; }
    [Required]
    public List<PromptCachingRuleDto> Rules { get; set; } = new();
}

public sealed class PromptCachingRuleDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    [Required]
    public string Provider { get; set; } = "OpenRouter";
    [Required, MaxLength(200)]
    public string ModelPattern { get; set; } = string.Empty;
    [Required]
    public string Strategy { get; set; } = string.Empty;
    public string? Ttl { get; set; }
    [MaxLength(4)]
    public List<CacheInjectionPointDto> InjectionPoints { get; set; } = new();
}

public sealed class CacheInjectionPointDto
{
    [RegularExpression("^(system|developer|user|assistant)$")]
    public string? Role { get; set; }
    [Range(-100, 100)]
    public int? Index { get; set; }
}

public sealed class PromptCachingCapabilityDto
{
    public string Provider { get; set; } = string.Empty;
    public string ModelPattern { get; set; } = string.Empty;
    public List<string> Strategies { get; set; } = new();
    public List<string> Ttls { get; set; } = new();
    public int? MinimumTokens { get; set; }
    public int MaxBreakpoints { get; set; }
    public bool ProviderManaged { get; set; }
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

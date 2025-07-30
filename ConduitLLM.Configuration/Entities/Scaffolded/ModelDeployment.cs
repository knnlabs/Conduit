using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class ModelDeployment
{
    public Guid Id { get; set; }

    public string ModelName { get; set; } = null!;

    public int ProviderType { get; set; }

    public int Weight { get; set; }

    public bool HealthCheckEnabled { get; set; }

    public bool IsEnabled { get; set; }

    public int? Rpm { get; set; }

    public int? Tpm { get; set; }

    public decimal? InputTokenCostPer1K { get; set; }

    public decimal? OutputTokenCostPer1K { get; set; }

    public int Priority { get; set; }

    public bool IsHealthy { get; set; }

    public int RouterConfigId { get; set; }

    public DateTime LastUpdated { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string DeploymentName { get; set; } = null!;

    public bool SupportsEmbeddings { get; set; }

    public virtual RouterConfigEntity RouterConfig { get; set; } = null!;
}

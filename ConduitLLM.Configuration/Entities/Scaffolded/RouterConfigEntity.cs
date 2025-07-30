using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class RouterConfigEntity
{
    public int Id { get; set; }

    public string DefaultRoutingStrategy { get; set; } = null!;

    public int MaxRetries { get; set; }

    public int RetryBaseDelayMs { get; set; }

    public int RetryMaxDelayMs { get; set; }

    public bool FallbacksEnabled { get; set; }

    public DateTime LastUpdated { get; set; }

    public bool IsActive { get; set; }

    public string Name { get; set; } = null!;

    public DateTime UpdatedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<FallbackConfiguration> FallbackConfigurations { get; set; } = new List<FallbackConfiguration>();

    public virtual ICollection<ModelDeployment> ModelDeployments { get; set; } = new List<ModelDeployment>();
}

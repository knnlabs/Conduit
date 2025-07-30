using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class FallbackConfiguration
{
    public Guid Id { get; set; }

    public Guid PrimaryModelDeploymentId { get; set; }

    public int RouterConfigId { get; set; }

    public DateTime LastUpdated { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual ICollection<FallbackModelMapping> FallbackModelMappings { get; set; } = new List<FallbackModelMapping>();

    public virtual RouterConfigEntity RouterConfig { get; set; } = null!;
}

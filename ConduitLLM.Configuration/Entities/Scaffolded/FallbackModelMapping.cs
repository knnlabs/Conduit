using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class FallbackModelMapping
{
    public int Id { get; set; }

    public Guid FallbackConfigurationId { get; set; }

    public Guid ModelDeploymentId { get; set; }

    public int Order { get; set; }

    public string SourceModelName { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual FallbackConfiguration FallbackConfiguration { get; set; } = null!;
}

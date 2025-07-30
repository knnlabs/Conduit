using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class CacheConfigurationAudit
{
    public int Id { get; set; }

    public string Region { get; set; } = null!;

    public string Action { get; set; } = null!;

    public string? OldConfigJson { get; set; }

    public string? NewConfigJson { get; set; }

    public string? Reason { get; set; }

    public string ChangedBy { get; set; } = null!;

    public DateTime ChangedAt { get; set; }

    public string? ChangeSource { get; set; }

    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }
}

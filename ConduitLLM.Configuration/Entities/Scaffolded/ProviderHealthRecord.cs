using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class ProviderHealthRecord
{
    public int Id { get; set; }

    public int ProviderType { get; set; }

    public int Status { get; set; }

    public bool IsOnline { get; set; }

    public string StatusMessage { get; set; } = null!;

    public DateTime TimestampUtc { get; set; }

    public double ResponseTimeMs { get; set; }

    public string? ErrorCategory { get; set; }

    public string? ErrorDetails { get; set; }

    public string? EndpointUrl { get; set; }
}

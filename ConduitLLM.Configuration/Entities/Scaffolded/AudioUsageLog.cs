using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class AudioUsageLog
{
    public long Id { get; set; }

    public string VirtualKey { get; set; } = null!;

    public string OperationType { get; set; } = null!;

    public string? Model { get; set; }

    public string? RequestId { get; set; }

    public string? SessionId { get; set; }

    public double? DurationSeconds { get; set; }

    public int? CharacterCount { get; set; }

    public int? InputTokens { get; set; }

    public int? OutputTokens { get; set; }

    public decimal Cost { get; set; }

    public string? Language { get; set; }

    public string? Voice { get; set; }

    public int? StatusCode { get; set; }

    public string? ErrorMessage { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? Metadata { get; set; }

    public DateTime Timestamp { get; set; }

    public int Provider { get; set; }
}

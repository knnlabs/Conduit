using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class AsyncTask
{
    public string Id { get; set; } = null!;

    public string Type { get; set; } = null!;

    public int State { get; set; }

    public string? Payload { get; set; }

    public int Progress { get; set; }

    public string? ProgressMessage { get; set; }

    public string? Result { get; set; }

    public string? Error { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int VirtualKeyId { get; set; }

    public string? Metadata { get; set; }

    public bool IsArchived { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public string? LeasedBy { get; set; }

    public DateTime? LeaseExpiryTime { get; set; }

    public int Version { get; set; }

    public int RetryCount { get; set; }

    public int MaxRetries { get; set; }

    public bool IsRetryable { get; set; }

    public DateTime? NextRetryAt { get; set; }

    public virtual VirtualKey VirtualKey { get; set; } = null!;
}

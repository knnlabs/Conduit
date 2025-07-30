using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class BatchOperationHistory
{
    public string OperationId { get; set; } = null!;

    public string OperationType { get; set; } = null!;

    public int VirtualKeyId { get; set; }

    public int TotalItems { get; set; }

    public int SuccessCount { get; set; }

    public int FailedCount { get; set; }

    public string Status { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public double? DurationSeconds { get; set; }

    public double? ItemsPerSecond { get; set; }

    public string? ErrorMessage { get; set; }

    public string? CancellationReason { get; set; }

    public string? ErrorDetails { get; set; }

    public string? ResultSummary { get; set; }

    public string? Metadata { get; set; }

    public string? CheckpointData { get; set; }

    public bool CanResume { get; set; }

    public int? LastProcessedIndex { get; set; }

    public virtual VirtualKey VirtualKey { get; set; } = null!;
}

using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.Entities;

/// <summary>
/// Durable cursor for the continuous billing reconciliation job.
/// </summary>
public sealed class BillingReconciliationCheckpoint
{
    [Key]
    public int Id { get; set; }

    public DateTime NextWindowStartUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

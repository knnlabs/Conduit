using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class VirtualKey
{
    public int Id { get; set; }

    public string KeyName { get; set; } = null!;

    public string KeyHash { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsEnabled { get; set; }

    public decimal? MaxBudget { get; set; }

    public decimal CurrentSpend { get; set; }

    public string? BudgetDuration { get; set; }

    public DateTime? BudgetStartDate { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? Metadata { get; set; }

    public string? AllowedModels { get; set; }

    public int? RateLimitRpm { get; set; }

    public int? RateLimitRpd { get; set; }

    public byte[]? RowVersion { get; set; }

    public virtual ICollection<AsyncTask> AsyncTasks { get; set; } = new List<AsyncTask>();

    public virtual ICollection<BatchOperationHistory> BatchOperationHistories { get; set; } = new List<BatchOperationHistory>();

    public virtual ICollection<MediaLifecycleRecord> MediaLifecycleRecords { get; set; } = new List<MediaLifecycleRecord>();

    public virtual ICollection<MediaRecord> MediaRecords { get; set; } = new List<MediaRecord>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<RequestLog> RequestLogs { get; set; } = new List<RequestLog>();

    public virtual ICollection<VirtualKeySpendHistory> VirtualKeySpendHistories { get; set; } = new List<VirtualKeySpendHistory>();
}

using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class AudioCost
{
    public int Id { get; set; }

    public string OperationType { get; set; } = null!;

    public string? Model { get; set; }

    public string CostUnit { get; set; } = null!;

    public decimal CostPerUnit { get; set; }

    public decimal? MinimumCharge { get; set; }

    public string? AdditionalFactors { get; set; }

    public bool IsActive { get; set; }

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Provider { get; set; }
}

using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class VirtualKeySpendHistory
{
    public int Id { get; set; }

    public int VirtualKeyId { get; set; }

    public decimal Amount { get; set; }

    public DateTime Date { get; set; }

    public DateTime Timestamp { get; set; }

    public virtual VirtualKey VirtualKey { get; set; } = null!;
}

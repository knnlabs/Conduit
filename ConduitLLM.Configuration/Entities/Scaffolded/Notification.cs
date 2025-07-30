using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class Notification
{
    public int Id { get; set; }

    public int? VirtualKeyId { get; set; }

    public int Type { get; set; }

    public int Severity { get; set; }

    public string Message { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual VirtualKey? VirtualKey { get; set; }
}

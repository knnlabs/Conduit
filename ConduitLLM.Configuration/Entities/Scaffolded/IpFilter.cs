using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class IpFilter
{
    public int Id { get; set; }

    public string FilterType { get; set; } = null!;

    public string IpAddressOrCidr { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public byte[]? RowVersion { get; set; }
}

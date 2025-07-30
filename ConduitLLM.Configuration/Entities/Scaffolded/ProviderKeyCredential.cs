using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class ProviderKeyCredential
{
    public int Id { get; set; }

    public int ProviderCredentialId { get; set; }

    public short ProviderAccountGroup { get; set; }

    public string? ApiKey { get; set; }

    public string? BaseUrl { get; set; }

    public string? Organization { get; set; }

    public string? KeyName { get; set; }

    public bool IsPrimary { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ProviderCredential ProviderCredential { get; set; } = null!;
}

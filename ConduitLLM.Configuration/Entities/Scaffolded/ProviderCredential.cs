using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class ProviderCredential
{
    public int Id { get; set; }

    public int ProviderType { get; set; }

    public string? BaseUrl { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual AudioProviderConfig? AudioProviderConfig { get; set; }

    public virtual ICollection<ModelProviderMapping> ModelProviderMappings { get; set; } = new List<ModelProviderMapping>();

    public virtual ICollection<ProviderKeyCredential> ProviderKeyCredentials { get; set; } = new List<ProviderKeyCredential>();
}

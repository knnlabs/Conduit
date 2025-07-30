using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class MediaRecord
{
    public Guid Id { get; set; }

    public string StorageKey { get; set; } = null!;

    public int VirtualKeyId { get; set; }

    public string MediaType { get; set; } = null!;

    public string? ContentType { get; set; }

    public long? SizeBytes { get; set; }

    public string? ContentHash { get; set; }

    public string? Provider { get; set; }

    public string? Model { get; set; }

    public string? Prompt { get; set; }

    public string? StorageUrl { get; set; }

    public string? PublicUrl { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastAccessedAt { get; set; }

    public int AccessCount { get; set; }

    public virtual VirtualKey VirtualKey { get; set; } = null!;
}

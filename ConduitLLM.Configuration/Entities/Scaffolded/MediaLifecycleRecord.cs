using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class MediaLifecycleRecord
{
    public int Id { get; set; }

    public int VirtualKeyId { get; set; }

    public string MediaType { get; set; } = null!;

    public string MediaUrl { get; set; } = null!;

    public string StorageKey { get; set; } = null!;

    public long FileSizeBytes { get; set; }

    public string ContentType { get; set; } = null!;

    public string GeneratedByModel { get; set; } = null!;

    public string GenerationPrompt { get; set; } = null!;

    public DateTime GeneratedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string? Metadata { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual VirtualKey VirtualKey { get; set; } = null!;
}

using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class CacheConfiguration
{
    public int Id { get; set; }

    public string Region { get; set; } = null!;

    public bool Enabled { get; set; }

    public int? DefaultTtlSeconds { get; set; }

    public int? MaxTtlSeconds { get; set; }

    public long? MaxEntries { get; set; }

    public long? MaxMemoryBytes { get; set; }

    public string EvictionPolicy { get; set; } = null!;

    public bool UseMemoryCache { get; set; }

    public bool UseDistributedCache { get; set; }

    public bool EnableCompression { get; set; }

    public long? CompressionThresholdBytes { get; set; }

    public int Priority { get; set; }

    public bool EnableDetailedStats { get; set; }

    public string? ExtendedConfig { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public byte[]? Version { get; set; }

    public bool IsActive { get; set; }

    public string? Notes { get; set; }
}

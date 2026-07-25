using ConduitLLM.Configuration;

namespace ConduitLLM.Admin.Endpoints;

// Request contracts shared by the split provider account endpoint files.
// These were previously nested in the deleted MVC controller model file.
public sealed class CreateProviderRequest
{
    public ProviderType ProviderType { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public Dictionary<string, string>? Settings { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool TrustProviderReportedCosts { get; set; }
    public decimal ProviderCostMarkupMultiplier { get; set; } = 1.0m;
}

public sealed class UpdateProviderRequest
{
    public string? ProviderName { get; set; }
    public string? BaseUrl { get; set; }
    public Dictionary<string, string>? Settings { get; set; }
    public bool IsEnabled { get; set; }
    public bool TrustProviderReportedCosts { get; set; }
    public decimal ProviderCostMarkupMultiplier { get; set; } = 1.0m;
}

public sealed class TestProviderRequest
{
    public ProviderType ProviderType { get; set; }
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public Dictionary<string, string>? Settings { get; set; }
}

public sealed class CreateKeyRequest
{
    public string ApiKey { get; set; } = string.Empty;
    public string KeyName { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int? ProviderAccountGroup { get; set; }
}

public sealed class UpdateKeyRequest
{
    public string? KeyName { get; set; }
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public bool? IsPrimary { get; set; }
    public bool? IsEnabled { get; set; }
    public int? ProviderAccountGroup { get; set; }
}

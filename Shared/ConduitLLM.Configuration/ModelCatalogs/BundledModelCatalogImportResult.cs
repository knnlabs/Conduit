using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.ModelCatalogs;

public sealed class BundledModelCatalogImportResult
{
    [Required]
    public int ProvidersProcessed { get; set; }
    [Required]
    public int ModelsDiscovered { get; set; }
    [Required]
    public CatalogImportCounts Created { get; set; } = new();
    [Required]
    public int SkippedExistingIdentifiers { get; set; }
    [Required]
    public List<string> Conflicts { get; set; } = [];
    [Required]
    public List<ProviderCatalogImportResult> Providers { get; set; } = [];
}

public sealed class ProviderCatalogImportResult
{
    [Required]
    public string Provider { get; set; } = string.Empty;
    [Required]
    public int ModelsDiscovered { get; set; }
    [Required]
    public CatalogImportCounts Created { get; set; } = new();
    [Required]
    public int SkippedExistingIdentifiers { get; set; }
    [Required]
    public int Conflicts { get; set; }
}

public sealed class CatalogImportCounts
{
    [Required]
    public int Authors { get; set; }
    [Required]
    public int Series { get; set; }
    [Required]
    public int Models { get; set; }
    [Required]
    public int Costs { get; set; }
    [Required]
    public int Identifiers { get; set; }

    internal void Add(CatalogImportCounts other)
    {
        Authors += other.Authors;
        Series += other.Series;
        Models += other.Models;
        Costs += other.Costs;
        Identifiers += other.Identifiers;
    }
}

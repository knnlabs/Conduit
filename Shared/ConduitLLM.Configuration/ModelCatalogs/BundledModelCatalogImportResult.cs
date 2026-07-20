namespace ConduitLLM.Configuration.ModelCatalogs;

public sealed class BundledModelCatalogImportResult
{
    public int ProvidersProcessed { get; set; }
    public int ModelsDiscovered { get; set; }
    public CatalogImportCounts Created { get; set; } = new();
    public int SkippedExistingIdentifiers { get; set; }
    public List<string> Conflicts { get; set; } = [];
    public List<ProviderCatalogImportResult> Providers { get; set; } = [];
}

public sealed class ProviderCatalogImportResult
{
    public string Provider { get; set; } = string.Empty;
    public int ModelsDiscovered { get; set; }
    public CatalogImportCounts Created { get; set; } = new();
    public int SkippedExistingIdentifiers { get; set; }
    public int Conflicts { get; set; }
}

public sealed class CatalogImportCounts
{
    public int Authors { get; set; }
    public int Series { get; set; }
    public int Models { get; set; }
    public int Costs { get; set; }
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

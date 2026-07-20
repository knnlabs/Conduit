using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.ModelCatalogs;

/// <summary>Merges the immutable bundled model snapshot without modifying matched records.</summary>
public interface IBundledModelCatalogImporter
{
    Task<BundledModelCatalogImportResult?> ImportAsync(
        bool onlyWhenIdentifierCatalogIsEmpty,
        CancellationToken cancellationToken = default);
}

public sealed class BundledModelCatalogImporter : IBundledModelCatalogImporter
{
    private const long CATALOG_IMPORT_LOCK_ID = 7891012;
    private readonly IDbContextFactory<ConduitDbContext> _contextFactory;
    private readonly BundledModelCatalog _catalog;
    private readonly ILogger<BundledModelCatalogImporter> _logger;

    public BundledModelCatalogImporter(
        IDbContextFactory<ConduitDbContext> contextFactory,
        BundledModelCatalog catalog,
        ILogger<BundledModelCatalogImporter> logger)
    {
        _contextFactory = contextFactory;
        _catalog = catalog;
        _logger = logger;
    }

    /// <summary>Returns without importing when <paramref name="onlyWhenIdentifierCatalogIsEmpty"/> is true and identifiers exist.</summary>
    public async Task<BundledModelCatalogImportResult?> ImportAsync(
        bool onlyWhenIdentifierCatalogIsEmpty,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        if (onlyWhenIdentifierCatalogIsEmpty &&
            await context.ModelProviderTypeAssociations.AnyAsync(cancellationToken))
        {
            _logger.LogDebug("Bundled model catalog startup seed skipped because model identifiers already exist");
            return null;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            $"SELECT pg_advisory_xact_lock({CATALOG_IMPORT_LOCK_ID})", cancellationToken);

        // Re-check after taking the lock; another host may have seeded while this host waited.
        if (onlyWhenIdentifierCatalogIsEmpty &&
            await context.ModelProviderTypeAssociations.AnyAsync(cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var result = await MergeAsync(context, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        foreach (var conflict in result.Conflicts)
        {
            _logger.LogWarning("Bundled model catalog entry skipped: {Conflict}", conflict);
        }

        _logger.LogInformation(
            "Bundled model catalog import completed: {Providers} providers, {Discovered} entries, " +
            "{Created} identifiers created, {Skipped} existing identifiers skipped, {Conflicts} conflicts",
            result.ProvidersProcessed,
            result.ModelsDiscovered,
            result.Created.Identifiers,
            result.SkippedExistingIdentifiers,
            result.Conflicts.Count);
        return result;
    }

    private async Task<BundledModelCatalogImportResult> MergeAsync(
        ConduitDbContext context,
        CancellationToken cancellationToken)
    {
        var catalogs = await _catalog.LoadAsync(cancellationToken);
        var result = new BundledModelCatalogImportResult
        {
            ProvidersProcessed = catalogs.Count,
            ModelsDiscovered = catalogs.Sum(x => x.Models.Count)
        };

        var authors = (await context.ModelAuthors.ToListAsync(cancellationToken))
            .ToDictionary(x => x.Name, StringComparer.Ordinal);
        var series = (await context.ModelSeries.Include(x => x.Author).ToListAsync(cancellationToken))
            .ToDictionary(x => SeriesKey(x.Author.Name, x.Name), StringComparer.Ordinal);
        var models = (await context.Models.Include(x => x.Series).ThenInclude(x => x.Author)
                .ToListAsync(cancellationToken))
            .GroupBy(x => ModelKey(x.Series.Author.Name, x.Series.Name, x.Name), StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.Ordinal);
        var costs = (await context.ModelCosts.ToListAsync(cancellationToken))
            .GroupBy(x => x.CostName, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.Ordinal);
        var identifiers = (await context.ModelProviderTypeAssociations
                .Where(x => x.Provider != null)
                .ToListAsync(cancellationToken))
            .ToDictionary(x => IdentifierKey((int?)x.Provider, x.Identifier), StringComparer.Ordinal);

        foreach (var providerCatalog in catalogs)
        {
            var providerResult = new ProviderCatalogImportResult
            {
                Provider = providerCatalog.Name,
                ModelsDiscovered = providerCatalog.Models.Count
            };
            result.Providers.Add(providerResult);

            foreach (var (identifier, catalogModel) in providerCatalog.Models)
            {
                var identifierKey = IdentifierKey(providerCatalog.Configuration.ProviderType, identifier);
                if (identifiers.ContainsKey(identifierKey))
                {
                    providerResult.SkippedExistingIdentifiers++;
                    result.SkippedExistingIdentifiers++;
                    continue;
                }

                var modelName = string.IsNullOrWhiteSpace(catalogModel.Name) ? identifier : catalogModel.Name;
                var ownerName = string.IsNullOrWhiteSpace(catalogModel.Owner)
                    ? providerCatalog.Name
                    : catalogModel.Owner;
                var seriesName = string.IsNullOrWhiteSpace(catalogModel.Series)
                    ? (string.IsNullOrWhiteSpace(catalogModel.Family) ? "Unknown Series" : catalogModel.Family)
                    : catalogModel.Series;
                var modelKey = ModelKey(ownerName, seriesName, modelName);

                if (models.TryGetValue(modelKey, out var matchingModels) && matchingModels.Count > 1)
                {
                    AddConflict(result, providerResult,
                        $"{providerCatalog.Name}/{identifier}: multiple canonical models match '{ownerName} / {seriesName} / {modelName}'.");
                    continue;
                }

                if (costs.TryGetValue(identifier, out var matchingCosts) && matchingCosts.Count > 1)
                {
                    AddConflict(result, providerResult,
                        $"{providerCatalog.Name}/{identifier}: multiple model costs have CostName '{identifier}'.");
                    continue;
                }

                if (!authors.TryGetValue(ownerName, out var author))
                {
                    author = new ModelAuthor
                    {
                        Name = ownerName,
                        WebsiteUrl = providerCatalog.Configuration.WebsiteUrl
                    };
                    context.ModelAuthors.Add(author);
                    authors.Add(ownerName, author);
                    providerResult.Created.Authors++;
                }

                var seriesKey = SeriesKey(ownerName, seriesName);
                if (!series.TryGetValue(seriesKey, out var modelSeries))
                {
                    modelSeries = new ModelSeries
                    {
                        Name = seriesName,
                        Author = author,
                        TokenizerType = MapTokenizer(catalogModel.TokenizerType),
                        Parameters = "{}"
                    };
                    context.ModelSeries.Add(modelSeries);
                    series.Add(seriesKey, modelSeries);
                    providerResult.Created.Series++;
                }

                Model model;
                if (matchingModels?.Count == 1)
                {
                    model = matchingModels[0];
                }
                else
                {
                    model = new Model
                    {
                        Name = modelName,
                        Description = catalogModel.Notes,
                        ModelCardUrl = providerCatalog.Configuration.ModelCardUrl,
                        Series = modelSeries,
                        SupportsVision = catalogModel.SupportsVision,
                        SupportsEmbeddings = catalogModel.SupportsEmbeddings,
                        SupportsChat = catalogModel.SupportsChat,
                        SupportsFunctionCalling = catalogModel.SupportsFunctionCalling,
                        SupportsStreaming = catalogModel.SupportsStreaming,
                        TokenizerType = MapTokenizer(catalogModel.TokenizerType),
                        MaxInputTokens = catalogModel.MaxInputTokens,
                        MaxOutputTokens = catalogModel.MaxOutputTokens,
                        IsActive = true,
                        ModelParameters = "{}"
                    };
                    context.Models.Add(model);
                    models[modelKey] = [model];
                    providerResult.Created.Models++;
                }

                ModelCost cost;
                if (matchingCosts?.Count == 1)
                {
                    cost = matchingCosts[0];
                }
                else
                {
                    var now = DateTime.UtcNow;
                    cost = new ModelCost
                    {
                        CostName = identifier,
                        Description = $"Standard pricing for {identifier}",
                        InputCostPerMillionTokens = catalogModel.InputPricePerMillion,
                        OutputCostPerMillionTokens = catalogModel.OutputPricePerMillion,
                        PricingModel = PricingModel.Standard,
                        ModelType = "Text",
                        IsActive = true,
                        EffectiveDate = now,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    context.ModelCosts.Add(cost);
                    costs[identifier] = [cost];
                    providerResult.Created.Costs++;
                }

                var association = new ModelProviderTypeAssociation
                {
                    Model = model,
                    Identifier = identifier,
                    Provider = (ProviderType)providerCatalog.Configuration.ProviderType,
                    IsEnabled = true,
                    IsPrimary = true,
                    MaxInputTokens = catalogModel.MaxInputTokens,
                    MaxOutputTokens = catalogModel.MaxOutputTokens,
                    ModelCost = cost
                };
                context.ModelProviderTypeAssociations.Add(association);
                identifiers.Add(identifierKey, association);
                providerResult.Created.Identifiers++;
            }

            result.Created.Add(providerResult.Created);
        }

        return result;
    }

    private static void AddConflict(
        BundledModelCatalogImportResult result,
        ProviderCatalogImportResult provider,
        string message)
    {
        result.Conflicts.Add(message);
        provider.Conflicts++;
    }

    private static string IdentifierKey(int? provider, string identifier) => $"{provider}:{identifier}";
    private static string SeriesKey(string author, string series) => $"{author}\u001f{series}";
    private static string ModelKey(string author, string series, string model) => $"{author}\u001f{series}\u001f{model}";

    private static TokenizerType MapTokenizer(string value) => value switch
    {
        "Cl100KBase" or "cl100k_base" => TokenizerType.Cl100KBase,
        "P50KBase" or "p50k_base" => TokenizerType.P50KBase,
        "P50KEdit" => TokenizerType.P50KEdit,
        "R50KBase" => TokenizerType.R50KBase,
        "O200KBase" or "o200k_base" => TokenizerType.O200KBase,
        "O200KHarmony" => TokenizerType.O200KHarmony,
        "Claude" => TokenizerType.Claude,
        "Claude3" => TokenizerType.Claude3,
        "Gemini" => TokenizerType.Gemini,
        "PaLM" => TokenizerType.PaLM,
        "LLaMA" or "Llama" => TokenizerType.LLaMA,
        "LLaMA2" => TokenizerType.LLaMA2,
        "LLaMA3" => TokenizerType.LLaMA3,
        "Mistral" => TokenizerType.Mistral,
        "Cohere" => TokenizerType.Cohere,
        "Kimi" => TokenizerType.Kimi,
        "Groq" => TokenizerType.Groq,
        "Cerebras" => TokenizerType.Cerebras,
        "MiniMax" => TokenizerType.MiniMax,
        "SentencePiece" or "T5" => TokenizerType.SentencePiece,
        "BPE" or "ByteLevelBPE" or "GPTNeoX" => TokenizerType.BPE,
        "WordPiece" => TokenizerType.WordPiece,
        "Tiktoken" or "tiktoken" => TokenizerType.Tiktoken,
        _ => TokenizerType.None
    };
}

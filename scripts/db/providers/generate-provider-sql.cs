#!/usr/bin/env -S dotnet run

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

// Parse command-line arguments
if (args.Length == 0)
{
    Console.WriteLine("❌ Error: Provider name required");
    Console.WriteLine();
    Console.WriteLine("Usage: dotnet script generate-provider-sql.cs <provider> [output-file]");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  dotnet script generate-provider-sql.cs cerebras");
    Console.WriteLine("  dotnet script generate-provider-sql.cs sambanova sambanova-models.sql");
    Console.WriteLine();
    Console.WriteLine("Available providers are defined in provider-config.json");
    return;
}

var providerName = args[0].ToLower();
var outputFilename = args.Length > 1 ? args[1] : $"{providerName}-models.sql";

Console.WriteLine($"=== Provider Models SQL Generator ===");
Console.WriteLine($"Provider: {providerName}");
Console.WriteLine($"Output file: {outputFilename}");
Console.WriteLine();

// Load provider configuration
ProviderConfig? providerConfig = null;
var configSearchPaths = new[]
{
    "scripts/db/providers/provider-config.json",  // From repo root
    "provider-config.json",                        // Same directory as script
    Path.Combine(AppContext.BaseDirectory, "provider-config.json")  // Script execution directory
};

string? configPath = null;
foreach (var path in configSearchPaths)
{
    if (File.Exists(path))
    {
        configPath = path;
        break;
    }
}

if (configPath == null)
{
    Console.WriteLine($"❌ Error: provider-config.json not found");
    Console.WriteLine($"   Searched locations:");
    foreach (var path in configSearchPaths)
    {
        Console.WriteLine($"   - {Path.GetFullPath(path)}");
    }
    return;
}

try
{
    var configContent = await File.ReadAllTextAsync(configPath);
    var configDoc = JsonDocument.Parse(configContent);

    if (!configDoc.RootElement.TryGetProperty(providerName, out var providerElement))
    {
        Console.WriteLine($"❌ Error: Provider '{providerName}' not found in provider-config.json");
        Console.WriteLine();
        Console.WriteLine("Available providers:");
        foreach (var prop in configDoc.RootElement.EnumerateObject())
        {
            Console.WriteLine($"  - {prop.Name}");
        }
        return;
    }

    providerConfig = new ProviderConfig
    {
        Name = providerName,
        ProviderType = providerElement.GetProperty("providerType").GetInt32(),
        WebsiteUrl = providerElement.GetProperty("websiteUrl").GetString() ?? "",
        ModelCardUrl = providerElement.GetProperty("modelCardUrl").GetString() ?? "",
        SupportsAudio = providerElement.GetProperty("supportsAudio").GetBoolean()
    };

    Console.WriteLine($"✅ Loaded configuration for {providerName}");
    Console.WriteLine($"   Provider Type: {providerConfig.ProviderType}");
    Console.WriteLine($"   Website: {providerConfig.WebsiteUrl}");
    Console.WriteLine();
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error loading provider configuration: {ex.Message}");
    return;
}

// Load models from JSON file
string? jsonPath = null;
var searchPaths = new[]
{
    $"scripts/db/providers/{providerName}-models.json",  // From repo root
    $"{providerName}-models.json",                        // Same directory as script
    Path.Combine(AppContext.BaseDirectory, $"{providerName}-models.json")  // Script execution directory
};

foreach (var path in searchPaths)
{
    if (File.Exists(path))
    {
        jsonPath = path;
        break;
    }
}

if (jsonPath == null)
{
    Console.WriteLine($"❌ Error: {providerName}-models.json not found");
    Console.WriteLine($"   Searched locations:");
    foreach (var path in searchPaths)
    {
        Console.WriteLine($"   - {Path.GetFullPath(path)}");
    }
    return;
}

var jsonContent = await File.ReadAllTextAsync(jsonPath);
var jsonDoc = JsonDocument.Parse(jsonContent);
var modelsNode = jsonDoc.RootElement.GetProperty("models");

var models = new List<ProviderModel>();

foreach (var modelProp in modelsNode.EnumerateObject())
{
    var modelId = modelProp.Name;
    var modelData = modelProp.Value;

    var model = new ProviderModel
    {
        ModelId = modelId,
        Name = modelData.GetProperty("name").GetString() ?? modelId,
        Family = modelData.GetProperty("family").GetString() ?? "Unknown",
        Series = modelData.GetProperty("series").GetString() ?? "Unknown Series",
        Owner = modelData.GetProperty("owner").GetString() ?? providerName,
        MaxInputTokens = modelData.GetProperty("maxInputTokens").GetInt32(),
        MaxOutputTokens = modelData.GetProperty("maxOutputTokens").GetInt32(),
        TokenizerType = modelData.GetProperty("tokenizerType").GetString() ?? "None",
        SupportsChat = modelData.GetProperty("supportsChat").GetBoolean(),
        SupportsStreaming = modelData.GetProperty("supportsStreaming").GetBoolean(),
        SupportsVision = modelData.GetProperty("supportsVision").GetBoolean(),
        SupportsFunctionCalling = modelData.GetProperty("supportsFunctionCalling").GetBoolean(),
        SupportsEmbeddings = modelData.GetProperty("supportsEmbeddings").GetBoolean(),
        SupportsAudio = modelData.TryGetProperty("supportsAudio", out var audio) ? audio.GetBoolean() : false,
        SupportsImageGeneration = GetBoolean(modelData, "supportsImageGeneration"),
        SupportsVideoGeneration = GetBoolean(modelData, "supportsVideoGeneration"),
        SupportsSpeechToText = GetBoolean(modelData, "supportsSpeechToText"),
        SupportsTextToSpeech = GetBoolean(modelData, "supportsTextToSpeech"),
        SupportsRerank = GetBoolean(modelData, "supportsRerank"),
        InputModalities = GetModalities(modelData, "inputModalities"),
        OutputModalities = GetModalities(modelData, "outputModalities"),
        CapabilitySource = GetCapabilitySource(modelData),
        CapabilitiesLastVerifiedAt = modelData.TryGetProperty("capabilitiesLastVerifiedAt", out var verified)
            && verified.ValueKind == JsonValueKind.String
            && verified.TryGetDateTime(out var verifiedAt) ? verifiedAt : null,
        InputPricePerMillion = modelData.GetProperty("inputPricePerMillion").GetDouble(),
        OutputPricePerMillion = modelData.GetProperty("outputPricePerMillion").GetDouble(),
        SpeedTokensPerSec = modelData.TryGetProperty("speedTokensPerSec", out var speed) && speed.ValueKind != JsonValueKind.Null ? speed.GetInt32() : (int?)null,
        Notes = modelData.TryGetProperty("notes", out var notes) ? notes.GetString() : null
    };

    models.Add(model);
}

Console.WriteLine($"✅ Loaded {models.Count} models from {jsonPath}");
Console.WriteLine();

// Generate SQL
await GenerateSQLOutput(models, outputFilename, providerConfig);

Console.WriteLine();
Console.WriteLine("=== Generation Complete ===");
Console.WriteLine($"✅ SQL written to: {outputFilename}");
Console.WriteLine($"   Total models: {models.Count}");
Console.WriteLine();
Console.WriteLine("To execute:");
Console.WriteLine($"  psql -h localhost -U conduit -d conduit_db < {outputFilename}");

// SQL Generation
static async Task GenerateSQLOutput(List<ProviderModel> models, string outputFilename, ProviderConfig config)
{
    var sql = new StringBuilder();

    sql.AppendLine($"-- Auto-generated SQL for {config.Name} models");
    sql.AppendLine($"-- Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
    sql.AppendLine($"-- Total Models: {models.Count}");
    sql.AppendLine($"-- Provider Type: {config.ProviderType}");
    sql.AppendLine();
    sql.AppendLine("-- NOTE: This script uses ON CONFLICT DO NOTHING to preserve manual changes.");
    sql.AppendLine("-- If you need to update existing records, remove the ON CONFLICT clauses.");
    sql.AppendLine();
    sql.AppendLine("BEGIN;");
    sql.AppendLine();

    foreach (var model in models)
    {
        var tokenizerTypeEnum = MapTokenizerTypeToEnum(model.TokenizerType);
        var standardParameters = "{}"; // Providers use standard OpenAI-compatible parameters
        var description = model.Notes != null ? $"'{EscapeSqlString(model.Notes)}'" : "NULL";
        var modelCardUrl = config.ModelCardUrl;
        var inputModalities = FormatJsonb(model.InputModalities ?? InferInputModalities(model));
        var outputModalities = FormatJsonb(model.OutputModalities ?? InferOutputModalities(model));
        var operationalCapabilities = FormatOperationalCapabilities(model);
        var verifiedAt = model.CapabilitiesLastVerifiedAt.HasValue
            ? $"'{model.CapabilitiesLastVerifiedAt.Value.ToUniversalTime():O}'::timestamptz"
            : "NULL";

        sql.AppendLine($"-- Model: {model.ModelId}");
        sql.AppendLine();

        // Step 1: Upsert ModelAuthor
        sql.AppendLine($"DO $$");
        sql.AppendLine($"DECLARE");
        sql.AppendLine($"  v_author_id INTEGER;");
        sql.AppendLine($"  v_series_id INTEGER;");
        sql.AppendLine($"  v_model_id INTEGER;");
        sql.AppendLine($"  v_cost_id INTEGER;");
        sql.AppendLine($"BEGIN");
        sql.AppendLine();

        // Insert or get author
        sql.AppendLine($"  -- Insert or get author");
        sql.AppendLine($"  INSERT INTO \"ModelAuthors\" (\"Name\", \"Description\", \"WebsiteUrl\")");
        sql.AppendLine($"  VALUES ('{EscapeSqlString(model.Owner)}', NULL, '{config.WebsiteUrl}')");
        sql.AppendLine($"  ON CONFLICT (\"Name\") DO NOTHING;");
        sql.AppendLine();
        sql.AppendLine($"  SELECT \"Id\" INTO v_author_id FROM \"ModelAuthors\" WHERE \"Name\" = '{EscapeSqlString(model.Owner)}';");
        sql.AppendLine();

        // Insert or get series
        sql.AppendLine($"  -- Insert or get series");
        sql.AppendLine($"  INSERT INTO \"ModelSeries\" (\"Name\", \"Description\", \"AuthorId\", \"TokenizerType\", \"Parameters\")");
        sql.AppendLine($"  VALUES ('{EscapeSqlString(model.Series)}', NULL, v_author_id, {tokenizerTypeEnum}, '{standardParameters}')");
        sql.AppendLine($"  ON CONFLICT (\"AuthorId\", \"Name\") DO NOTHING;");
        sql.AppendLine();
        sql.AppendLine($"  SELECT \"Id\" INTO v_series_id FROM \"ModelSeries\" WHERE \"AuthorId\" = v_author_id AND \"Name\" = '{EscapeSqlString(model.Series)}';");
        sql.AppendLine();

        // Insert or get model
        sql.AppendLine($"  -- Insert or get model");
        sql.AppendLine($"  SELECT \"Id\" INTO v_model_id FROM \"Models\" WHERE \"Name\" = '{EscapeSqlString(model.Name)}' AND \"ModelSeriesId\" = v_series_id;");
        sql.AppendLine();
        sql.AppendLine($"  IF v_model_id IS NULL THEN");
        sql.AppendLine($"    INSERT INTO \"Models\" (");
        sql.AppendLine($"      \"Name\", \"Version\", \"Description\", \"ModelCardUrl\", \"ModelSeriesId\",");
        sql.AppendLine($"      \"SupportsVision\", \"SupportsImageGeneration\", \"SupportsVideoGeneration\",");
        sql.AppendLine($"      \"SupportsEmbeddings\", \"SupportsChat\", \"SupportsFunctionCalling\", \"SupportsStreaming\",");
        sql.AppendLine($"      \"SupportsSpeechToText\", \"SupportsTextToSpeech\", \"SupportsRerank\",");
        sql.AppendLine($"      \"InputModalities\", \"OutputModalities\", \"CapabilitySource\", \"CapabilitiesLastVerifiedAt\",");
        sql.AppendLine($"      \"TokenizerType\", \"MaxInputTokens\", \"MaxOutputTokens\",");
        sql.AppendLine($"      \"IsActive\", \"Parameters\", \"CreatedAt\", \"UpdatedAt\"");
        sql.AppendLine($"    ) VALUES (");
        sql.AppendLine($"      '{EscapeSqlString(model.Name)}', NULL, {description}, '{modelCardUrl}', v_series_id,");
        sql.AppendLine($"      {FormatBool(model.SupportsVision)}, {FormatBool(model.SupportsImageGeneration)}, {FormatBool(model.SupportsVideoGeneration)},");
        sql.AppendLine($"      {FormatBool(model.SupportsEmbeddings)}, {FormatBool(model.SupportsChat)}, {FormatBool(model.SupportsFunctionCalling)}, {FormatBool(model.SupportsStreaming)},");
        sql.AppendLine($"      {FormatBool(model.SupportsSpeechToText)}, {FormatBool(model.SupportsTextToSpeech)}, {FormatBool(model.SupportsRerank)},");
        sql.AppendLine($"      {inputModalities}, {outputModalities}, {model.CapabilitySource}, {verifiedAt},");
        sql.AppendLine($"      {tokenizerTypeEnum}, {model.MaxInputTokens}, {model.MaxOutputTokens},");
        sql.AppendLine($"      true, '{standardParameters}', NOW(), NOW()");
        sql.AppendLine($"    ) RETURNING \"Id\" INTO v_model_id;");
        sql.AppendLine($"  ELSE");
        sql.AppendLine($"    -- Update existing model");
        sql.AppendLine($"    UPDATE \"Models\" SET");
        sql.AppendLine($"      \"Description\" = {description},");
        sql.AppendLine($"      \"MaxInputTokens\" = {model.MaxInputTokens},");
        sql.AppendLine($"      \"MaxOutputTokens\" = {model.MaxOutputTokens},");
        sql.AppendLine($"      \"SupportsVision\" = CASE WHEN \"CapabilitySource\" IN (0, 1, 3) THEN {FormatBool(model.SupportsVision)} ELSE \"SupportsVision\" END,");
        sql.AppendLine($"      \"SupportsImageGeneration\" = CASE WHEN \"CapabilitySource\" IN (0, 1, 3) THEN {FormatBool(model.SupportsImageGeneration)} ELSE \"SupportsImageGeneration\" END,");
        sql.AppendLine($"      \"SupportsVideoGeneration\" = CASE WHEN \"CapabilitySource\" IN (0, 1, 3) THEN {FormatBool(model.SupportsVideoGeneration)} ELSE \"SupportsVideoGeneration\" END,");
        sql.AppendLine($"      \"InputModalities\" = CASE WHEN \"CapabilitySource\" IN (0, 1, 3) THEN {inputModalities} ELSE \"InputModalities\" END,");
        sql.AppendLine($"      \"OutputModalities\" = CASE WHEN \"CapabilitySource\" IN (0, 1, 3) THEN {outputModalities} ELSE \"OutputModalities\" END,");
        sql.AppendLine($"      \"CapabilitySource\" = CASE WHEN \"CapabilitySource\" IN (0, 1, 3) THEN {model.CapabilitySource} ELSE \"CapabilitySource\" END,");
        sql.AppendLine($"      \"CapabilitiesLastVerifiedAt\" = CASE WHEN \"CapabilitySource\" IN (0, 1, 3) THEN {verifiedAt} ELSE \"CapabilitiesLastVerifiedAt\" END,");
        sql.AppendLine($"      \"UpdatedAt\" = NOW()");
        sql.AppendLine($"    WHERE \"Id\" = v_model_id;");
        sql.AppendLine($"  END IF;");
        sql.AppendLine();

        // Insert or update cost
        sql.AppendLine($"  -- Insert or update cost");
        sql.AppendLine($"  SELECT \"Id\" INTO v_cost_id FROM \"ModelCosts\" WHERE \"CostName\" = '{EscapeSqlString(model.ModelId)}';");
        sql.AppendLine();
        sql.AppendLine($"  IF v_cost_id IS NULL THEN");
        sql.AppendLine($"    INSERT INTO \"ModelCosts\" (");
        sql.AppendLine($"      \"CostName\", \"Description\", \"InputCostPerMillionTokens\", \"OutputCostPerMillionTokens\",");
        sql.AppendLine($"      \"PricingModel\", \"ModelType\", \"IsActive\", \"EffectiveDate\", \"Priority\",");
        sql.AppendLine($"      \"CreatedAt\", \"UpdatedAt\", \"SupportsBatchProcessing\"");
        sql.AppendLine($"    ) VALUES (");
        sql.AppendLine($"      '{EscapeSqlString(model.ModelId)}', 'Standard pricing for {EscapeSqlString(model.ModelId)}',");
        sql.AppendLine($"      {model.InputPricePerMillion}, {model.OutputPricePerMillion},");
        sql.AppendLine($"      0, 'Text', true, NOW(), 0, NOW(), NOW(), false");
        sql.AppendLine($"    ) RETURNING \"Id\" INTO v_cost_id;");
        sql.AppendLine($"  ELSE");
        sql.AppendLine($"    UPDATE \"ModelCosts\" SET");
        sql.AppendLine($"      \"InputCostPerMillionTokens\" = {model.InputPricePerMillion},");
        sql.AppendLine($"      \"OutputCostPerMillionTokens\" = {model.OutputPricePerMillion},");
        sql.AppendLine($"      \"UpdatedAt\" = NOW()");
        sql.AppendLine($"    WHERE \"Id\" = v_cost_id;");
        sql.AppendLine($"  END IF;");
        sql.AppendLine();

        // Insert or update model identifier
        sql.AppendLine($"  -- Insert or update model identifier");
        sql.AppendLine($"  INSERT INTO \"ModelIdentifiers\" (");
        sql.AppendLine($"    \"ModelId\", \"Identifier\", \"Provider\", \"IsEnabled\",");
        sql.AppendLine($"    \"MaxInputTokens\", \"MaxOutputTokens\", \"IsPrimary\", \"ModelCostId\",");
        sql.AppendLine($"    \"InputModalities\", \"OutputModalities\", \"OperationalCapabilities\", \"CapabilitySource\", \"CapabilitiesLastVerifiedAt\"");
        sql.AppendLine($"  ) VALUES (");
        sql.AppendLine($"    v_model_id, '{EscapeSqlString(model.ModelId)}', {config.ProviderType}, true,");
        sql.AppendLine($"    {model.MaxInputTokens}, {model.MaxOutputTokens}, true, v_cost_id,");
        sql.AppendLine($"    {inputModalities}, {outputModalities}, {operationalCapabilities}, {model.CapabilitySource}, {verifiedAt}");
        sql.AppendLine($"  )");
        sql.AppendLine($"  ON CONFLICT (\"Provider\", \"Identifier\") DO UPDATE SET");
        sql.AppendLine($"    \"ModelCostId\" = EXCLUDED.\"ModelCostId\",");
        sql.AppendLine($"    \"MaxInputTokens\" = EXCLUDED.\"MaxInputTokens\",");
        sql.AppendLine($"    \"MaxOutputTokens\" = EXCLUDED.\"MaxOutputTokens\",");
        sql.AppendLine($"    \"InputModalities\" = EXCLUDED.\"InputModalities\",");
        sql.AppendLine($"    \"OutputModalities\" = EXCLUDED.\"OutputModalities\",");
        sql.AppendLine($"    \"OperationalCapabilities\" = EXCLUDED.\"OperationalCapabilities\",");
        sql.AppendLine($"    \"CapabilitySource\" = EXCLUDED.\"CapabilitySource\",");
        sql.AppendLine($"    \"CapabilitiesLastVerifiedAt\" = EXCLUDED.\"CapabilitiesLastVerifiedAt\";");
        sql.AppendLine();
        sql.AppendLine($"END $$;");
        sql.AppendLine();
    }

    sql.AppendLine("COMMIT;");
    sql.AppendLine();
    sql.AppendLine($"-- Successfully generated SQL for {models.Count} {config.Name} models");

    // Write to file
    await File.WriteAllTextAsync(outputFilename, sql.ToString());
}

static int MapTokenizerTypeToEnum(string tokenizerType)
{
    return tokenizerType switch
    {
        "None" => 0,
        "Cl100KBase" or "cl100k_base" => 1,
        "P50KBase" or "p50k_base" => 2,
        "P50KEdit" => 3,
        "R50KBase" => 4,
        "O200KBase" or "o200k_base" or "O200KHarmony" => 5,
        "Claude" => 6,
        "Claude3" => 7,
        "Gemini" or "SentencePiece" => 8,
        "PaLM" => 9,
        "LLaMA" or "Llama" => 10,
        "LLaMA2" => 11,
        "LLaMA3" => 12,
        "Mistral" => 13,
        "Cohere" => 14,
        "Kimi" => 16,
        "Groq" => 17,
        "Cerebras" => 18,
        "MiniMax" => 19,
        "BPE" or "ByteLevelBPE" or "GPTNeoX" => 21,
        "WordPiece" => 22,
        "Tiktoken" or "tiktoken" => 23,
        "T5" => 8,
        _ => 0
    };
}

static string FormatBool(bool value) => value ? "true" : "false";

static bool GetBoolean(JsonElement element, string propertyName) =>
    element.TryGetProperty(propertyName, out var property) &&
    property.ValueKind is JsonValueKind.True or JsonValueKind.False &&
    property.GetBoolean();

static IReadOnlyList<string>? GetModalities(JsonElement element, string propertyName) =>
    element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array
        ? property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToArray()
        : null;

static int GetCapabilitySource(JsonElement element)
{
    if (!element.TryGetProperty("capabilitySource", out var source))
        return 2; // Curated
    if (source.ValueKind == JsonValueKind.Number)
        return source.GetInt32();
    return source.GetString() switch
    {
        "Unknown" => 0,
        "LegacyInferred" => 1,
        "ProviderApi" => 3,
        "Manual" => 4,
        _ => 2
    };
}

static string FormatJsonb(IEnumerable<string> values)
{
    var json = "[" + string.Join(",", values.Select(value =>
        $"\"{JsonEncodedText.Encode(value)}\"")) + "]";
    return $"'{EscapeSqlString(json)}'::jsonb";
}

static string FormatOperationalCapabilities(ProviderModel model)
{
    var json = "{" + string.Join(",", new[]
    {
        $"\"supportsChat\":{FormatBool(model.SupportsChat)}",
        $"\"supportsStreaming\":{FormatBool(model.SupportsStreaming)}",
        $"\"supportsVision\":{FormatBool(model.SupportsVision)}",
        $"\"supportsImageGeneration\":{FormatBool(model.SupportsImageGeneration)}",
        $"\"supportsVideoGeneration\":{FormatBool(model.SupportsVideoGeneration)}",
        $"\"supportsEmbeddings\":{FormatBool(model.SupportsEmbeddings)}",
        $"\"supportsFunctionCalling\":{FormatBool(model.SupportsFunctionCalling)}",
        $"\"supportsSpeechToText\":{FormatBool(model.SupportsSpeechToText)}",
        $"\"supportsTextToSpeech\":{FormatBool(model.SupportsTextToSpeech)}",
        $"\"supportsRerank\":{FormatBool(model.SupportsRerank)}"
    }) + "}";
    return $"'{json}'::jsonb";
}

static IReadOnlyList<string> InferInputModalities(ProviderModel model)
{
    var values = new List<string>();
    if (model.SupportsChat || model.SupportsEmbeddings || model.SupportsImageGeneration ||
        model.SupportsVideoGeneration || model.SupportsTextToSpeech || model.SupportsRerank)
        values.Add("text");
    if (model.SupportsVision) values.Add("image");
    if (model.SupportsSpeechToText || model.SupportsAudio) values.Add("audio");
    return values;
}

static IReadOnlyList<string> InferOutputModalities(ProviderModel model)
{
    var values = new List<string>();
    if (model.SupportsChat || model.SupportsSpeechToText || model.SupportsRerank) values.Add("text");
    if (model.SupportsImageGeneration) values.Add("image");
    if (model.SupportsVideoGeneration) values.Add("video");
    if (model.SupportsTextToSpeech) values.Add("audio");
    return values;
}

static string EscapeSqlString(string value)
{
    return value.Replace("'", "''");
}

// Data models
record ProviderConfig
{
    public string Name { get; init; } = "";
    public int ProviderType { get; init; }
    public string WebsiteUrl { get; init; } = "";
    public string ModelCardUrl { get; init; } = "";
    public bool SupportsAudio { get; init; }
}

record ProviderModel
{
    public string ModelId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Family { get; init; } = "";
    public string Series { get; init; } = "";
    public string Owner { get; init; } = "";
    public int MaxInputTokens { get; init; }
    public int MaxOutputTokens { get; init; }
    public string TokenizerType { get; init; } = "None";
    public bool SupportsChat { get; init; }
    public bool SupportsStreaming { get; init; }
    public bool SupportsVision { get; init; }
    public bool SupportsFunctionCalling { get; init; }
    public bool SupportsEmbeddings { get; init; }
    public bool SupportsAudio { get; init; }
    public bool SupportsImageGeneration { get; init; }
    public bool SupportsVideoGeneration { get; init; }
    public bool SupportsSpeechToText { get; init; }
    public bool SupportsTextToSpeech { get; init; }
    public bool SupportsRerank { get; init; }
    public IReadOnlyList<string>? InputModalities { get; init; }
    public IReadOnlyList<string>? OutputModalities { get; init; }
    public int CapabilitySource { get; init; } = 2;
    public DateTime? CapabilitiesLastVerifiedAt { get; init; }
    public double InputPricePerMillion { get; init; }
    public double OutputPricePerMillion { get; init; }
    public int? SpeedTokensPerSec { get; init; }
    public string? Notes { get; init; }
}

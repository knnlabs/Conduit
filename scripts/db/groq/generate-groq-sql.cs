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
var outputFilename = args.Length > 0 ? args[0] : "groq-models.sql";

Console.WriteLine("=== Groq Models SQL Generator ===");
Console.WriteLine($"Output file: {outputFilename}");
Console.WriteLine();

// Load models from JSON file
// Try multiple locations to find groq-models.json
string? jsonPath = null;
var searchPaths = new[]
{
    "scripts/db/groq/groq-models.json",  // From repo root
    "groq-models.json",                   // Same directory as script
    Path.Combine(AppContext.BaseDirectory, "groq-models.json")  // Script execution directory
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
    Console.WriteLine($"❌ Error: groq-models.json not found");
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

var models = new List<GroqModel>();

foreach (var modelProp in modelsNode.EnumerateObject())
{
    var modelId = modelProp.Name;
    var modelData = modelProp.Value;

    var model = new GroqModel
    {
        ModelId = modelId,
        Name = modelData.GetProperty("name").GetString() ?? modelId,
        Family = modelData.GetProperty("family").GetString() ?? "Unknown",
        Series = modelData.GetProperty("series").GetString() ?? "Unknown Series",
        Owner = modelData.GetProperty("owner").GetString() ?? "groq",
        MaxInputTokens = modelData.GetProperty("maxInputTokens").GetInt32(),
        MaxOutputTokens = modelData.GetProperty("maxOutputTokens").GetInt32(),
        TokenizerType = modelData.GetProperty("tokenizerType").GetString() ?? "None",
        SupportsChat = modelData.GetProperty("supportsChat").GetBoolean(),
        SupportsStreaming = modelData.GetProperty("supportsStreaming").GetBoolean(),
        SupportsVision = modelData.GetProperty("supportsVision").GetBoolean(),
        SupportsFunctionCalling = modelData.GetProperty("supportsFunctionCalling").GetBoolean(),
        SupportsEmbeddings = modelData.GetProperty("supportsEmbeddings").GetBoolean(),
        InputPricePerMillion = modelData.GetProperty("inputPricePerMillion").GetDouble(),
        OutputPricePerMillion = modelData.GetProperty("outputPricePerMillion").GetDouble(),
        SpeedTokensPerSec = modelData.TryGetProperty("speedTokensPerSec", out var speed) && speed.ValueKind != JsonValueKind.Null ? speed.GetInt32() : (int?)null,
        Notes = modelData.TryGetProperty("notes", out var notes) ? notes.GetString() : null
    };

    models.Add(model);
}

Console.WriteLine($"Loaded {models.Count} models from {jsonPath}");
Console.WriteLine();

// Generate SQL
await GenerateSQLOutput(models, outputFilename);

Console.WriteLine();
Console.WriteLine("=== Generation Complete ===");
Console.WriteLine($"✅ SQL written to: {outputFilename}");
Console.WriteLine($"   Total models: {models.Count}");
Console.WriteLine();
Console.WriteLine("To execute:");
Console.WriteLine($"  psql -h localhost -U conduit -d conduit_db < {outputFilename}");

// SQL Generation
static async Task GenerateSQLOutput(List<GroqModel> models, string outputFilename)
{
    var sql = new StringBuilder();

    sql.AppendLine("-- Auto-generated SQL for Groq models");
    sql.AppendLine($"-- Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
    sql.AppendLine($"-- Total Models: {models.Count}");
    sql.AppendLine();
    sql.AppendLine("-- NOTE: This script uses ON CONFLICT DO NOTHING to preserve manual changes.");
    sql.AppendLine("-- If you need to update existing records, remove the ON CONFLICT clauses.");
    sql.AppendLine();
    sql.AppendLine("BEGIN;");
    sql.AppendLine();

    foreach (var model in models)
    {
        var tokenizerTypeEnum = MapTokenizerTypeToEnum(model.TokenizerType);
        var standardParameters = "{}"; // Groq uses standard OpenAI-compatible parameters
        var description = model.Notes != null ? $"'{EscapeSqlString(model.Notes)}'" : "NULL";

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
        sql.AppendLine($"  VALUES ('{EscapeSqlString(model.Owner)}', NULL, 'https://groq.com')");
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
        sql.AppendLine($"      \"TokenizerType\", \"MaxInputTokens\", \"MaxOutputTokens\",");
        sql.AppendLine($"      \"IsActive\", \"Parameters\", \"CreatedAt\", \"UpdatedAt\"");
        sql.AppendLine($"    ) VALUES (");
        sql.AppendLine($"      '{EscapeSqlString(model.Name)}', NULL, {description}, 'https://console.groq.com/docs/models', v_series_id,");
        sql.AppendLine($"      {FormatBool(model.SupportsVision)}, false, false,");
        sql.AppendLine($"      {FormatBool(model.SupportsEmbeddings)}, {FormatBool(model.SupportsChat)}, {FormatBool(model.SupportsFunctionCalling)}, {FormatBool(model.SupportsStreaming)},");
        sql.AppendLine($"      {tokenizerTypeEnum}, {model.MaxInputTokens}, {model.MaxOutputTokens},");
        sql.AppendLine($"      true, '{standardParameters}', NOW(), NOW()");
        sql.AppendLine($"    ) RETURNING \"Id\" INTO v_model_id;");
        sql.AppendLine($"  ELSE");
        sql.AppendLine($"    -- Update existing model");
        sql.AppendLine($"    UPDATE \"Models\" SET");
        sql.AppendLine($"      \"Description\" = {description},");
        sql.AppendLine($"      \"MaxInputTokens\" = {model.MaxInputTokens},");
        sql.AppendLine($"      \"MaxOutputTokens\" = {model.MaxOutputTokens},");
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
        sql.AppendLine($"    \"MaxInputTokens\", \"MaxOutputTokens\", \"IsPrimary\", \"ModelCostId\"");
        sql.AppendLine($"  ) VALUES (");
        sql.AppendLine($"    v_model_id, '{EscapeSqlString(model.ModelId)}', 2, true,");
        sql.AppendLine($"    {model.MaxInputTokens}, {model.MaxOutputTokens}, true, v_cost_id");
        sql.AppendLine($"  )");
        sql.AppendLine($"  ON CONFLICT (\"Provider\", \"Identifier\") DO UPDATE SET");
        sql.AppendLine($"    \"ModelCostId\" = EXCLUDED.\"ModelCostId\",");
        sql.AppendLine($"    \"MaxInputTokens\" = EXCLUDED.\"MaxInputTokens\",");
        sql.AppendLine($"    \"MaxOutputTokens\" = EXCLUDED.\"MaxOutputTokens\";");
        sql.AppendLine();
        sql.AppendLine($"END $$;");
        sql.AppendLine();
    }

    sql.AppendLine("COMMIT;");
    sql.AppendLine();
    sql.AppendLine($"-- Successfully generated SQL for {models.Count} Groq models");

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

static string EscapeSqlString(string value)
{
    return value.Replace("'", "''");
}

// Data model
record GroqModel
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
    public double InputPricePerMillion { get; init; }
    public double OutputPricePerMillion { get; init; }
    public int? SpeedTokensPerSec { get; init; }
    public string? Notes { get; init; }
}

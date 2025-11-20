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
var outputFilename = args.Length > 0 ? args[0] : "functions-seed.sql";

Console.WriteLine("=== Function Configurations SQL Generator ===");
Console.WriteLine($"Output file: {outputFilename}");
Console.WriteLine();

// Load configurations from JSON file
string? jsonPath = null;
var searchPaths = new[]
{
    "scripts/db/functions/function-configurations.json",  // From repo root
    "function-configurations.json",                        // Same directory as script
    Path.Combine(AppContext.BaseDirectory, "function-configurations.json")  // Script execution directory
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
    Console.WriteLine($"❌ Error: function-configurations.json not found");
    Console.WriteLine($"   Searched locations:");
    foreach (var path in searchPaths)
    {
        Console.WriteLine($"   - {Path.GetFullPath(path)}");
    }
    return;
}

var jsonContent = await File.ReadAllTextAsync(jsonPath);
var jsonDoc = JsonDocument.Parse(jsonContent);
var configurationsNode = jsonDoc.RootElement.GetProperty("configurations");

var configurations = new List<FunctionConfiguration>();

foreach (var config in configurationsNode.EnumerateArray())
{
    var configuration = new FunctionConfiguration
    {
        ProviderType = config.GetProperty("providerType").GetString() ?? "Unknown",
        ProviderTypeId = config.GetProperty("providerTypeId").GetInt32(),
        ConfigurationName = config.GetProperty("configurationName").GetString() ?? "Unknown",
        Purpose = config.GetProperty("purpose").GetString() ?? "Unknown",
        PurposeId = config.GetProperty("purposeId").GetInt32(),
        Description = config.TryGetProperty("description", out var desc) ? desc.GetString() : null,
        DefaultExecutionMode = config.GetProperty("defaultExecutionMode").GetString() ?? "Synchronous",
        DefaultExecutionModeId = config.GetProperty("defaultExecutionModeId").GetInt32(),
        BaseUrl = config.TryGetProperty("baseUrl", out var baseUrl) && baseUrl.ValueKind != JsonValueKind.Null
            ? baseUrl.GetString()
            : null,
        IsEnabled = config.GetProperty("isEnabled").GetBoolean(),
        TimeoutSeconds = config.TryGetProperty("timeoutSeconds", out var timeout) ? timeout.GetInt32() : (int?)null,
        MaxRetries = config.TryGetProperty("maxRetries", out var retries) ? retries.GetInt32() : (int?)null,
        ProviderSettings = config.TryGetProperty("providerSettings", out var settings)
            ? settings.GetRawText()
            : null,
        ParameterSchema = config.TryGetProperty("parameterSchema", out var schema)
            ? schema.GetRawText()
            : null
    };

    configurations.Add(configuration);
}

Console.WriteLine($"Loaded {configurations.Count} function configurations from {jsonPath}");
Console.WriteLine();

// Group by provider for reporting
var byProvider = configurations.GroupBy(c => c.ProviderType).OrderBy(g => g.Key);
foreach (var group in byProvider)
{
    Console.WriteLine($"  {group.Key}: {group.Count()} configuration(s)");
    foreach (var config in group)
    {
        Console.WriteLine($"    - {config.ConfigurationName} ({config.Purpose})");
    }
}
Console.WriteLine();

// Generate SQL
await GenerateSQLOutput(configurations, outputFilename);

Console.WriteLine();
Console.WriteLine("=== Generation Complete ===");
Console.WriteLine($"✅ SQL written to: {outputFilename}");
Console.WriteLine($"   Total configurations: {configurations.Count}");
Console.WriteLine();
Console.WriteLine("To execute:");
Console.WriteLine($"  psql -h localhost -U conduit -d conduit_db < {outputFilename}");

// SQL Generation
static async Task GenerateSQLOutput(List<FunctionConfiguration> configurations, string outputFilename)
{
    var sql = new StringBuilder();

    sql.AppendLine("-- Auto-generated SQL for Function Configurations");
    sql.AppendLine($"-- Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
    sql.AppendLine($"-- Total Configurations: {configurations.Count}");
    sql.AppendLine();
    sql.AppendLine("-- Providers:");
    sql.AppendLine("--   Exa (1): Neural Search, Keyword Search, Content Retrieval");
    sql.AppendLine("--   Tavily (4): General Search, News Search, Finance Search, Answer Generation");
    sql.AppendLine("--   Perplexity (2): Sonar Search, Sonar Pro Search, Sonar Reasoning");
    sql.AppendLine();
    sql.AppendLine("-- NOTE: This script uses upsert logic to preserve manual changes.");
    sql.AppendLine("-- Existing configurations will be updated with new settings.");
    sql.AppendLine();
    sql.AppendLine("BEGIN;");
    sql.AppendLine();

    foreach (var config in configurations)
    {
        sql.AppendLine($"-- Configuration: {config.ConfigurationName}");
        sql.AppendLine($"-- Provider: {config.ProviderType} (Type: {config.ProviderTypeId})");
        sql.AppendLine($"-- Purpose: {config.Purpose} ({config.PurposeId})");
        sql.AppendLine();

        sql.AppendLine($"DO $$");
        sql.AppendLine($"DECLARE");
        sql.AppendLine($"  v_config_id INTEGER;");
        sql.AppendLine($"BEGIN");
        sql.AppendLine();

        // Check if configuration exists
        sql.AppendLine($"  -- Check if configuration exists");
        sql.AppendLine($"  SELECT \"Id\" INTO v_config_id");
        sql.AppendLine($"  FROM \"FunctionConfigurations\"");
        sql.AppendLine($"  WHERE \"ConfigurationName\" = '{EscapeSqlString(config.ConfigurationName)}'");
        sql.AppendLine($"    AND \"ProviderType\" = {config.ProviderTypeId};");
        sql.AppendLine();

        // Prepare nullable fields
        var description = config.Description != null
            ? $"'{EscapeSqlString(config.Description)}'"
            : "NULL";
        var baseUrl = config.BaseUrl != null
            ? $"'{EscapeSqlString(config.BaseUrl)}'"
            : "NULL";
        var timeoutSeconds = config.TimeoutSeconds?.ToString() ?? "NULL";
        var maxRetries = config.MaxRetries?.ToString() ?? "NULL";
        var providerSettings = config.ProviderSettings != null
            ? $"'{EscapeSqlString(config.ProviderSettings)}'"
            : "NULL";
        var parameterSchema = config.ParameterSchema != null
            ? $"'{EscapeSqlString(config.ParameterSchema)}'"
            : "NULL";

        // Insert or update
        sql.AppendLine($"  IF v_config_id IS NULL THEN");
        sql.AppendLine($"    -- Insert new configuration");
        sql.AppendLine($"    INSERT INTO \"FunctionConfigurations\" (");
        sql.AppendLine($"      \"ProviderType\", \"ConfigurationName\", \"Purpose\", \"DefaultExecutionMode\",");
        sql.AppendLine($"      \"IsEnabled\", \"BaseUrl\", \"TimeoutSeconds\", \"MaxRetries\",");
        sql.AppendLine($"      \"ProviderSettings\", \"ParameterSchema\", \"Description\",");
        sql.AppendLine($"      \"CreatedAt\", \"UpdatedAt\"");
        sql.AppendLine($"    ) VALUES (");
        sql.AppendLine($"      {config.ProviderTypeId}, '{EscapeSqlString(config.ConfigurationName)}', {config.PurposeId}, {config.DefaultExecutionModeId},");
        sql.AppendLine($"      {FormatBool(config.IsEnabled)}, {baseUrl}, {timeoutSeconds}, {maxRetries},");
        sql.AppendLine($"      {providerSettings}::jsonb, {parameterSchema}::jsonb, {description},");
        sql.AppendLine($"      NOW(), NOW()");
        sql.AppendLine($"    );");
        sql.AppendLine($"  ELSE");
        sql.AppendLine($"    -- Update existing configuration");
        sql.AppendLine($"    UPDATE \"FunctionConfigurations\" SET");
        sql.AppendLine($"      \"Purpose\" = {config.PurposeId},");
        sql.AppendLine($"      \"DefaultExecutionMode\" = {config.DefaultExecutionModeId},");
        sql.AppendLine($"      \"IsEnabled\" = {FormatBool(config.IsEnabled)},");
        sql.AppendLine($"      \"BaseUrl\" = {baseUrl},");
        sql.AppendLine($"      \"TimeoutSeconds\" = {timeoutSeconds},");
        sql.AppendLine($"      \"MaxRetries\" = {maxRetries},");
        sql.AppendLine($"      \"ProviderSettings\" = {providerSettings}::jsonb,");
        sql.AppendLine($"      \"ParameterSchema\" = {parameterSchema}::jsonb,");
        sql.AppendLine($"      \"Description\" = {description},");
        sql.AppendLine($"      \"UpdatedAt\" = NOW()");
        sql.AppendLine($"    WHERE \"Id\" = v_config_id;");
        sql.AppendLine($"  END IF;");
        sql.AppendLine();

        sql.AppendLine($"END $$;");
        sql.AppendLine();
    }

    sql.AppendLine("COMMIT;");
    sql.AppendLine();
    sql.AppendLine($"-- Successfully generated SQL for {configurations.Count} function configurations");

    // Write to file
    await File.WriteAllTextAsync(outputFilename, sql.ToString());
}

static string FormatBool(bool value) => value ? "true" : "false";

static string EscapeSqlString(string value)
{
    return value.Replace("'", "''").Replace("\\", "\\\\");
}

// Data model
record FunctionConfiguration
{
    public string ProviderType { get; init; } = "";
    public int ProviderTypeId { get; init; }
    public string ConfigurationName { get; init; } = "";
    public string Purpose { get; init; } = "";
    public int PurposeId { get; init; }
    public string? Description { get; init; }
    public string DefaultExecutionMode { get; init; } = "";
    public int DefaultExecutionModeId { get; init; }
    public string? BaseUrl { get; init; }
    public bool IsEnabled { get; init; }
    public int? TimeoutSeconds { get; init; }
    public int? MaxRetries { get; init; }
    public string? ProviderSettings { get; init; }
    public string? ParameterSchema { get; init; }
}

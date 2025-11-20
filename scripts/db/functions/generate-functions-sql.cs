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
    // Parse cost configuration if present
    CostConfiguration? costConfig = null;
    if (config.TryGetProperty("costConfiguration", out var costConfigElement))
    {
        costConfig = new CostConfiguration
        {
            CostName = costConfigElement.GetProperty("costName").GetString() ?? "",
            PricingModel = costConfigElement.GetProperty("pricingModel").GetString() ?? "",
            PricingModelId = costConfigElement.GetProperty("pricingModelId").GetInt32(),
            CostPerExecution = costConfigElement.TryGetProperty("costPerExecution", out var costPerExec)
                ? costPerExec.GetDecimal() : (decimal?)null,
            CostPerResult = costConfigElement.TryGetProperty("costPerResult", out var costPerRes)
                ? costPerRes.GetDecimal() : (decimal?)null,
            CostPerToken = costConfigElement.TryGetProperty("costPerToken", out var costPerTok)
                ? costPerTok.GetDecimal() : (decimal?)null,
            CostPerMinute = costConfigElement.TryGetProperty("costPerMinute", out var costPerMin)
                ? costPerMin.GetDecimal() : (decimal?)null,
            TieredPricing = costConfigElement.TryGetProperty("tieredPricing", out var tiered)
                ? tiered.GetRawText()
                : null,
            PricingConfiguration = costConfigElement.TryGetProperty("pricingConfiguration", out var pricingConfig)
                ? pricingConfig.GetRawText()
                : null,
            IsActive = costConfigElement.TryGetProperty("isActive", out var isActive)
                ? isActive.GetBoolean() : true,
            Priority = costConfigElement.TryGetProperty("priority", out var priority)
                ? priority.GetInt32() : 1
        };
    }

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
            : null,
        CostConfiguration = costConfig
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

    sql.AppendLine("-- Auto-generated SQL for Function Configurations and Cost Management");
    sql.AppendLine($"-- Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
    sql.AppendLine($"-- Total Configurations: {configurations.Count}");
    var configurationsWithCost = configurations.Count(c => c.CostConfiguration != null);
    sql.AppendLine($"-- Configurations with Cost Data: {configurationsWithCost}");
    sql.AppendLine();
    sql.AppendLine("-- Providers:");
    sql.AppendLine("--   Exa (1): Neural Search, Keyword Search, Content Retrieval");
    sql.AppendLine("--   Tavily (4): General Search, News Search, Finance Search, Answer Generation");
    sql.AppendLine("--   Perplexity (3): Sonar Search, Sonar Pro Search, Sonar Reasoning");
    sql.AppendLine();
    sql.AppendLine("-- Pricing Models:");
    sql.AppendLine("--   FlatRate (1): Fixed cost per execution");
    sql.AppendLine("--   PerResult (2): Cost per result/page returned");
    sql.AppendLine("--   Hybrid (6): Combined request fee + token costs");
    sql.AppendLine();
    sql.AppendLine("-- NOTE: This script uses upsert logic to preserve manual changes.");
    sql.AppendLine("-- Existing configurations and costs will be updated with new settings.");
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

        // Generate cost configuration if present
        if (config.CostConfiguration != null)
        {
            sql.AppendLine($"-- Cost Configuration: {config.CostConfiguration.CostName}");
            sql.AppendLine($"-- Pricing Model: {config.CostConfiguration.PricingModel} ({config.CostConfiguration.PricingModelId})");
            sql.AppendLine();

            sql.AppendLine($"DO $$");
            sql.AppendLine($"DECLARE");
            sql.AppendLine($"  v_cost_id INTEGER;");
            sql.AppendLine($"  v_function_config_id INTEGER;");
            sql.AppendLine($"BEGIN");
            sql.AppendLine();

            // Get the function configuration ID
            sql.AppendLine($"  -- Get function configuration ID");
            sql.AppendLine($"  SELECT \"Id\" INTO v_function_config_id");
            sql.AppendLine($"  FROM \"FunctionConfigurations\"");
            sql.AppendLine($"  WHERE \"ConfigurationName\" = '{EscapeSqlString(config.ConfigurationName)}'");
            sql.AppendLine($"    AND \"ProviderType\" = {config.ProviderTypeId};");
            sql.AppendLine();

            // Check if cost configuration exists
            sql.AppendLine($"  -- Check if cost configuration exists");
            sql.AppendLine($"  SELECT \"Id\" INTO v_cost_id");
            sql.AppendLine($"  FROM \"FunctionCosts\"");
            sql.AppendLine($"  WHERE \"CostName\" = '{EscapeSqlString(config.CostConfiguration.CostName)}';");
            sql.AppendLine();

            // Prepare nullable cost fields
            var costPerExecution = config.CostConfiguration.CostPerExecution.HasValue
                ? config.CostConfiguration.CostPerExecution.Value.ToString("F8")
                : "NULL";
            var costPerResult = config.CostConfiguration.CostPerResult.HasValue
                ? config.CostConfiguration.CostPerResult.Value.ToString("F8")
                : "NULL";
            var costPerToken = config.CostConfiguration.CostPerToken.HasValue
                ? config.CostConfiguration.CostPerToken.Value.ToString("F8")
                : "NULL";
            var costPerMinute = config.CostConfiguration.CostPerMinute.HasValue
                ? config.CostConfiguration.CostPerMinute.Value.ToString("F8")
                : "NULL";
            var tieredPricing = config.CostConfiguration.TieredPricing != null
                ? $"'{EscapeSqlString(config.CostConfiguration.TieredPricing)}'"
                : "NULL";
            var pricingConfiguration = config.CostConfiguration.PricingConfiguration != null
                ? $"'{EscapeSqlString(config.CostConfiguration.PricingConfiguration)}'"
                : "NULL";

            // Insert or update cost configuration
            sql.AppendLine($"  IF v_cost_id IS NULL THEN");
            sql.AppendLine($"    -- Insert new cost configuration");
            sql.AppendLine($"    INSERT INTO \"FunctionCosts\" (");
            sql.AppendLine($"      \"CostName\", \"PricingModel\", \"CostPerExecution\", \"CostPerResult\",");
            sql.AppendLine($"      \"CostPerToken\", \"CostPerMinute\", \"TieredPricing\", \"PricingConfiguration\",");
            sql.AppendLine($"      \"IsActive\", \"EffectiveDate\", \"Priority\", \"CreatedAt\", \"UpdatedAt\"");
            sql.AppendLine($"    ) VALUES (");
            sql.AppendLine($"      '{EscapeSqlString(config.CostConfiguration.CostName)}', {config.CostConfiguration.PricingModelId},");
            sql.AppendLine($"      {costPerExecution}, {costPerResult}, {costPerToken}, {costPerMinute},");
            sql.AppendLine($"      {tieredPricing}::jsonb, {pricingConfiguration}::jsonb,");
            sql.AppendLine($"      {FormatBool(config.CostConfiguration.IsActive)}, NOW(), {config.CostConfiguration.Priority},");
            sql.AppendLine($"      NOW(), NOW()");
            sql.AppendLine($"    )");
            sql.AppendLine($"    RETURNING \"Id\" INTO v_cost_id;");
            sql.AppendLine($"  ELSE");
            sql.AppendLine($"    -- Update existing cost configuration");
            sql.AppendLine($"    UPDATE \"FunctionCosts\" SET");
            sql.AppendLine($"      \"PricingModel\" = {config.CostConfiguration.PricingModelId},");
            sql.AppendLine($"      \"CostPerExecution\" = {costPerExecution},");
            sql.AppendLine($"      \"CostPerResult\" = {costPerResult},");
            sql.AppendLine($"      \"CostPerToken\" = {costPerToken},");
            sql.AppendLine($"      \"CostPerMinute\" = {costPerMinute},");
            sql.AppendLine($"      \"TieredPricing\" = {tieredPricing}::jsonb,");
            sql.AppendLine($"      \"PricingConfiguration\" = {pricingConfiguration}::jsonb,");
            sql.AppendLine($"      \"IsActive\" = {FormatBool(config.CostConfiguration.IsActive)},");
            sql.AppendLine($"      \"Priority\" = {config.CostConfiguration.Priority},");
            sql.AppendLine($"      \"UpdatedAt\" = NOW()");
            sql.AppendLine($"    WHERE \"Id\" = v_cost_id;");
            sql.AppendLine($"  END IF;");
            sql.AppendLine();

            // Create or update function cost mapping
            sql.AppendLine($"  -- Create/update function cost mapping");
            sql.AppendLine($"  IF NOT EXISTS (");
            sql.AppendLine($"    SELECT 1 FROM \"FunctionCostMappings\"");
            sql.AppendLine($"    WHERE \"FunctionConfigurationId\" = v_function_config_id");
            sql.AppendLine($"      AND \"FunctionCostId\" = v_cost_id");
            sql.AppendLine($"  ) THEN");
            sql.AppendLine($"    -- Deactivate any existing mappings for this configuration");
            sql.AppendLine($"    UPDATE \"FunctionCostMappings\"");
            sql.AppendLine($"    SET \"IsActive\" = false");
            sql.AppendLine($"    WHERE \"FunctionConfigurationId\" = v_function_config_id;");
            sql.AppendLine();
            sql.AppendLine($"    -- Insert new mapping");
            sql.AppendLine($"    INSERT INTO \"FunctionCostMappings\" (");
            sql.AppendLine($"      \"FunctionConfigurationId\", \"FunctionCostId\", \"IsActive\", \"CreatedAt\"");
            sql.AppendLine($"    ) VALUES (");
            sql.AppendLine($"      v_function_config_id, v_cost_id, true, NOW()");
            sql.AppendLine($"    );");
            sql.AppendLine($"  END IF;");
            sql.AppendLine();

            sql.AppendLine($"END $$;");
            sql.AppendLine();
        }
    }

    sql.AppendLine("COMMIT;");
    sql.AppendLine();
    sql.AppendLine($"-- Successfully generated SQL for {configurations.Count} function configurations");
    sql.AppendLine($"-- Created {configurationsWithCost} cost configurations and mappings");

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
    public CostConfiguration? CostConfiguration { get; init; }
}

record CostConfiguration
{
    public string CostName { get; init; } = "";
    public string PricingModel { get; init; } = "";
    public int PricingModelId { get; init; }
    public decimal? CostPerExecution { get; init; }
    public decimal? CostPerResult { get; init; }
    public decimal? CostPerToken { get; init; }
    public decimal? CostPerMinute { get; init; }
    public string? TieredPricing { get; init; }
    public string? PricingConfiguration { get; init; }
    public bool IsActive { get; init; }
    public int Priority { get; init; }
}

using ConduitLLM.Core.Extensions;
using System.Text;
using System.Text.Json;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service implementation for managing model costs - CSV/JSON parsing functionality
    /// </summary>
    public partial class AdminModelCostService
    {
        private string GenerateJsonExport(List<ModelCost> modelCosts)
        {
            var exportData = modelCosts.Select(mc => new ModelCostExportDto
            {
                CostName = mc.CostName,
                PricingModel = mc.PricingModel,
                PricingConfiguration = mc.PricingConfiguration,
                InputCostPerMillionTokens = mc.InputCostPerMillionTokens,
                OutputCostPerMillionTokens = mc.OutputCostPerMillionTokens,
                EmbeddingCostPerMillionTokens = mc.EmbeddingCostPerMillionTokens,
                BatchProcessingMultiplier = mc.BatchProcessingMultiplier,
                SupportsBatchProcessing = mc.SupportsBatchProcessing,
                CostPerSearchUnit = mc.CostPerSearchUnit,
                CachedInputCostPerMillionTokens = mc.CachedInputCostPerMillionTokens,
                CachedInputWriteCostPerMillionTokens = mc.CachedInputWriteCostPerMillionTokens
            });

            return JsonSerializer.Serialize(exportData, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        private string GenerateCsvExport(List<ModelCost> modelCosts)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Cost Name,Pricing Model,Pricing Configuration,Input Cost (per million tokens),Output Cost (per million tokens),Embedding Cost (per million tokens),Batch Processing Multiplier,Supports Batch Processing,Search Unit Cost (per 1K units),Cached Input Cost (per million tokens),Cached Write Cost (per million tokens)");

            foreach (var modelCost in modelCosts.OrderBy(mc => mc.CostName))
            {
                csv.AppendLine($"{EscapeCsvValue(modelCost.CostName)}," +
                    $"{modelCost.PricingModel}," +
                    $"{EscapeCsvValue(modelCost.PricingConfiguration ?? "")}," +
                    $"{modelCost.InputCostPerMillionTokens:F6}," +
                    $"{modelCost.OutputCostPerMillionTokens:F6}," +
                    $"{(modelCost.EmbeddingCostPerMillionTokens.HasValue ? modelCost.EmbeddingCostPerMillionTokens.Value.ToString("F6") : "")}," +
                    $"{(modelCost.BatchProcessingMultiplier?.ToString("F4") ?? "")}," +
                    $"{(modelCost.SupportsBatchProcessing ? "Yes" : "No")}," +
                    $"{(modelCost.CostPerSearchUnit?.ToString("F6") ?? "")}," +
                    $"{(modelCost.CachedInputCostPerMillionTokens?.ToString("F6") ?? "")}," +
                    $"{(modelCost.CachedInputWriteCostPerMillionTokens?.ToString("F6") ?? "")}");
            }

            return csv.ToString();
        }

        private List<CreateModelCostDto> ParseJsonImport(string jsonData)
        {
            try
            {
                var importData = JsonSerializer.Deserialize<List<ModelCostExportDto>>(jsonData);
                if (importData == null) return new List<CreateModelCostDto>();

                return importData.Select(d => new CreateModelCostDto
                {
                    CostName = d.CostName,
                    PricingModel = d.PricingModel,
                    PricingConfiguration = d.PricingConfiguration,
                    InputCostPerMillionTokens = d.InputCostPerMillionTokens,
                    OutputCostPerMillionTokens = d.OutputCostPerMillionTokens,
                    EmbeddingCostPerMillionTokens = d.EmbeddingCostPerMillionTokens,
                    BatchProcessingMultiplier = d.BatchProcessingMultiplier,
                    SupportsBatchProcessing = d.SupportsBatchProcessing,
                    CostPerSearchUnit = d.CostPerSearchUnit,
                    CachedInputCostPerMillionTokens = d.CachedInputCostPerMillionTokens,
                    CachedInputWriteCostPerMillionTokens = d.CachedInputWriteCostPerMillionTokens
                }).ToList();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse JSON import data");
                throw new ArgumentException("Invalid JSON format", ex);
            }
        }

        private List<CreateModelCostDto> ParseCsvImport(string csvData)
        {
            var modelCosts = new List<CreateModelCostDto>();
            var lines = csvData.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 2)
            {
                throw new ArgumentException("CSV data must contain header and at least one data row");
            }

            // Skip header
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(',');
                if (parts.Length < 2)
                {
                    _logger.LogWarning("Skipping invalid CSV line: {Line}", LoggingSanitizer.S(lines[i]));
                    continue;
                }

                try
                {
                    var modelCost = new CreateModelCostDto
                    {
                        CostName = UnescapeCsvValue(parts[0]),
                        PricingModel = parts.Length > 1 && Enum.TryParse<PricingModel>(parts[1], out var pricingModel) ? pricingModel : PricingModel.Standard,
                        PricingConfiguration = parts.Length > 2 ? UnescapeCsvValue(parts[2]) : null,
                        InputCostPerMillionTokens = parts.Length > 3 && decimal.TryParse(parts[3], out var inputCost) ? inputCost : 0,
                        OutputCostPerMillionTokens = parts.Length > 4 && decimal.TryParse(parts[4], out var outputCost) ? outputCost : 0,
                        EmbeddingCostPerMillionTokens = parts.Length > 5 && decimal.TryParse(parts[5], out var embeddingCost) ? embeddingCost : null,
                        BatchProcessingMultiplier = parts.Length > 6 && decimal.TryParse(parts[6], out var batchMultiplier) ? batchMultiplier : null,
                        SupportsBatchProcessing = parts.Length > 7 && (parts[7].Trim().ToLower() == "yes" || parts[7].Trim().ToLower() == "true"),
                        CostPerSearchUnit = parts.Length > 8 && decimal.TryParse(parts[8], out var searchUnitCost) ? searchUnitCost : null,
                        CachedInputCostPerMillionTokens = parts.Length > 9 && decimal.TryParse(parts[9], out var cachedInputCost) ? cachedInputCost : null,
                        CachedInputWriteCostPerMillionTokens = parts.Length > 10 && decimal.TryParse(parts[10], out var cachedWriteCost) ? cachedWriteCost : null
                    };

                    modelCosts.Add(modelCost);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse CSV line: {Line}", LoggingSanitizer.S(lines[i]));
                    throw new ArgumentException($"Invalid CSV data at line {i + 1}", ex);
                }
            }

            return modelCosts;
        }

        private static string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        private static string UnescapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            if (value.StartsWith("\"") && value.EndsWith("\""))
            {
                value = value.Substring(1, value.Length - 2);
                value = value.Replace("\"\"", "\"");
            }

            return value;
        }
    }
}
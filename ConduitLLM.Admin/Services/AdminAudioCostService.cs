using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs.Audio;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service implementation for managing audio cost configurations.
    /// </summary>
    public class AdminAudioCostService : IAdminAudioCostService
    {
        private readonly IAudioCostRepository _repository;
        private readonly ILogger<AdminAudioCostService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminAudioCostService"/> class.
        /// </summary>
        public AdminAudioCostService(
            IAudioCostRepository repository,
            ILogger<AdminAudioCostService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<List<AudioCostDto>> GetAllAsync()
        {
            var costs = await _repository.GetAllAsync();
            return costs.Select(MapToDto).ToList();
        }

        /// <inheritdoc/>
        public async Task<AudioCostDto?> GetByIdAsync(int id)
        {
            var cost = await _repository.GetByIdAsync(id);
            return cost != null ? MapToDto(cost) : null;
        }

        /// <inheritdoc/>
        public async Task<List<AudioCostDto>> GetByProviderAsync(string provider)
        {
            var costs = await _repository.GetByProviderAsync(provider);
            return costs.Select(MapToDto).ToList();
        }

        /// <inheritdoc/>
        public async Task<AudioCostDto?> GetCurrentCostAsync(string provider, string operationType, string? model = null)
        {
            var cost = await _repository.GetCurrentCostAsync(provider, operationType, model);
            return cost != null ? MapToDto(cost) : null;
        }

        /// <inheritdoc/>
        public async Task<List<AudioCostDto>> GetCostHistoryAsync(string provider, string operationType, string? model = null)
        {
            var costs = await _repository.GetCostHistoryAsync(provider, operationType, model);
            return costs.Select(MapToDto).ToList();
        }

        /// <inheritdoc/>
        public async Task<AudioCostDto> CreateAsync(CreateAudioCostDto dto)
        {
            var cost = new AudioCost
            {
                Provider = dto.ProviderType.ToString(),
                OperationType = dto.OperationType,
                Model = dto.Model,
                CostUnit = dto.CostUnit,
                CostPerUnit = dto.CostPerUnit,
                MinimumCharge = dto.MinimumCharge,
                AdditionalFactors = dto.AdditionalFactors,
                IsActive = dto.IsActive,
                EffectiveFrom = dto.EffectiveFrom,
                EffectiveTo = dto.EffectiveTo
            };

            var created = await _repository.CreateAsync(cost);
            _logger.LogInformation("Created audio cost configuration {Id} for {Provider} {Operation}",
                created.Id,
                created.Provider.Replace(Environment.NewLine, ""),
                created.OperationType.Replace(Environment.NewLine, ""));

            return MapToDto(created);
        }

        /// <inheritdoc/>
        public async Task<AudioCostDto?> UpdateAsync(int id, UpdateAudioCostDto dto)
        {
            var cost = await _repository.GetByIdAsync(id);
            if (cost == null)
            {
                return null;
            }

            // Update properties
            cost.Provider = dto.ProviderType.ToString();
            cost.OperationType = dto.OperationType;
            cost.Model = dto.Model;
            cost.CostUnit = dto.CostUnit;
            cost.CostPerUnit = dto.CostPerUnit;
            cost.MinimumCharge = dto.MinimumCharge;
            cost.AdditionalFactors = dto.AdditionalFactors;
            cost.IsActive = dto.IsActive;
            cost.EffectiveFrom = dto.EffectiveFrom;
            cost.EffectiveTo = dto.EffectiveTo;

            var updated = await _repository.UpdateAsync(cost);
            _logger.LogInformation("Updated audio cost configuration {Id}",
                id);

            return MapToDto(updated);
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(int id)
        {
            var deleted = await _repository.DeleteAsync(id);
            if (deleted)
            {
                _logger.LogInformation("Deleted audio cost configuration {Id}",
                id);
            }
            return deleted;
        }

        /// <inheritdoc/>
        public async Task<BulkImportResult> ImportCostsAsync(string data, string format)
        {
            var result = new BulkImportResult
            {
                SuccessCount = 0,
                FailureCount = 0,
                Errors = new List<string>()
            };

            try
            {
                format = format?.ToLowerInvariant() ?? "json";
                var costs = format switch
                {
                    "json" => ParseJsonImport(data),
                    "csv" => ParseCsvImport(data),
                    _ => throw new ArgumentException($"Unsupported import format: {format}")
                };

                foreach (var cost in costs)
                {
                    try
                    {
                        // Check if cost configuration already exists
                        var existing = await _repository.GetCurrentCostAsync(
                            cost.Provider, cost.OperationType, cost.Model);

                        if (existing != null)
                        {
                            // Update existing
                            existing.CostUnit = cost.CostUnit;
                            existing.CostPerUnit = cost.CostPerUnit;
                            existing.EffectiveFrom = cost.EffectiveFrom;
                            existing.EffectiveTo = cost.EffectiveTo;
                            existing.UpdatedAt = DateTime.UtcNow;

                            await _repository.UpdateAsync(existing);
                        }
                        else
                        {
                            // Create new
                            await _repository.CreateAsync(cost);
                        }

                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        result.FailureCount++;
                        result.Errors.Add($"Failed to import cost for {cost.Provider}/{cost.OperationType}: {ex.Message}");
                    }
                }

                _logger.LogInformation("Imported {Success} costs successfully, {Failed} failed",
                result.SuccessCount,
                result.FailureCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error during bulk import");
                result.Errors.Add($"Import failed: {ex.Message}");
            }

            return result;
        }

        /// <inheritdoc/>
        public async Task<string> ExportCostsAsync(string format, string? provider = null)
        {
            var costs = provider != null ? await _repository.GetByProviderAsync(provider) : await _repository.GetAllAsync();

            format = format?.ToLowerInvariant() ?? "json";

            return format switch
            {
                "json" => GenerateJsonExport(costs),
                "csv" => GenerateCsvExport(costs),
                _ => throw new ArgumentException($"Unsupported export format: {format}")
            };
        }

        private static AudioCostDto MapToDto(AudioCost cost)
        {
            return new AudioCostDto
            {
                Id = cost.Id,
                ProviderType = Enum.TryParse<ProviderType>(cost.Provider, true, out var providerType) 
                    ? providerType 
                    : ProviderType.OpenAI, // Default to OpenAI if parsing fails
                OperationType = cost.OperationType,
                Model = cost.Model,
                CostUnit = cost.CostUnit,
                CostPerUnit = cost.CostPerUnit,
                MinimumCharge = cost.MinimumCharge,
                AdditionalFactors = cost.AdditionalFactors,
                IsActive = cost.IsActive,
                EffectiveFrom = cost.EffectiveFrom,
                EffectiveTo = cost.EffectiveTo,
                CreatedAt = cost.CreatedAt,
                UpdatedAt = cost.UpdatedAt
            };
        }

        private List<AudioCost> ParseJsonImport(string jsonData)
        {
            try
            {
                var importData = JsonSerializer.Deserialize<List<AudioCostImportDto>>(jsonData);
                if (importData == null) return new List<AudioCost>();

                return importData.Select(d => new AudioCost
                {
                    Provider = d.Provider,
                    OperationType = d.OperationType,
                    Model = d.Model ?? "default",
                    CostUnit = d.CostUnit,
                    CostPerUnit = d.CostPerUnit,
                    MinimumCharge = d.MinimumCharge,
                    IsActive = d.IsActive ?? true,
                    EffectiveFrom = d.EffectiveFrom ?? DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Failed to parse JSON import data");
                throw new ArgumentException("Invalid JSON format", ex);
            }
        }

        private List<AudioCost> ParseCsvImport(string csvData)
        {
            var costs = new List<AudioCost>();
            var lines = csvData.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 2)
            {
                throw new ArgumentException("CSV data must contain header and at least one data row");
            }

            // Skip header
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(',');
                if (parts.Length < 5)
                {
                    _logger.LogWarning("Skipping invalid CSV line: {Line}",
                lines[i].Replace(Environment.NewLine, ""));
                    continue;
                }

                try
                {
                    costs.Add(new AudioCost
                    {
                        Provider = parts[0].Trim(),
                        OperationType = parts[1].Trim(),
                        Model = parts.Length > 2 ? parts[2].Trim() : "default",
                        CostUnit = parts[3].Trim(),
                        CostPerUnit = decimal.Parse(parts[4].Trim()),
                        MinimumCharge = parts.Length > 5 ? decimal.Parse(parts[5].Trim()) : null,
                        IsActive = true,
                        EffectiveFrom = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                "Failed to parse CSV line: {Line}",
                lines[i].Replace(Environment.NewLine, ""));
                    throw new ArgumentException($"Invalid CSV data at line {i + 1}", ex);
                }
            }

            return costs;
        }

        private string GenerateJsonExport(List<AudioCost> costs)
        {
            var exportData = costs.Select(c => new AudioCostImportDto
            {
                Provider = c.Provider,
                OperationType = c.OperationType,
                Model = c.Model,
                CostUnit = c.CostUnit,
                CostPerUnit = c.CostPerUnit,
                MinimumCharge = c.MinimumCharge,
                IsActive = c.IsActive,
                EffectiveFrom = c.EffectiveFrom
            });

            return JsonSerializer.Serialize(exportData, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        private string GenerateCsvExport(List<AudioCost> costs)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Provider,OperationType,Model,CostUnit,CostPerUnit,MinimumCharge");

            foreach (var cost in costs.OrderBy(c => c.Provider).ThenBy(c => c.OperationType))
            {
                csv.AppendLine($"{cost.Provider},{cost.OperationType},{cost.Model}," +
                    $"{cost.CostUnit},{cost.CostPerUnit},{cost.MinimumCharge ?? 0}");
            }

            return csv.ToString();
        }
    }

    /// <summary>
    /// DTO for importing audio costs.
    /// </summary>
    internal class AudioCostImportDto
    {
        public string Provider { get; set; } = string.Empty;
        public string OperationType { get; set; } = string.Empty;
        public string? Model { get; set; }
        public string CostUnit { get; set; } = string.Empty;
        public decimal CostPerUnit { get; set; }
        public decimal? MinimumCharge { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? EffectiveFrom { get; set; }
    }
}

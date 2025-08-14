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

using ConduitLLM.Configuration.Interfaces;
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
        public async Task<List<AudioCostDto>> GetByProviderAsync(int providerId)
        {
            var costs = await _repository.GetByProviderAsync(providerId);
            return costs.Select(MapToDto).ToList();
        }

        /// <inheritdoc/>
        public async Task<AudioCostDto?> GetCurrentCostAsync(int providerId, string operationType, string? model = null)
        {
            var cost = await _repository.GetCurrentCostAsync(providerId, operationType, model);
            return cost != null ? MapToDto(cost) : null;
        }

        /// <inheritdoc/>
        public async Task<List<AudioCostDto>> GetCostHistoryAsync(int providerId, string operationType, string? model = null)
        {
            var costs = await _repository.GetCostHistoryAsync(providerId, operationType, model);
            return costs.Select(MapToDto).ToList();
        }

        /// <inheritdoc/>
        public async Task<AudioCostDto> CreateAsync(CreateAudioCostDto dto)
        {
            var cost = new AudioCost
            {
                ProviderId = dto.ProviderId,
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
            _logger.LogInformation("Created audio cost configuration {Id} for Provider {ProviderId} {Operation}",
                created.Id,
                created.ProviderId,
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
            cost.ProviderId = dto.ProviderId;
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
                            cost.ProviderId, cost.OperationType, cost.Model);

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
                        result.Errors.Add($"Failed to import cost for Provider {cost.ProviderId}/{cost.OperationType}: {ex.Message}");
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
        public async Task<string> ExportCostsAsync(string format, int? providerId = null)
        {
            List<AudioCost> costs;
            if (providerId.HasValue)
            {
                costs = await _repository.GetByProviderAsync(providerId.Value);
            }
            else
            {
                costs = await _repository.GetAllAsync();
            }

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
                ProviderId = cost.ProviderId,
                ProviderName = cost.Provider?.ProviderName,
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

                var costs = new List<AudioCost>();
                foreach (var d in importData)
                {
                    costs.Add(new AudioCost
                    {
                        ProviderId = d.ProviderId,
                        OperationType = d.OperationType,
                        Model = d.Model ?? "default",
                        CostUnit = d.CostUnit,
                        CostPerUnit = d.CostPerUnit,
                        MinimumCharge = d.MinimumCharge,
                        IsActive = d.IsActive ?? true,
                        EffectiveFrom = d.EffectiveFrom ?? DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                return costs;
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
                    var providerIdString = parts[0].Trim();
                    if (!int.TryParse(providerIdString, out var providerId))
                    {
                        _logger.LogWarning("Invalid provider ID in CSV: {ProviderId}", providerIdString);
                        continue;
                    }
                    
                    costs.Add(new AudioCost
                    {
                        ProviderId = providerId,
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
                ProviderId = c.ProviderId,
                ProviderName = c.Provider?.ProviderName,
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
            csv.AppendLine("ProviderId,OperationType,Model,CostUnit,CostPerUnit,MinimumCharge");

            foreach (var cost in costs.OrderBy(c => c.ProviderId).ThenBy(c => c.OperationType))
            {
                csv.AppendLine($"{cost.ProviderId},{cost.OperationType},{cost.Model}," +
                    $"{cost.CostUnit},{cost.CostPerUnit},{cost.MinimumCharge ?? 0}");
            }

            return csv.ToString();
        }
    }

}

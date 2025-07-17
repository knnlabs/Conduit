using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;

using MassTransit;
using Microsoft.Extensions.Logging;

using static ConduitLLM.Core.Extensions.LoggingSanitizer;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service implementation for managing model costs through the Admin API
    /// </summary>
    public class AdminModelCostService : EventPublishingServiceBase, IAdminModelCostService
    {
        private readonly IModelCostRepository _modelCostRepository;
        private readonly IRequestLogRepository _requestLogRepository;
        private readonly ILogger<AdminModelCostService> _logger;

        /// <summary>
        /// Initializes a new instance of the AdminModelCostService
        /// </summary>
        /// <param name="modelCostRepository">The model cost repository</param>
        /// <param name="requestLogRepository">The request log repository</param>
        /// <param name="publishEndpoint">Optional event publishing endpoint (null if MassTransit not configured)</param>
        /// <param name="logger">The logger</param>
        public AdminModelCostService(
            IModelCostRepository modelCostRepository,
            IRequestLogRepository requestLogRepository,
            IPublishEndpoint? publishEndpoint,
            ILogger<AdminModelCostService> logger)
            : base(publishEndpoint, logger)
        {
            _modelCostRepository = modelCostRepository ?? throw new ArgumentNullException(nameof(modelCostRepository));
            _requestLogRepository = requestLogRepository ?? throw new ArgumentNullException(nameof(requestLogRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<ModelCostDto> CreateModelCostAsync(CreateModelCostDto modelCost)
        {
            if (modelCost == null)
            {
                throw new ArgumentNullException(nameof(modelCost));
            }

            try
            {
                // Check if a model cost with the same pattern already exists
                var existingModelCost = await _modelCostRepository.GetByModelIdPatternAsync(modelCost.ModelIdPattern);
                if (existingModelCost != null)
                {
                    throw new InvalidOperationException($"A model cost with pattern '{modelCost.ModelIdPattern}' already exists");
                }

                // Convert DTO to entity
                var modelCostEntity = modelCost.ToEntity();

                // Save to database
                var id = await _modelCostRepository.CreateAsync(modelCostEntity);

                // Get the created model cost
                var createdModelCost = await _modelCostRepository.GetByIdAsync(id);
                if (createdModelCost == null)
                {
                    throw new InvalidOperationException($"Failed to retrieve newly created model cost with ID {id}");
                }

                // Publish ModelCostChanged event for cache invalidation and cross-service coordination
                await PublishEventAsync(
                    new ModelCostChanged
                    {
                        ModelCostId = createdModelCost.Id,
                        ModelIdPattern = createdModelCost.ModelIdPattern,
                        ChangeType = "Created",
                        ChangedProperties = new[] { "Created" },
                        CorrelationId = Guid.NewGuid().ToString()
                    },
                    "CreateModelCost");

                _logger.LogInformation("Created model cost with pattern '{Pattern}'", modelCost.ModelIdPattern.Replace(Environment.NewLine, ""));
                return createdModelCost.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model cost with pattern '{Pattern}'", modelCost.ModelIdPattern.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteModelCostAsync(int id)
        {
            try
            {
                // Get model cost info before deletion for event publishing
                var modelCostToDelete = await _modelCostRepository.GetByIdAsync(id);
                
                var result = await _modelCostRepository.DeleteAsync(id);

                if (result)
                {
                    // Publish ModelCostChanged event for cache invalidation and cleanup
                    if (modelCostToDelete != null)
                    {
                        await PublishEventAsync(
                            new ModelCostChanged
                            {
                                ModelCostId = id,
                                ModelIdPattern = modelCostToDelete.ModelIdPattern,
                                ChangeType = "Deleted",
                                ChangedProperties = new[] { "Deleted" },
                                CorrelationId = Guid.NewGuid().ToString()
                            },
                            "DeleteModelCost");
                    }
                    
                    _logger.LogInformation("Deleted model cost with ID {Id}",
                id);
                }
                else
                {
                    _logger.LogWarning("Model cost with ID {Id} not found for deletion",
                id);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error deleting model cost with ID {Id}",
                id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ModelCostDto>> GetAllModelCostsAsync()
        {
            try
            {
                var modelCosts = await _modelCostRepository.GetAllAsync();
                return modelCosts.Select(mc => mc.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error getting all model costs");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ModelCostDto?> GetModelCostByIdAsync(int id)
        {
            try
            {
                var modelCost = await _modelCostRepository.GetByIdAsync(id);
                return modelCost?.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error getting model cost with ID {Id}",
                id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ModelCostDto?> GetModelCostByPatternAsync(string modelIdPattern)
        {
            if (string.IsNullOrWhiteSpace(modelIdPattern))
            {
                throw new ArgumentException("Model ID pattern cannot be null or empty", nameof(modelIdPattern));
            }

            try
            {
                var modelCost = await _modelCostRepository.GetByModelIdPatternAsync(modelIdPattern);
                return modelCost?.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model cost with pattern '{Pattern}'", modelIdPattern.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ModelCostOverviewDto>> GetModelCostOverviewAsync(DateTime startDate, DateTime endDate)
        {
            if (startDate > endDate)
            {
                throw new ArgumentException("Start date cannot be after end date", nameof(startDate));
            }

            try
            {
                // Get request logs for the specified time period
                var logs = await _requestLogRepository.GetByDateRangeAsync(startDate, endDate);
                if (logs == null || !logs.Any())
                {
                    return Enumerable.Empty<ModelCostOverviewDto>();
                }

                // Group by model and aggregate cost data
                var modelGroups = logs
                    .Where(l => !string.IsNullOrEmpty(l.ModelName)) // Filter out logs with no model name
                    .GroupBy(l => l.ModelName)
                    .Select(g => new ModelCostOverviewDto
                    {
                        Model = g.Key ?? "Unknown",
                        RequestCount = g.Count(),
                        TotalCost = g.Sum(l => l.Cost),
                        InputTokens = g.Sum(l => l.InputTokens),
                        OutputTokens = g.Sum(l => l.OutputTokens)
                    })
                    .OrderByDescending(m => m.TotalCost)
                    .ToList();

                return modelGroups;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error getting model cost overview for period {StartDate} to {EndDate}",
                startDate,
                endDate);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ModelCostDto>> GetModelCostsByProviderAsync(string providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
            }

            try
            {
                var modelCosts = await _modelCostRepository.GetByProviderAsync(providerName);
                return modelCosts.Select(mc => mc.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model costs for provider '{ProviderName}'", providerName.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> ImportModelCostsAsync(IEnumerable<CreateModelCostDto> modelCosts)
        {
            if (modelCosts == null)
            {
                throw new ArgumentNullException(nameof(modelCosts));
            }

            if (!modelCosts.Any())
            {
                return 0;
            }

            try
            {
                int importedCount = 0;

                // Process each model cost
                foreach (var modelCost in modelCosts)
                {
                    try
                    {
                        // Check if a model cost with the same pattern already exists
                        var existingModelCost = await _modelCostRepository.GetByModelIdPatternAsync(modelCost.ModelIdPattern);

                        if (existingModelCost != null)
                        {
                            // Update existing model cost
                            var updateDto = new UpdateModelCostDto
                            {
                                Id = existingModelCost.Id,
                                ModelIdPattern = modelCost.ModelIdPattern,
                                InputTokenCost = modelCost.InputTokenCost,
                                OutputTokenCost = modelCost.OutputTokenCost,
                                EmbeddingTokenCost = modelCost.EmbeddingTokenCost,
                                ImageCostPerImage = modelCost.ImageCostPerImage,
                                AudioCostPerMinute = modelCost.AudioCostPerMinute,
                                AudioCostPerKCharacters = modelCost.AudioCostPerKCharacters,
                                AudioInputCostPerMinute = modelCost.AudioInputCostPerMinute,
                                AudioOutputCostPerMinute = modelCost.AudioOutputCostPerMinute,
                                VideoCostPerSecond = modelCost.VideoCostPerSecond,
                                VideoResolutionMultipliers = modelCost.VideoResolutionMultipliers
                            };

                            existingModelCost.UpdateFrom(updateDto);
                            await _modelCostRepository.UpdateAsync(existingModelCost);
                            
                            // Publish ModelCostChanged event for updated model cost
                            await PublishEventAsync(
                                new ModelCostChanged
                                {
                                    ModelCostId = existingModelCost.Id,
                                    ModelIdPattern = existingModelCost.ModelIdPattern,
                                    ChangeType = "Updated",
                                    ChangedProperties = new[] { "ImportUpdated" },
                                    CorrelationId = Guid.NewGuid().ToString()
                                },
                                "ImportModelCosts");
                        }
                        else
                        {
                            // Create new model cost
                            var modelCostEntity = modelCost.ToEntity();
                            var newId = await _modelCostRepository.CreateAsync(modelCostEntity);
                            
                            // Publish ModelCostChanged event for new model cost
                            await PublishEventAsync(
                                new ModelCostChanged
                                {
                                    ModelCostId = newId,
                                    ModelIdPattern = modelCost.ModelIdPattern,
                                    ChangeType = "Created",
                                    ChangedProperties = new[] { "ImportCreated" },
                                    CorrelationId = Guid.NewGuid().ToString()
                                },
                                "ImportModelCosts");
                        }

                        importedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                "Error importing model cost with pattern '{Pattern}'",
                modelCost.ModelIdPattern.Replace(Environment.NewLine, ""));
                        // Continue with next model cost
                    }
                }

                _logger.LogInformation("Imported {Count} model costs",
                importedCount);
                return importedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error importing model costs");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateModelCostAsync(UpdateModelCostDto modelCost)
        {
            if (modelCost == null)
            {
                throw new ArgumentNullException(nameof(modelCost));
            }

            try
            {
                // Get existing model cost
                var existingModelCost = await _modelCostRepository.GetByIdAsync(modelCost.Id);
                if (existingModelCost == null)
                {
                    _logger.LogWarning("Model cost with ID {Id} not found",
                modelCost.Id);
                    return false;
                }

                // Check if the pattern is being changed and a model cost with the new pattern already exists
                if (existingModelCost.ModelIdPattern != modelCost.ModelIdPattern)
                {
                    var patternExists = await _modelCostRepository.GetByModelIdPatternAsync(modelCost.ModelIdPattern);
                    if (patternExists != null && patternExists.Id != modelCost.Id)
                    {
                        throw new InvalidOperationException($"Another model cost with pattern '{modelCost.ModelIdPattern}' already exists");
                    }
                }

                // Track changes for event publishing
                var changedProperties = new List<string>();
                if (existingModelCost.ModelIdPattern != modelCost.ModelIdPattern)
                    changedProperties.Add(nameof(modelCost.ModelIdPattern));
                if (existingModelCost.InputTokenCost != modelCost.InputTokenCost)
                    changedProperties.Add(nameof(modelCost.InputTokenCost));
                if (existingModelCost.OutputTokenCost != modelCost.OutputTokenCost)
                    changedProperties.Add(nameof(modelCost.OutputTokenCost));
                if (existingModelCost.EmbeddingTokenCost != modelCost.EmbeddingTokenCost)
                    changedProperties.Add(nameof(modelCost.EmbeddingTokenCost));
                if (existingModelCost.ImageCostPerImage != modelCost.ImageCostPerImage)
                    changedProperties.Add(nameof(modelCost.ImageCostPerImage));

                // Update entity
                existingModelCost.UpdateFrom(modelCost);

                // Save changes
                var result = await _modelCostRepository.UpdateAsync(existingModelCost);

                if (result)
                {
                    // Publish ModelCostChanged event for cache invalidation and cross-service coordination
                    if (changedProperties.Any())
                    {
                        await PublishEventAsync(
                            new ModelCostChanged
                            {
                                ModelCostId = modelCost.Id,
                                ModelIdPattern = existingModelCost.ModelIdPattern,
                                ChangeType = "Updated",
                                ChangedProperties = changedProperties.ToArray(),
                                CorrelationId = Guid.NewGuid().ToString()
                            },
                            "UpdateModelCost");
                    }
                    
                    _logger.LogInformation("Updated model cost with ID {Id}",
                modelCost.Id);
                }
                else
                {
                    _logger.LogWarning("Failed to update model cost with ID {Id}",
                modelCost.Id);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error updating model cost with ID {Id}",
                modelCost.Id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<string> ExportModelCostsAsync(string format, string? providerName = null)
        {
            var modelCosts = providerName != null 
                ? await _modelCostRepository.GetByProviderAsync(providerName)
                : await _modelCostRepository.GetAllAsync();

            format = format?.ToLowerInvariant() ?? "json";

            return format switch
            {
                "json" => GenerateJsonExport(modelCosts),
                "csv" => GenerateCsvExport(modelCosts),
                _ => throw new ArgumentException($"Unsupported export format: {format}")
            };
        }

        /// <inheritdoc />
        public async Task<BulkImportResult> ImportModelCostsAsync(string data, string format)
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
                var modelCosts = format switch
                {
                    "json" => ParseJsonImport(data),
                    "csv" => ParseCsvImport(data),
                    _ => throw new ArgumentException($"Unsupported import format: {format}")
                };

                foreach (var modelCost in modelCosts)
                {
                    try
                    {
                        // Check if model cost with the same pattern already exists
                        var existingModelCost = await _modelCostRepository.GetByModelIdPatternAsync(modelCost.ModelIdPattern);

                        if (existingModelCost != null)
                        {
                            // Update existing model cost
                            var updateDto = new UpdateModelCostDto
                            {
                                Id = existingModelCost.Id,
                                ModelIdPattern = modelCost.ModelIdPattern,
                                InputTokenCost = modelCost.InputTokenCost,
                                OutputTokenCost = modelCost.OutputTokenCost,
                                EmbeddingTokenCost = modelCost.EmbeddingTokenCost,
                                ImageCostPerImage = modelCost.ImageCostPerImage,
                                AudioCostPerMinute = modelCost.AudioCostPerMinute,
                                AudioCostPerKCharacters = modelCost.AudioCostPerKCharacters,
                                AudioInputCostPerMinute = modelCost.AudioInputCostPerMinute,
                                AudioOutputCostPerMinute = modelCost.AudioOutputCostPerMinute,
                                VideoCostPerSecond = modelCost.VideoCostPerSecond,
                                VideoResolutionMultipliers = modelCost.VideoResolutionMultipliers
                            };

                            existingModelCost.UpdateFrom(updateDto);
                            await _modelCostRepository.UpdateAsync(existingModelCost);
                        }
                        else
                        {
                            // Create new model cost
                            var modelCostEntity = modelCost.ToEntity();
                            await _modelCostRepository.CreateAsync(modelCostEntity);
                        }

                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        result.FailureCount++;
                        result.Errors.Add($"Failed to import model cost for pattern '{modelCost.ModelIdPattern}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.FailureCount++;
                result.Errors.Add($"Failed to parse import data: {ex.Message}");
            }

            return result;
        }

        private string GenerateJsonExport(List<ModelCost> modelCosts)
        {
            var exportData = modelCosts.Select(mc => new ModelCostExportDto
            {
                ModelIdPattern = mc.ModelIdPattern,
                InputTokenCost = mc.InputTokenCost,
                OutputTokenCost = mc.OutputTokenCost,
                EmbeddingTokenCost = mc.EmbeddingTokenCost,
                ImageCostPerImage = mc.ImageCostPerImage,
                AudioCostPerMinute = mc.AudioCostPerMinute,
                AudioCostPerKCharacters = mc.AudioCostPerKCharacters,
                AudioInputCostPerMinute = mc.AudioInputCostPerMinute,
                AudioOutputCostPerMinute = mc.AudioOutputCostPerMinute,
                VideoCostPerSecond = mc.VideoCostPerSecond,
                VideoResolutionMultipliers = mc.VideoResolutionMultipliers
            });

            return JsonSerializer.Serialize(exportData, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        private string GenerateCsvExport(List<ModelCost> modelCosts)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Model Pattern,Input Cost (per 1K tokens),Output Cost (per 1K tokens),Embedding Cost (per 1K tokens),Image Cost (per image),Audio Cost (per minute),Audio Cost (per 1K chars),Audio Input Cost (per minute),Audio Output Cost (per minute),Video Cost (per second),Video Resolution Multipliers");

            foreach (var modelCost in modelCosts.OrderBy(mc => mc.ModelIdPattern))
            {
                csv.AppendLine($"{EscapeCsvValue(modelCost.ModelIdPattern)}," +
                    $"{(modelCost.InputTokenCost * 1000):F6}," +
                    $"{(modelCost.OutputTokenCost * 1000):F6}," +
                    $"{(modelCost.EmbeddingTokenCost.HasValue ? (modelCost.EmbeddingTokenCost.Value * 1000).ToString("F6") : "")}," +
                    $"{(modelCost.ImageCostPerImage?.ToString("F4") ?? "")}," +
                    $"{(modelCost.AudioCostPerMinute?.ToString("F4") ?? "")}," +
                    $"{(modelCost.AudioCostPerKCharacters?.ToString("F4") ?? "")}," +
                    $"{(modelCost.AudioInputCostPerMinute?.ToString("F4") ?? "")}," +
                    $"{(modelCost.AudioOutputCostPerMinute?.ToString("F4") ?? "")}," +
                    $"{(modelCost.VideoCostPerSecond?.ToString("F4") ?? "")}," +
                    $"{EscapeCsvValue(modelCost.VideoResolutionMultipliers ?? "")}");
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
                    ModelIdPattern = d.ModelIdPattern,
                    InputTokenCost = d.InputTokenCost,
                    OutputTokenCost = d.OutputTokenCost,
                    EmbeddingTokenCost = d.EmbeddingTokenCost,
                    ImageCostPerImage = d.ImageCostPerImage,
                    AudioCostPerMinute = d.AudioCostPerMinute,
                    AudioCostPerKCharacters = d.AudioCostPerKCharacters,
                    AudioInputCostPerMinute = d.AudioInputCostPerMinute,
                    AudioOutputCostPerMinute = d.AudioOutputCostPerMinute,
                    VideoCostPerSecond = d.VideoCostPerSecond,
                    VideoResolutionMultipliers = d.VideoResolutionMultipliers
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
                    _logger.LogWarning("Skipping invalid CSV line: {Line}", lines[i].Replace(Environment.NewLine, ""));
                    continue;
                }

                try
                {
                    var modelCost = new CreateModelCostDto
                    {
                        ModelIdPattern = UnescapeCsvValue(parts[0]),
                        InputTokenCost = decimal.TryParse(parts[1], out var inputCost) ? inputCost / 1000 : 0,
                        OutputTokenCost = decimal.TryParse(parts[2], out var outputCost) ? outputCost / 1000 : 0,
                        EmbeddingTokenCost = decimal.TryParse(parts[3], out var embeddingCost) ? embeddingCost / 1000 : null,
                        ImageCostPerImage = decimal.TryParse(parts[4], out var imageCost) ? imageCost : null,
                        AudioCostPerMinute = decimal.TryParse(parts[5], out var audioCost) ? audioCost : null,
                        AudioCostPerKCharacters = decimal.TryParse(parts[6], out var audioKCharCost) ? audioKCharCost : null,
                        AudioInputCostPerMinute = decimal.TryParse(parts[7], out var audioInputCost) ? audioInputCost : null,
                        AudioOutputCostPerMinute = decimal.TryParse(parts[8], out var audioOutputCost) ? audioOutputCost : null,
                        VideoCostPerSecond = decimal.TryParse(parts[9], out var videoCost) ? videoCost : null,
                        VideoResolutionMultipliers = parts.Length > 10 ? UnescapeCsvValue(parts[10]) : null
                    };

                    modelCosts.Add(modelCost);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse CSV line: {Line}", lines[i].Replace(Environment.NewLine, ""));
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

    /// <summary>
    /// DTO for exporting model costs
    /// </summary>
    internal class ModelCostExportDto
    {
        public string ModelIdPattern { get; set; } = string.Empty;
        public decimal InputTokenCost { get; set; }
        public decimal OutputTokenCost { get; set; }
        public decimal? EmbeddingTokenCost { get; set; }
        public decimal? ImageCostPerImage { get; set; }
        public decimal? AudioCostPerMinute { get; set; }
        public decimal? AudioCostPerKCharacters { get; set; }
        public decimal? AudioInputCostPerMinute { get; set; }
        public decimal? AudioOutputCostPerMinute { get; set; }
        public decimal? VideoCostPerSecond { get; set; }
        public string? VideoResolutionMultipliers { get; set; }
    }
}

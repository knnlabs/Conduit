using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;

using MassTransit;
using Microsoft.EntityFrameworkCore;
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
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;

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
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            IPublishEndpoint? publishEndpoint,
            ILogger<AdminModelCostService> logger)
            : base(publishEndpoint, logger)
        {
            _modelCostRepository = modelCostRepository ?? throw new ArgumentNullException(nameof(modelCostRepository));
            _requestLogRepository = requestLogRepository ?? throw new ArgumentNullException(nameof(requestLogRepository));
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
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
                // Check if a model cost with the same name already exists
                var existingModelCost = await _modelCostRepository.GetByCostNameAsync(modelCost.CostName);
                if (existingModelCost != null)
                {
                    throw new InvalidOperationException($"A model cost with name '{modelCost.CostName}' already exists");
                }

                // Convert DTO to entity
                var modelCostEntity = modelCost.ToEntity();

                // Save to database
                var id = await _modelCostRepository.CreateAsync(modelCostEntity);

                // Create model-cost mappings if provided
                if (modelCost.ModelProviderMappingIds != null && modelCost.ModelProviderMappingIds.Any())
                {
                    using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                    foreach (var mappingId in modelCost.ModelProviderMappingIds)
                    {
                        var modelCostMapping = new ModelCostMapping
                        {
                            ModelCostId = id,
                            ModelProviderMappingId = mappingId,
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true
                        };
                        dbContext.ModelCostMappings.Add(modelCostMapping);
                    }
                    await dbContext.SaveChangesAsync();
                }

                // Get the created model cost with mappings
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
                        CostName = createdModelCost.CostName,
                        ChangeType = "Created",
                        ChangedProperties = new[] { "Created" },
                        CorrelationId = Guid.NewGuid().ToString()
                    },
                    "CreateModelCost");

                _logger.LogInformation("Created model cost with name '{CostName}'", modelCost.CostName.Replace(Environment.NewLine, ""));
                return createdModelCost.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model cost with name '{CostName}'", modelCost.CostName.Replace(Environment.NewLine, ""));
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
                                CostName = modelCostToDelete.CostName,
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
        public async Task<ModelCostDto?> GetModelCostByCostNameAsync(string costName)
        {
            if (string.IsNullOrWhiteSpace(costName))
            {
                throw new ArgumentException("Cost name cannot be null or empty", nameof(costName));
            }

            try
            {
                var modelCost = await _modelCostRepository.GetByCostNameAsync(costName);
                return modelCost?.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model cost with name '{CostName}'", costName.Replace(Environment.NewLine, ""));
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
        public async Task<IEnumerable<ModelCostDto>> GetModelCostsByProviderAsync(int providerId)
        {
            try
            {
                var modelCosts = await _modelCostRepository.GetByProviderAsync(providerId);
                return modelCosts.Select(mc => mc.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model costs for provider {ProviderId}", providerId);
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
                        // Check if a model cost with the same name already exists
                        var existingModelCost = await _modelCostRepository.GetByCostNameAsync(modelCost.CostName);

                        if (existingModelCost != null)
                        {
                            // Update existing model cost
                            var updateDto = new UpdateModelCostDto
                            {
                                Id = existingModelCost.Id,
                                CostName = modelCost.CostName,
                                PricingModel = modelCost.PricingModel,
                                PricingConfiguration = modelCost.PricingConfiguration,
                                InputCostPerMillionTokens = modelCost.InputCostPerMillionTokens,
                                OutputCostPerMillionTokens = modelCost.OutputCostPerMillionTokens,
                                EmbeddingCostPerMillionTokens = modelCost.EmbeddingCostPerMillionTokens,
                                ImageCostPerImage = modelCost.ImageCostPerImage,
                                AudioCostPerMinute = modelCost.AudioCostPerMinute,
                                AudioCostPerKCharacters = modelCost.AudioCostPerKCharacters,
                                AudioInputCostPerMinute = modelCost.AudioInputCostPerMinute,
                                AudioOutputCostPerMinute = modelCost.AudioOutputCostPerMinute,
                                VideoCostPerSecond = modelCost.VideoCostPerSecond,
                                VideoResolutionMultipliers = modelCost.VideoResolutionMultipliers,
                                ImageResolutionMultipliers = modelCost.ImageResolutionMultipliers,
                                BatchProcessingMultiplier = modelCost.BatchProcessingMultiplier,
                                SupportsBatchProcessing = modelCost.SupportsBatchProcessing,
                                CostPerSearchUnit = modelCost.CostPerSearchUnit,
                                CostPerInferenceStep = modelCost.CostPerInferenceStep,
                                DefaultInferenceSteps = modelCost.DefaultInferenceSteps
                            };

                            existingModelCost.UpdateFrom(updateDto);
                            await _modelCostRepository.UpdateAsync(existingModelCost);
                            
                            // Publish ModelCostChanged event for updated model cost
                            await PublishEventAsync(
                                new ModelCostChanged
                                {
                                    ModelCostId = existingModelCost.Id,
                                    CostName = existingModelCost.CostName,
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
                                    CostName = modelCost.CostName,
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
                "Error importing model cost with name '{CostName}'",
                modelCost.CostName.Replace(Environment.NewLine, ""));
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

                // Check if the cost name is being changed and a model cost with the new name already exists
                if (existingModelCost.CostName != modelCost.CostName)
                {
                    var nameExists = await _modelCostRepository.GetByCostNameAsync(modelCost.CostName);
                    if (nameExists != null && nameExists.Id != modelCost.Id)
                    {
                        throw new InvalidOperationException($"Another model cost with name '{modelCost.CostName}' already exists");
                    }
                }

                // Track changes for event publishing
                var changedProperties = new List<string>();
                if (existingModelCost.CostName != modelCost.CostName)
                    changedProperties.Add(nameof(modelCost.CostName));
                if (existingModelCost.InputCostPerMillionTokens != modelCost.InputCostPerMillionTokens)
                    changedProperties.Add(nameof(modelCost.InputCostPerMillionTokens));
                if (existingModelCost.OutputCostPerMillionTokens != modelCost.OutputCostPerMillionTokens)
                    changedProperties.Add(nameof(modelCost.OutputCostPerMillionTokens));
                if (existingModelCost.EmbeddingCostPerMillionTokens != modelCost.EmbeddingCostPerMillionTokens)
                    changedProperties.Add(nameof(modelCost.EmbeddingCostPerMillionTokens));
                if (existingModelCost.ImageCostPerImage != modelCost.ImageCostPerImage)
                    changedProperties.Add(nameof(modelCost.ImageCostPerImage));

                // Update entity
                existingModelCost.UpdateFrom(modelCost);

                // Save changes
                var result = await _modelCostRepository.UpdateAsync(existingModelCost);

                // Update model-cost mappings if provided
                if (modelCost.ModelProviderMappingIds != null)
                {
                    using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                    
                    // Remove existing mappings
                    var existingMappings = await dbContext.ModelCostMappings
                        .Where(mcm => mcm.ModelCostId == modelCost.Id)
                        .ToListAsync();
                    dbContext.ModelCostMappings.RemoveRange(existingMappings);
                    
                    // Add new mappings
                    foreach (var mappingId in modelCost.ModelProviderMappingIds)
                    {
                        var modelCostMapping = new ModelCostMapping
                        {
                            ModelCostId = modelCost.Id,
                            ModelProviderMappingId = mappingId,
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true
                        };
                        dbContext.ModelCostMappings.Add(modelCostMapping);
                    }
                    await dbContext.SaveChangesAsync();
                }

                if (result)
                {
                    // Publish ModelCostChanged event for cache invalidation and cross-service coordination
                    if (changedProperties.Any())
                    {
                        await PublishEventAsync(
                            new ModelCostChanged
                            {
                                ModelCostId = modelCost.Id,
                                CostName = existingModelCost.CostName,
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
        public async Task<string> ExportModelCostsAsync(string format, int? providerId = null)
        {
            IEnumerable<ModelCost> modelCosts;
            if (providerId != null)
            {
                modelCosts = await _modelCostRepository.GetByProviderAsync(providerId.Value);
            }
            else
            {
                modelCosts = await _modelCostRepository.GetAllAsync();
            }

            format = format?.ToLowerInvariant() ?? "json";

            return format switch
            {
                "json" => GenerateJsonExport(modelCosts.ToList()),
                "csv" => GenerateCsvExport(modelCosts.ToList()),
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
                        // Check if model cost with the same name already exists
                        var existingModelCost = await _modelCostRepository.GetByCostNameAsync(modelCost.CostName);

                        if (existingModelCost != null)
                        {
                            // Update existing model cost
                            var updateDto = new UpdateModelCostDto
                            {
                                Id = existingModelCost.Id,
                                CostName = modelCost.CostName,
                                PricingModel = modelCost.PricingModel,
                                PricingConfiguration = modelCost.PricingConfiguration,
                                InputCostPerMillionTokens = modelCost.InputCostPerMillionTokens,
                                OutputCostPerMillionTokens = modelCost.OutputCostPerMillionTokens,
                                EmbeddingCostPerMillionTokens = modelCost.EmbeddingCostPerMillionTokens,
                                ImageCostPerImage = modelCost.ImageCostPerImage,
                                AudioCostPerMinute = modelCost.AudioCostPerMinute,
                                AudioCostPerKCharacters = modelCost.AudioCostPerKCharacters,
                                AudioInputCostPerMinute = modelCost.AudioInputCostPerMinute,
                                AudioOutputCostPerMinute = modelCost.AudioOutputCostPerMinute,
                                VideoCostPerSecond = modelCost.VideoCostPerSecond,
                                VideoResolutionMultipliers = modelCost.VideoResolutionMultipliers,
                                ImageResolutionMultipliers = modelCost.ImageResolutionMultipliers,
                                BatchProcessingMultiplier = modelCost.BatchProcessingMultiplier,
                                SupportsBatchProcessing = modelCost.SupportsBatchProcessing,
                                CostPerSearchUnit = modelCost.CostPerSearchUnit,
                                CostPerInferenceStep = modelCost.CostPerInferenceStep,
                                DefaultInferenceSteps = modelCost.DefaultInferenceSteps
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
                        result.Errors.Add($"Failed to import model cost '{modelCost.CostName}': {ex.Message}");
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
                CostName = mc.CostName,
                PricingModel = mc.PricingModel,
                PricingConfiguration = mc.PricingConfiguration,
                InputCostPerMillionTokens = mc.InputCostPerMillionTokens,
                OutputCostPerMillionTokens = mc.OutputCostPerMillionTokens,
                EmbeddingCostPerMillionTokens = mc.EmbeddingCostPerMillionTokens,
                ImageCostPerImage = mc.ImageCostPerImage,
                AudioCostPerMinute = mc.AudioCostPerMinute,
                AudioCostPerKCharacters = mc.AudioCostPerKCharacters,
                AudioInputCostPerMinute = mc.AudioInputCostPerMinute,
                AudioOutputCostPerMinute = mc.AudioOutputCostPerMinute,
                VideoCostPerSecond = mc.VideoCostPerSecond,
                VideoResolutionMultipliers = mc.VideoResolutionMultipliers,
                ImageResolutionMultipliers = mc.ImageResolutionMultipliers,
                BatchProcessingMultiplier = mc.BatchProcessingMultiplier,
                SupportsBatchProcessing = mc.SupportsBatchProcessing,
                CostPerSearchUnit = mc.CostPerSearchUnit,
                CostPerInferenceStep = mc.CostPerInferenceStep,
                DefaultInferenceSteps = mc.DefaultInferenceSteps
            });

            return JsonSerializer.Serialize(exportData, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        private string GenerateCsvExport(List<ModelCost> modelCosts)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Cost Name,Pricing Model,Pricing Configuration,Input Cost (per million tokens),Output Cost (per million tokens),Embedding Cost (per million tokens),Image Cost (per image),Audio Cost (per minute),Audio Cost (per 1K chars),Audio Input Cost (per minute),Audio Output Cost (per minute),Video Cost (per second),Video Resolution Multipliers,Image Resolution Multipliers,Batch Processing Multiplier,Supports Batch Processing,Search Unit Cost (per 1K units),Inference Step Cost,Default Inference Steps");

            foreach (var modelCost in modelCosts.OrderBy(mc => mc.CostName))
            {
                csv.AppendLine($"{EscapeCsvValue(modelCost.CostName)}," +
                    $"{modelCost.PricingModel}," +
                    $"{EscapeCsvValue(modelCost.PricingConfiguration ?? "")}," +
                    $"{modelCost.InputCostPerMillionTokens:F6}," +
                    $"{modelCost.OutputCostPerMillionTokens:F6}," +
                    $"{(modelCost.EmbeddingCostPerMillionTokens.HasValue ? modelCost.EmbeddingCostPerMillionTokens.Value.ToString("F6") : "")}," +
                    $"{(modelCost.ImageCostPerImage?.ToString("F4") ?? "")}," +
                    $"{(modelCost.AudioCostPerMinute?.ToString("F4") ?? "")}," +
                    $"{(modelCost.AudioCostPerKCharacters?.ToString("F4") ?? "")}," +
                    $"{(modelCost.AudioInputCostPerMinute?.ToString("F4") ?? "")}," +
                    $"{(modelCost.AudioOutputCostPerMinute?.ToString("F4") ?? "")}," +
                    $"{(modelCost.VideoCostPerSecond?.ToString("F4") ?? "")}," +
                    $"{EscapeCsvValue(modelCost.VideoResolutionMultipliers ?? "")}," +
                    $"{EscapeCsvValue(modelCost.ImageResolutionMultipliers ?? "")}," +
                    $"{(modelCost.BatchProcessingMultiplier?.ToString("F4") ?? "")}," +
                    $"{(modelCost.SupportsBatchProcessing ? "Yes" : "No")}," +
                    $"{(modelCost.CostPerSearchUnit?.ToString("F6") ?? "")}," +
                    $"{(modelCost.CostPerInferenceStep?.ToString("F6") ?? "")}," +
                    $"{(modelCost.DefaultInferenceSteps?.ToString() ?? "")}");
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
                    ImageCostPerImage = d.ImageCostPerImage,
                    AudioCostPerMinute = d.AudioCostPerMinute,
                    AudioCostPerKCharacters = d.AudioCostPerKCharacters,
                    AudioInputCostPerMinute = d.AudioInputCostPerMinute,
                    AudioOutputCostPerMinute = d.AudioOutputCostPerMinute,
                    VideoCostPerSecond = d.VideoCostPerSecond,
                    VideoResolutionMultipliers = d.VideoResolutionMultipliers,
                    ImageResolutionMultipliers = d.ImageResolutionMultipliers,
                    BatchProcessingMultiplier = d.BatchProcessingMultiplier,
                    SupportsBatchProcessing = d.SupportsBatchProcessing,
                    CostPerSearchUnit = d.CostPerSearchUnit,
                    CostPerInferenceStep = d.CostPerInferenceStep,
                    DefaultInferenceSteps = d.DefaultInferenceSteps
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
                        CostName = UnescapeCsvValue(parts[0]),
                        PricingModel = parts.Length > 1 && Enum.TryParse<PricingModel>(parts[1], out var pricingModel) ? pricingModel : PricingModel.Standard,
                        PricingConfiguration = parts.Length > 2 ? UnescapeCsvValue(parts[2]) : null,
                        InputCostPerMillionTokens = parts.Length > 3 && decimal.TryParse(parts[3], out var inputCost) ? inputCost : 0,
                        OutputCostPerMillionTokens = parts.Length > 4 && decimal.TryParse(parts[4], out var outputCost) ? outputCost : 0,
                        EmbeddingCostPerMillionTokens = parts.Length > 5 && decimal.TryParse(parts[5], out var embeddingCost) ? embeddingCost : null,
                        ImageCostPerImage = parts.Length > 6 && decimal.TryParse(parts[6], out var imageCost) ? imageCost : null,
                        AudioCostPerMinute = parts.Length > 7 && decimal.TryParse(parts[7], out var audioCost) ? audioCost : null,
                        AudioCostPerKCharacters = parts.Length > 8 && decimal.TryParse(parts[8], out var audioKCharCost) ? audioKCharCost : null,
                        AudioInputCostPerMinute = parts.Length > 9 && decimal.TryParse(parts[9], out var audioInputCost) ? audioInputCost : null,
                        AudioOutputCostPerMinute = parts.Length > 10 && decimal.TryParse(parts[10], out var audioOutputCost) ? audioOutputCost : null,
                        VideoCostPerSecond = parts.Length > 11 && decimal.TryParse(parts[11], out var videoCost) ? videoCost : null,
                        VideoResolutionMultipliers = parts.Length > 12 ? UnescapeCsvValue(parts[12]) : null,
                        ImageResolutionMultipliers = parts.Length > 13 ? UnescapeCsvValue(parts[13]) : null,
                        BatchProcessingMultiplier = parts.Length > 14 && decimal.TryParse(parts[14], out var batchMultiplier) ? batchMultiplier : null,
                        SupportsBatchProcessing = parts.Length > 15 && (parts[15].Trim().ToLower() == "yes" || parts[15].Trim().ToLower() == "true"),
                        CostPerSearchUnit = parts.Length > 16 && decimal.TryParse(parts[16], out var searchUnitCost) ? searchUnitCost : null,
                        CostPerInferenceStep = parts.Length > 17 && decimal.TryParse(parts[17], out var inferenceStepCost) ? inferenceStepCost : null,
                        DefaultInferenceSteps = parts.Length > 18 && int.TryParse(parts[18], out var defaultSteps) ? defaultSteps : null
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

}

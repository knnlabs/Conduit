using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Events;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service implementation for managing model costs - Import/Export functionality
    /// </summary>
    public partial class AdminModelCostService
    {
        /// <inheritdoc />
        public async Task<int> ImportModelCostsAsync(IEnumerable<CreateModelCostDto> modelCosts)
        {
            if (modelCosts == null)
            {
                throw new ArgumentNullException(nameof(modelCosts));
            }

            if (modelCosts.Count() == 0)
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
    }
}
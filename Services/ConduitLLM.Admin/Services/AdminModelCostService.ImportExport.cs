using ConduitLLM.Core.Extensions;
using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;
using ConduitLLM.Functions.Utilities;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service implementation for managing model costs - Import/Export functionality
    /// </summary>
    public partial class AdminModelCostService
    {
        /// <inheritdoc />
        public async Task<BulkImportResult> ImportModelCostsAsync(IEnumerable<CreateModelCostDto> modelCosts)
        {
            if (modelCosts == null)
            {
                throw new ArgumentNullException(nameof(modelCosts));
            }

            if (!modelCosts.Any())
            {
                return new BulkImportResult();
            }

            try
            {
                var result = new BulkImportResult();
                var totalCount = modelCosts.Count();

                // Process each model cost
                foreach (var modelCost in modelCosts)
                {
                    try
                    {
                        ModelPricingConfigurationValidator.Validate(
                            modelCost.PricingModel,
                            StructuredJson.SerializeObject(modelCost.PricingConfiguration));

                        // Check if a model cost with the same name already exists
                        var existingModelCost = await _modelCostRepository.GetByCostNameAsync(modelCost.CostName);

                        if (existingModelCost != null)
                        {
                            // Update existing model cost
                            var updateDto = new UpdateModelCostDto
                            {
                                CostName = modelCost.CostName,
                                PricingModel = modelCost.PricingModel,
                                PricingConfiguration = modelCost.PricingConfiguration,
                                InputCostPerMillionTokens = modelCost.InputCostPerMillionTokens,
                                OutputCostPerMillionTokens = modelCost.OutputCostPerMillionTokens,
                                EmbeddingCostPerMillionTokens = modelCost.EmbeddingCostPerMillionTokens,
                                BatchProcessingMultiplier = modelCost.BatchProcessingMultiplier,
                                SupportsBatchProcessing = modelCost.SupportsBatchProcessing,
                                CostPerSearchUnit = modelCost.CostPerSearchUnit,
                                CachedInputCostPerMillionTokens = modelCost.CachedInputCostPerMillionTokens,
                                CachedInputWriteCostPerMillionTokens = modelCost.CachedInputWriteCostPerMillionTokens
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

                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        result.FailureCount++;
                        result.Errors.Add($"Failed to import model cost '{modelCost.CostName}': {ex.Message}");
                        _logger.LogWarning(ex,
                            "Error importing model cost with name '{CostName}'",
                            LoggingSanitizer.S(modelCost.CostName));
                        // Continue with next model cost
                    }
                }

                _logger.LogInformation("Imported {Imported} model costs ({Failed} failed out of {Total})",
                    result.SuccessCount, result.FailureCount, totalCount);
                return result;
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
            _logger.LogDebug("Exporting model costs as {Format}{ProviderFilter}",
                format ?? "json",
                providerId.HasValue ? $" for provider {providerId}" : "");

            IEnumerable<ModelCost> modelCosts;
            if (providerId != null)
            {
                modelCosts = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                    _modelCostRepository.GetByProviderPaginatedAsync, providerId.Value);
            }
            else
            {
                modelCosts = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                    _modelCostRepository.GetPaginatedAsync);
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

                // Keep parsed file imports on the same write and event-publishing path as
                // DTO imports so Gateway model-cost caches are invalidated consistently.
                result = await ImportModelCostsAsync(modelCosts);
            }
            catch (Exception ex)
            {
                result.FailureCount++;
                result.Errors.Add($"Failed to parse import data: {ex.Message}");
                _logger.LogError(ex, "Failed to parse {Format} import data", format);
            }

            _logger.LogInformation("Model cost {Format} import completed: {Success} succeeded, {Failed} failed",
                format, result.SuccessCount, result.FailureCount);
            return result;
        }
    }
}

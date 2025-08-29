using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Admin.Extensions
{
    /// <summary>
    /// Extension methods for repository interfaces to provide additional functionality needed by Admin services
    /// </summary>
    public static class RepositoryExtensions
    {
        /// <summary>
        /// Gets daily costs from request logs within a specified date range
        /// </summary>
        /// <param name="repository">The request log repository</param>
        /// <param name="startDate">The start date (inclusive)</param>
        /// <param name="endDate">The end date (inclusive)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A list of daily costs as tuples of (Date, Cost)</returns>
        public static async Task<List<(DateTime Date, decimal Cost)>> GetDailyCostsAsync(
            this IRequestLogRepository repository,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            // Get the logs for the date range
            var logs = await repository.GetByDateRangeAsync(startDate, endDate, cancellationToken);

            // Group by date and calculate daily costs
            var dailyCosts = logs
                .GroupBy(l => l.Timestamp.Date)
                .Select(g => new { Date = g.Key, Cost = g.Sum(l => l.Cost) })
                .OrderBy(d => d.Date)
                .Select(d => (d.Date, d.Cost))
                .ToList();

            return dailyCosts;
        }

        /// <summary>
        /// Gets virtual key information by key name
        /// </summary>
        /// <param name="repository">The virtual key repository</param>
        /// <param name="keyName">The name of the key to find</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The virtual key entity or null if not found</returns>
        public static async Task<VirtualKey?> GetByNameAsync(
            this IVirtualKeyRepository repository,
            string keyName,
            CancellationToken cancellationToken = default)
        {
            var keys = await repository.GetAllAsync(cancellationToken);
            return keys.FirstOrDefault(k => k.KeyName.Equals(keyName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets the spend history for a virtual key within a date range
        /// </summary>
        /// <param name="repository">The spend history repository</param>
        /// <param name="virtualKeyId">The ID of the virtual key</param>
        /// <param name="startDate">The start date (inclusive)</param>
        /// <param name="endDate">The end date (inclusive)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A list of spend history entries</returns>
        public static async Task<List<VirtualKeySpendHistory>> GetByKeyIdAndDateRangeAsync(
            this IVirtualKeySpendHistoryRepository repository,
            int virtualKeyId,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            var history = await repository.GetByVirtualKeyIdAsync(virtualKeyId, cancellationToken);
            return history
                .Where(h => h.Timestamp >= startDate && h.Timestamp <= endDate)
                .OrderBy(h => h.Timestamp)
                .ToList();
        }

        /// <summary>
        /// Gets the remaining budget for a virtual key
        /// </summary>
        /// <param name="virtualKey">The virtual key entity</param>
        /// <returns>The remaining budget or null if no budget is set</returns>
        [Obsolete("Budget tracking is now at the group level. Use VirtualKeyGroup.Balance instead.")]
        public static decimal? GetRemainingBudget(this VirtualKey virtualKey)
        {
            // This method is deprecated - budget tracking is now at the group level
            return null;
        }

        /// <summary>
        /// Maps a ModelProviderMapping entity to a ModelProviderMappingDto
        /// </summary>
        /// <param name="mapping">The entity to map</param>
        /// <returns>The mapped DTO</returns>
        public static ModelProviderMappingDto ToDto(this ConduitLLM.Configuration.Entities.ModelProviderMapping mapping)
        {
            if (mapping == null)
            {
                throw new ArgumentNullException(nameof(mapping));
            }

            return new ConduitLLM.Configuration.DTOs.ModelProviderMappingDto
            {
                Id = mapping.Id,
                ModelAlias = mapping.ModelAlias,
                ModelId = mapping.ModelId,
                ProviderModelId = mapping.ProviderModelId,
                ProviderId = mapping.ProviderId,
                Provider = mapping.Provider != null ? new ProviderReferenceDto
                {
                    Id = mapping.Provider.Id,
                    ProviderType = mapping.Provider.ProviderType,
                    DisplayName = mapping.Provider.ProviderName,
                    IsEnabled = mapping.Provider.IsEnabled
                } : null,
                Priority = 0, // Default priority if not available in entity
                IsEnabled = mapping.IsEnabled,
                MaxContextTokensOverride = mapping.MaxContextTokensOverride,
                ProviderVariation = mapping.ProviderVariation,
                QualityScore = mapping.QualityScore,
                IsDefault = mapping.IsDefault,
                DefaultCapabilityType = mapping.DefaultCapabilityType,
                CreatedAt = mapping.CreatedAt,
                UpdatedAt = mapping.UpdatedAt,
                Notes = null // Not available in entity
            };
        }

        /// <summary>
        /// Maps a ModelProviderMappingDto to a ModelProviderMapping entity
        /// </summary>
        /// <param name="dto">The DTO to map</param>
        /// <returns>The mapped entity</returns>
        public static ConduitLLM.Configuration.Entities.ModelProviderMapping ToEntity(this ModelProviderMappingDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            return new ConduitLLM.Configuration.Entities.ModelProviderMapping
            {
                Id = dto.Id,
                ModelAlias = dto.ModelAlias,
                ModelId = dto.ModelId,
                ProviderModelId = dto.ProviderModelId,
                ProviderId = dto.ProviderId,
                IsEnabled = dto.IsEnabled,
                MaxContextTokensOverride = dto.MaxContextTokensOverride,
                ProviderVariation = dto.ProviderVariation,
                QualityScore = dto.QualityScore,
                IsDefault = dto.IsDefault,
                DefaultCapabilityType = dto.DefaultCapabilityType,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt
            };
        }





        /// <summary>
        /// Maps a Notification entity to a NotificationDto
        /// </summary>
        /// <param name="notification">The entity to map</param>
        /// <param name="virtualKeyName">Optional virtual key name if available</param>
        /// <returns>The mapped DTO</returns>
        public static NotificationDto ToDto(this Notification notification, string? virtualKeyName = null)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            return new NotificationDto
            {
                Id = notification.Id,
                VirtualKeyId = notification.VirtualKeyId,
                VirtualKeyName = virtualKeyName ?? notification.VirtualKey?.KeyName,
                Type = notification.Type,
                Severity = notification.Severity,
                Message = notification.Message,
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt
            };
        }

        /// <summary>
        /// Maps a CreateNotificationDto to a Notification entity
        /// </summary>
        /// <param name="dto">The DTO to map</param>
        /// <returns>The mapped entity</returns>
        public static Notification ToEntity(this CreateNotificationDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            return new Notification
            {
                VirtualKeyId = dto.VirtualKeyId,
                Type = dto.Type,
                Severity = dto.Severity,
                Message = dto.Message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
        }
        /// <summary>
        /// Maps a GlobalSetting entity to a GlobalSettingDto
        /// </summary>
        /// <param name="setting">The entity to map</param>
        /// <returns>The mapped DTO</returns>
        public static GlobalSettingDto ToDto(this GlobalSetting setting)
        {
            if (setting == null)
            {
                throw new ArgumentNullException(nameof(setting));
            }

            return new GlobalSettingDto
            {
                Id = setting.Id,
                Key = setting.Key,
                Value = setting.Value,
                Description = setting.Description,
                CreatedAt = setting.CreatedAt,
                UpdatedAt = setting.UpdatedAt
            };
        }

        /// <summary>
        /// Maps a CreateGlobalSettingDto to a GlobalSetting entity
        /// </summary>
        /// <param name="dto">The DTO to map</param>
        /// <returns>The mapped entity</returns>
        public static GlobalSetting ToEntity(this CreateGlobalSettingDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            return new GlobalSetting
            {
                Key = dto.Key,
                Value = dto.Value,
                Description = dto.Description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Maps an UpdateGlobalSettingDto to a GlobalSetting entity
        /// </summary>
        /// <param name="dto">The DTO to map</param>
        /// <param name="entity">The existing entity to update</param>
        /// <returns>The updated entity</returns>
        public static GlobalSetting UpdateFrom(this GlobalSetting entity, UpdateGlobalSettingDto dto)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            entity.Value = dto.Value;
            entity.Description = dto.Description;
            entity.UpdatedAt = DateTime.UtcNow;

            return entity;
        }


        /// <summary>
        /// Maps a ModelCost entity to a ModelCostDto
        /// </summary>
        /// <param name="modelCost">The entity to map</param>
        /// <returns>The mapped DTO</returns>
        public static ModelCostDto ToDto(this ModelCost modelCost)
        {
            if (modelCost == null)
            {
                throw new ArgumentNullException(nameof(modelCost));
            }

            return new ModelCostDto
            {
                Id = modelCost.Id,
                CostName = modelCost.CostName,
                AssociatedModelAliases = modelCost.ModelCostMappings?
                    .Where(mcm => mcm.IsActive)
                    .Select(mcm => mcm.ModelProviderMapping?.ModelAlias ?? "")
                    .Where(alias => !string.IsNullOrEmpty(alias))
                    .ToList() ?? new List<string>(),
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
                ImageQualityMultipliers = modelCost.ImageQualityMultipliers,
                CachedInputCostPerMillionTokens = modelCost.CachedInputCostPerMillionTokens,
                CachedInputWriteCostPerMillionTokens = modelCost.CachedInputWriteCostPerMillionTokens,
                CostPerSearchUnit = modelCost.CostPerSearchUnit,
                CostPerInferenceStep = modelCost.CostPerInferenceStep,
                DefaultInferenceSteps = modelCost.DefaultInferenceSteps,
                CreatedAt = modelCost.CreatedAt,
                UpdatedAt = modelCost.UpdatedAt,
                ModelType = modelCost.ModelType,
                IsActive = modelCost.IsActive,
                EffectiveDate = modelCost.EffectiveDate,
                ExpiryDate = modelCost.ExpiryDate,
                Description = modelCost.Description,
                Priority = modelCost.Priority
            };
        }

        /// <summary>
        /// Maps a CreateModelCostDto to a ModelCost entity
        /// </summary>
        /// <param name="dto">The DTO to map</param>
        /// <returns>The mapped entity</returns>
        public static ModelCost ToEntity(this CreateModelCostDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            return new ModelCost
            {
                CostName = dto.CostName,
                PricingModel = dto.PricingModel,
                PricingConfiguration = dto.PricingConfiguration,
                InputCostPerMillionTokens = dto.InputCostPerMillionTokens,
                OutputCostPerMillionTokens = dto.OutputCostPerMillionTokens,
                EmbeddingCostPerMillionTokens = dto.EmbeddingCostPerMillionTokens,
                ImageCostPerImage = dto.ImageCostPerImage,
                VideoCostPerSecond = dto.VideoCostPerSecond,
                VideoResolutionMultipliers = dto.VideoResolutionMultipliers,
                ImageResolutionMultipliers = dto.ImageResolutionMultipliers,
                BatchProcessingMultiplier = dto.BatchProcessingMultiplier,
                SupportsBatchProcessing = dto.SupportsBatchProcessing,
                ImageQualityMultipliers = dto.ImageQualityMultipliers,
                CachedInputCostPerMillionTokens = dto.CachedInputCostPerMillionTokens,
                CachedInputWriteCostPerMillionTokens = dto.CachedInputWriteCostPerMillionTokens,
                CostPerSearchUnit = dto.CostPerSearchUnit,
                CostPerInferenceStep = dto.CostPerInferenceStep,
                DefaultInferenceSteps = dto.DefaultInferenceSteps,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Updates a ModelCost entity from an UpdateModelCostDto
        /// </summary>
        /// <param name="entity">The entity to update</param>
        /// <param name="dto">The DTO with updated values</param>
        /// <returns>The updated entity</returns>
        public static ModelCost UpdateFrom(this ModelCost entity, UpdateModelCostDto dto)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            entity.CostName = dto.CostName;
            entity.PricingModel = dto.PricingModel;
            entity.PricingConfiguration = dto.PricingConfiguration;
            entity.InputCostPerMillionTokens = dto.InputCostPerMillionTokens;
            entity.OutputCostPerMillionTokens = dto.OutputCostPerMillionTokens;
            entity.EmbeddingCostPerMillionTokens = dto.EmbeddingCostPerMillionTokens;
            entity.ImageCostPerImage = dto.ImageCostPerImage;
            entity.VideoCostPerSecond = dto.VideoCostPerSecond;
            entity.VideoResolutionMultipliers = dto.VideoResolutionMultipliers;
            entity.ImageResolutionMultipliers = dto.ImageResolutionMultipliers;
            entity.BatchProcessingMultiplier = dto.BatchProcessingMultiplier;
            entity.SupportsBatchProcessing = dto.SupportsBatchProcessing;
            entity.ImageQualityMultipliers = dto.ImageQualityMultipliers;
            entity.CachedInputCostPerMillionTokens = dto.CachedInputCostPerMillionTokens;
            entity.CachedInputWriteCostPerMillionTokens = dto.CachedInputWriteCostPerMillionTokens;
            entity.CostPerSearchUnit = dto.CostPerSearchUnit;
            entity.CostPerInferenceStep = dto.CostPerInferenceStep;
            entity.DefaultInferenceSteps = dto.DefaultInferenceSteps;
            entity.UpdatedAt = DateTime.UtcNow;

            return entity;
        }
    }
}

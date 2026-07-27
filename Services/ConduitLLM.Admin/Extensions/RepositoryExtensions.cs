using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Functions.Utilities;
namespace ConduitLLM.Admin.Extensions
{
    /// <summary>
    /// Extension methods for repository interfaces to provide additional functionality needed by Admin services
    /// </summary>
    public static class RepositoryExtensions
    {
        /// <summary>
        /// Gets daily costs from request logs within a specified date range.
        /// Uses database-level aggregation instead of loading all logs into memory.
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
            var aggregations = await repository.GetCostsByDateAsync(startDate, endDate, cancellationToken);
            return aggregations
                .Select(a => (a.Date, a.TotalCost))
                .ToList();
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
            var keys = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                repository.GetPaginatedAsync, cancellationToken: cancellationToken);
            return keys.FirstOrDefault(k => k.KeyName.Equals(keyName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets the spend history for a virtual key within a date range.
        /// Delegates to the repository's database-level filtered query.
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
            // Use the repository's DB-level filtered query instead of loading all history then filtering in memory
            var history = await repository.GetByVirtualKeyAndDateRangeAsync(virtualKeyId, startDate, endDate, cancellationToken);
            return history.OrderBy(h => h.Timestamp).ToList();
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
                ProviderId = notification.ProviderId,
                ProviderKeyCredentialId = notification.ProviderKeyCredentialId,
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

            if (dto.Value is not null) entity.Value = dto.Value;
            if (dto.Description is not null) entity.Description = dto.Description;
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
                AssociatedModelAliases = modelCost.ModelProviderTypeAssociations?
                    .Where(mpta => mpta.IsEnabled)
                    .Select(mpta => mpta.Identifier)
                    .Where(identifier => !string.IsNullOrEmpty(identifier))
                    .ToList() ?? new List<string>(),
                ModelProviderTypeAssociationIds = modelCost.ModelProviderTypeAssociations?
                    .Select(mpta => mpta.Id)
                    .ToList() ?? new List<int>(),
                PricingModel = modelCost.PricingModel,
                PricingConfiguration = StructuredJson.ParseObject(modelCost.PricingConfiguration),
                InputCostPerMillionTokens = modelCost.InputCostPerMillionTokens,
                OutputCostPerMillionTokens = modelCost.OutputCostPerMillionTokens,
                ReasoningCostPerMillionTokens = modelCost.ReasoningCostPerMillionTokens,
                EmbeddingCostPerMillionTokens = modelCost.EmbeddingCostPerMillionTokens,
                BatchProcessingMultiplier = modelCost.BatchProcessingMultiplier,
                SupportsBatchProcessing = modelCost.SupportsBatchProcessing,
                CachedInputCostPerMillionTokens = modelCost.CachedInputCostPerMillionTokens,
                CachedInputWriteCostPerMillionTokens = modelCost.CachedInputWriteCostPerMillionTokens,
                CostPerSearchUnit = modelCost.CostPerSearchUnit,
                AudioCostPerMinute = modelCost.AudioCostPerMinute,
                AudioCostPerThousandCharacters = modelCost.AudioCostPerThousandCharacters,
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
                PricingConfiguration = StructuredJson.SerializeObject(dto.PricingConfiguration),
                InputCostPerMillionTokens = dto.InputCostPerMillionTokens,
                OutputCostPerMillionTokens = dto.OutputCostPerMillionTokens,
                ReasoningCostPerMillionTokens = dto.ReasoningCostPerMillionTokens,
                EmbeddingCostPerMillionTokens = dto.EmbeddingCostPerMillionTokens,
                BatchProcessingMultiplier = dto.BatchProcessingMultiplier,
                SupportsBatchProcessing = dto.SupportsBatchProcessing,
                CachedInputCostPerMillionTokens = dto.CachedInputCostPerMillionTokens,
                CachedInputWriteCostPerMillionTokens = dto.CachedInputWriteCostPerMillionTokens,
                CostPerSearchUnit = dto.CostPerSearchUnit,
                AudioCostPerMinute = dto.AudioCostPerMinute,
                AudioCostPerThousandCharacters = dto.AudioCostPerThousandCharacters,
                ModelType = dto.ModelType,
                IsActive = dto.IsActive,
                Description = dto.Description,
                Priority = dto.Priority,
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

            if (dto.CostName is not null) entity.CostName = dto.CostName;
            if (dto.PricingModel.HasValue) entity.PricingModel = dto.PricingModel.Value;
            if (dto.PricingConfiguration is not null)
                entity.PricingConfiguration = StructuredJson.SerializeObject(dto.PricingConfiguration);
            if (dto.ModelType is not null) entity.ModelType = dto.ModelType;
            if (dto.IsActive.HasValue) entity.IsActive = dto.IsActive.Value;
            if (dto.Priority.HasValue) entity.Priority = dto.Priority.Value;
            if (dto.Description is not null) entity.Description = dto.Description;
            if (dto.InputCostPerMillionTokens.HasValue)
                entity.InputCostPerMillionTokens = dto.InputCostPerMillionTokens.Value;
            if (dto.OutputCostPerMillionTokens.HasValue)
                entity.OutputCostPerMillionTokens = dto.OutputCostPerMillionTokens.Value;
            if (dto.ReasoningCostPerMillionTokens.HasValue)
                entity.ReasoningCostPerMillionTokens = dto.ReasoningCostPerMillionTokens;
            if (dto.EmbeddingCostPerMillionTokens.HasValue)
                entity.EmbeddingCostPerMillionTokens = dto.EmbeddingCostPerMillionTokens;
            if (dto.BatchProcessingMultiplier.HasValue)
                entity.BatchProcessingMultiplier = dto.BatchProcessingMultiplier;
            if (dto.SupportsBatchProcessing.HasValue)
                entity.SupportsBatchProcessing = dto.SupportsBatchProcessing.Value;
            if (dto.CachedInputCostPerMillionTokens.HasValue)
                entity.CachedInputCostPerMillionTokens = dto.CachedInputCostPerMillionTokens;
            if (dto.CachedInputWriteCostPerMillionTokens.HasValue)
                entity.CachedInputWriteCostPerMillionTokens = dto.CachedInputWriteCostPerMillionTokens;
            if (dto.CostPerSearchUnit.HasValue) entity.CostPerSearchUnit = dto.CostPerSearchUnit;
            if (dto.AudioCostPerMinute.HasValue) entity.AudioCostPerMinute = dto.AudioCostPerMinute;
            if (dto.AudioCostPerThousandCharacters.HasValue)
                entity.AudioCostPerThousandCharacters = dto.AudioCostPerThousandCharacters;
            entity.UpdatedAt = DateTime.UtcNow;

            return entity;
        }
    }
}

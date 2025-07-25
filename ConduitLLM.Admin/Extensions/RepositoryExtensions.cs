using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;

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
        public static decimal? GetRemainingBudget(this VirtualKey virtualKey)
        {
            if (!virtualKey.MaxBudget.HasValue)
            {
                return null;
            }

            return Math.Max(0, virtualKey.MaxBudget.Value - virtualKey.CurrentSpend);
        }

        /// <summary>
        /// Maps a ModelProviderMapping entity to a ModelProviderMappingDto
        /// </summary>
        /// <param name="mapping">The entity to map</param>
        /// <returns>The mapped DTO</returns>
        public static ModelProviderMappingDto ToDto(this ModelProviderMapping mapping)
        {
            if (mapping == null)
            {
                throw new ArgumentNullException(nameof(mapping));
            }

            return new ModelProviderMappingDto
            {
                Id = mapping.Id,
                ModelId = mapping.ModelAlias,
                ProviderModelId = mapping.ProviderModelName,
                ProviderId = mapping.ProviderCredentialId.ToString(),
                ProviderName = mapping.ProviderCredential?.ProviderName,
                Priority = 0, // Default priority if not available in entity
                IsEnabled = mapping.IsEnabled,
                Capabilities = null, // Legacy field, superseded by individual capability fields
                MaxContextLength = mapping.MaxContextTokens,
                SupportsVision = mapping.SupportsVision,
                SupportsAudioTranscription = mapping.SupportsAudioTranscription,
                SupportsTextToSpeech = mapping.SupportsTextToSpeech,
                SupportsRealtimeAudio = mapping.SupportsRealtimeAudio,
                SupportsImageGeneration = mapping.SupportsImageGeneration,
                SupportsVideoGeneration = mapping.SupportsVideoGeneration,
                TokenizerType = mapping.TokenizerType,
                SupportedVoices = mapping.SupportedVoices,
                SupportedLanguages = mapping.SupportedLanguages,
                SupportedFormats = mapping.SupportedFormats,
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
        public static ModelProviderMapping ToEntity(this ModelProviderMappingDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            int providerId = 0;
            if (!string.IsNullOrEmpty(dto.ProviderId))
            {
                int.TryParse(dto.ProviderId, out providerId);
            }

            return new ModelProviderMapping
            {
                Id = dto.Id,
                ModelAlias = dto.ModelId,
                ProviderModelName = dto.ProviderModelId,
                ProviderCredentialId = providerId,
                IsEnabled = dto.IsEnabled,
                MaxContextTokens = dto.MaxContextLength,
                SupportsVision = dto.SupportsVision,
                SupportsAudioTranscription = dto.SupportsAudioTranscription,
                SupportsTextToSpeech = dto.SupportsTextToSpeech,
                SupportsRealtimeAudio = dto.SupportsRealtimeAudio,
                SupportsImageGeneration = dto.SupportsImageGeneration,
                SupportsVideoGeneration = dto.SupportsVideoGeneration,
                TokenizerType = dto.TokenizerType,
                SupportedVoices = dto.SupportedVoices,
                SupportedLanguages = dto.SupportedLanguages,
                SupportedFormats = dto.SupportedFormats,
                IsDefault = dto.IsDefault,
                DefaultCapabilityType = dto.DefaultCapabilityType,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt
            };
        }

        /// <summary>
        /// Maps a ProviderCredential entity to a ProviderCredentialDto
        /// </summary>
        /// <param name="credential">The entity to map</param>
        /// <returns>The mapped DTO</returns>
        public static ProviderCredentialDto ToDto(this ProviderCredential credential)
        {
            if (credential == null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            return new ProviderCredentialDto
            {
                Id = credential.Id,
                ProviderName = credential.ProviderName,
                BaseUrl = credential.BaseUrl ?? string.Empty,
                IsEnabled = credential.IsEnabled,
                Organization = null, // Organization not available in entity
                CreatedAt = credential.CreatedAt,
                UpdatedAt = credential.UpdatedAt
            };
        }

        /// <summary>
        /// Maps a ProviderCredential entity to a simple ProviderDataDto
        /// </summary>
        /// <param name="credential">The entity to map</param>
        /// <returns>The mapped DTO</returns>
        public static ProviderDataDto ToProviderDataDto(this ProviderCredential credential)
        {
            if (credential == null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            return new ProviderDataDto
            {
                Id = credential.Id,
                ProviderName = credential.ProviderName
            };
        }

        /// <summary>
        /// Maps a CreateProviderCredentialDto to a ProviderCredential entity
        /// </summary>
        /// <param name="dto">The DTO to map</param>
        /// <returns>The mapped entity</returns>
        public static ProviderCredential ToEntity(this CreateProviderCredentialDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            return new ProviderCredential
            {
                ProviderName = dto.ProviderName,
                BaseUrl = dto.BaseUrl,
                IsEnabled = dto.IsEnabled,
                // Organization is not available in entity
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Updates a ProviderCredential entity from an UpdateProviderCredentialDto
        /// </summary>
        /// <param name="entity">The entity to update</param>
        /// <param name="dto">The DTO with updated values</param>
        /// <returns>The updated entity</returns>
        public static ProviderCredential UpdateFrom(this ProviderCredential entity, UpdateProviderCredentialDto dto)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            entity.BaseUrl = dto.BaseUrl;


            entity.IsEnabled = dto.IsEnabled;
            // Organization is not available in entity
            entity.UpdatedAt = DateTime.UtcNow;

            return entity;
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
        /// Maps a ProviderHealthConfiguration entity to a ProviderHealthConfigurationDto
        /// </summary>
        /// <param name="config">The entity to map</param>
        /// <returns>The mapped DTO</returns>
        public static ProviderHealthConfigurationDto ToDto(this ProviderHealthConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            return new ProviderHealthConfigurationDto
            {
                Id = config.Id,
                ProviderName = config.ProviderName,
                MonitoringEnabled = config.MonitoringEnabled,
                CheckIntervalMinutes = config.CheckIntervalMinutes,
                TimeoutSeconds = config.TimeoutSeconds,
                ConsecutiveFailuresThreshold = config.ConsecutiveFailuresThreshold,
                NotificationsEnabled = config.NotificationsEnabled,
                CustomEndpointUrl = config.CustomEndpointUrl,
                LastCheckedUtc = config.LastCheckedUtc
            };
        }

        /// <summary>
        /// Maps a CreateProviderHealthConfigurationDto to a ProviderHealthConfiguration entity
        /// </summary>
        /// <param name="dto">The DTO to map</param>
        /// <returns>The mapped entity</returns>
        public static ProviderHealthConfiguration ToEntity(this CreateProviderHealthConfigurationDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            return new ProviderHealthConfiguration
            {
                ProviderName = dto.ProviderName,
                MonitoringEnabled = dto.MonitoringEnabled,
                CheckIntervalMinutes = dto.CheckIntervalMinutes,
                TimeoutSeconds = dto.TimeoutSeconds,
                ConsecutiveFailuresThreshold = dto.ConsecutiveFailuresThreshold,
                NotificationsEnabled = dto.NotificationsEnabled,
                CustomEndpointUrl = dto.CustomEndpointUrl,
                LastCheckedUtc = null
            };
        }

        /// <summary>
        /// Updates a ProviderHealthConfiguration entity from an UpdateProviderHealthConfigurationDto
        /// </summary>
        /// <param name="entity">The entity to update</param>
        /// <param name="dto">The DTO with updated values</param>
        /// <returns>The updated entity</returns>
        public static ProviderHealthConfiguration UpdateFrom(this ProviderHealthConfiguration entity, UpdateProviderHealthConfigurationDto dto)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            entity.MonitoringEnabled = dto.MonitoringEnabled;
            entity.CheckIntervalMinutes = dto.CheckIntervalMinutes;
            entity.TimeoutSeconds = dto.TimeoutSeconds;
            entity.ConsecutiveFailuresThreshold = dto.ConsecutiveFailuresThreshold;
            entity.NotificationsEnabled = dto.NotificationsEnabled;
            entity.CustomEndpointUrl = dto.CustomEndpointUrl;

            return entity;
        }

        /// <summary>
        /// Maps a ProviderHealthRecord entity to a ProviderHealthRecordDto
        /// </summary>
        /// <param name="record">The entity to map</param>
        /// <returns>The mapped DTO</returns>
        public static ProviderHealthRecordDto ToDto(this ProviderHealthRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            return new ProviderHealthRecordDto
            {
                Id = record.Id,
                ProviderName = record.ProviderName,
                Status = record.Status,
                StatusMessage = record.StatusMessage,
                TimestampUtc = record.TimestampUtc,
                ResponseTimeMs = record.ResponseTimeMs,
                ErrorCategory = record.ErrorCategory,
                ErrorDetails = record.ErrorDetails,
                EndpointUrl = record.EndpointUrl
            };
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
                VideoResolutionMultipliers = modelCost.VideoResolutionMultipliers,
                BatchProcessingMultiplier = modelCost.BatchProcessingMultiplier,
                SupportsBatchProcessing = modelCost.SupportsBatchProcessing,
                ImageQualityMultipliers = modelCost.ImageQualityMultipliers,
                CachedInputTokenCost = modelCost.CachedInputTokenCost,
                CachedInputWriteCost = modelCost.CachedInputWriteCost,
                CostPerSearchUnit = modelCost.CostPerSearchUnit,
                CreatedAt = modelCost.CreatedAt,
                UpdatedAt = modelCost.UpdatedAt
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
                ModelIdPattern = dto.ModelIdPattern,
                InputTokenCost = dto.InputTokenCost,
                OutputTokenCost = dto.OutputTokenCost,
                EmbeddingTokenCost = dto.EmbeddingTokenCost,
                ImageCostPerImage = dto.ImageCostPerImage,
                AudioCostPerMinute = dto.AudioCostPerMinute,
                AudioCostPerKCharacters = dto.AudioCostPerKCharacters,
                AudioInputCostPerMinute = dto.AudioInputCostPerMinute,
                AudioOutputCostPerMinute = dto.AudioOutputCostPerMinute,
                VideoCostPerSecond = dto.VideoCostPerSecond,
                VideoResolutionMultipliers = dto.VideoResolutionMultipliers,
                BatchProcessingMultiplier = dto.BatchProcessingMultiplier,
                SupportsBatchProcessing = dto.SupportsBatchProcessing,
                ImageQualityMultipliers = dto.ImageQualityMultipliers,
                CachedInputTokenCost = dto.CachedInputTokenCost,
                CachedInputWriteCost = dto.CachedInputWriteCost,
                CostPerSearchUnit = dto.CostPerSearchUnit,
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

            entity.ModelIdPattern = dto.ModelIdPattern;
            entity.InputTokenCost = dto.InputTokenCost;
            entity.OutputTokenCost = dto.OutputTokenCost;
            entity.EmbeddingTokenCost = dto.EmbeddingTokenCost;
            entity.ImageCostPerImage = dto.ImageCostPerImage;
            entity.AudioCostPerMinute = dto.AudioCostPerMinute;
            entity.AudioCostPerKCharacters = dto.AudioCostPerKCharacters;
            entity.AudioInputCostPerMinute = dto.AudioInputCostPerMinute;
            entity.AudioOutputCostPerMinute = dto.AudioOutputCostPerMinute;
            entity.VideoCostPerSecond = dto.VideoCostPerSecond;
            entity.VideoResolutionMultipliers = dto.VideoResolutionMultipliers;
            entity.BatchProcessingMultiplier = dto.BatchProcessingMultiplier;
            entity.SupportsBatchProcessing = dto.SupportsBatchProcessing;
            entity.ImageQualityMultipliers = dto.ImageQualityMultipliers;
            entity.CachedInputTokenCost = dto.CachedInputTokenCost;
            entity.CachedInputWriteCost = dto.CachedInputWriteCost;
            entity.CostPerSearchUnit = dto.CostPerSearchUnit;
            entity.UpdatedAt = DateTime.UtcNow;

            return entity;
        }
    }
}

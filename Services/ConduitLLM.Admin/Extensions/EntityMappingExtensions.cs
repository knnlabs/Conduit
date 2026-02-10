using ConduitLLM.Admin.Models.ModelAuthors;
using ConduitLLM.Admin.Models.Models;
using ConduitLLM.Admin.Models.ModelSeries;
using ConduitLLM.Configuration.DTOs.IpFilter;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Functions.DTOs;
using ConduitLLM.Functions.Entities;

namespace ConduitLLM.Admin.Extensions
{
    /// <summary>
    /// Extension methods for converting entities to their DTO representations
    /// </summary>
    public static class EntityMappingExtensions
    {
        /// <summary>
        /// Maps a VirtualKey entity to a VirtualKeyDto
        /// </summary>
        public static VirtualKeyDto ToDto(this VirtualKey key)
        {
            return new VirtualKeyDto
            {
                Id = key.Id,
                KeyName = key.KeyName,
                KeyPrefix = GenerateKeyPrefix(key.KeyHash),
                AllowedModels = key.AllowedModels,
                VirtualKeyGroupId = key.VirtualKeyGroupId,
                IsEnabled = key.IsEnabled,
                ExpiresAt = key.ExpiresAt,
                CreatedAt = key.CreatedAt,
                UpdatedAt = key.UpdatedAt,
                Metadata = key.Metadata,
                RateLimitRpm = key.RateLimitRpm,
                RateLimitRpd = key.RateLimitRpd
            };
        }

        /// <summary>
        /// Maps an IpFilterEntity to an IpFilterDto
        /// </summary>
        public static IpFilterDto ToDto(this IpFilterEntity entity)
        {
            return new IpFilterDto
            {
                Id = entity.Id,
                FilterType = entity.FilterType,
                IpAddressOrCidr = entity.IpAddressOrCidr,
                Description = entity.Description,
                IsEnabled = entity.IsEnabled,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                CreatedBy = entity.CreatedBy,
                UpdatedBy = entity.UpdatedBy
            };
        }

        /// <summary>
        /// Maps a ModelSeries entity to a ModelSeriesDto
        /// </summary>
        public static ModelSeriesDto ToDto(this ModelSeries series)
        {
            return new ModelSeriesDto
            {
                Id = series.Id,
                AuthorId = series.AuthorId,
                AuthorName = series.Author?.Name,
                Name = series.Name,
                Description = series.Description,
                TokenizerType = series.TokenizerType,
                Parameters = series.Parameters
            };
        }

        /// <summary>
        /// Maps a Model entity to a ModelDto
        /// </summary>
        public static ModelDto ToDto(this Model model)
        {
            return new ModelDto
            {
                Id = model.Id,
                Name = model.Name,
                ModelSeriesId = model.ModelSeriesId,
                IsActive = model.IsActive,
                CreatedAt = model.CreatedAt,
                UpdatedAt = model.UpdatedAt,
                Series = model.Series?.ToDto(),
                ModelParameters = model.ModelParameters,
                SupportsChat = model.SupportsChat,
                SupportsVision = model.SupportsVision,
                SupportsImageGeneration = model.SupportsImageGeneration,
                SupportsVideoGeneration = model.SupportsVideoGeneration,
                SupportsEmbeddings = model.SupportsEmbeddings,
                SupportsFunctionCalling = model.SupportsFunctionCalling,
                SupportsStreaming = model.SupportsStreaming,
                MaxInputTokens = model.MaxInputTokens,
                MaxOutputTokens = model.MaxOutputTokens,
                TokenizerType = model.TokenizerType
            };
        }

        /// <summary>
        /// Maps a ModelAuthor entity to a ModelAuthorDto
        /// </summary>
        public static ModelAuthorDto ToDto(this ModelAuthor author)
        {
            return new ModelAuthorDto
            {
                Id = author.Id,
                Name = author.Name,
                Description = author.Description,
                WebsiteUrl = author.WebsiteUrl
            };
        }

        /// <summary>
        /// Maps a FunctionExecution entity to a FunctionExecutionDto
        /// </summary>
        public static FunctionExecutionDto ToDto(this FunctionExecution entity)
        {
            return new FunctionExecutionDto
            {
                Id = entity.Id,
                FunctionConfigurationId = entity.FunctionConfigurationId,
                VirtualKeyId = entity.VirtualKeyId,
                ExecutionMode = entity.ExecutionMode,
                State = entity.State,
                RequestedAt = entity.RequestedAt,
                StartedAt = entity.StartedAt,
                CompletedAt = entity.CompletedAt,
                Duration = entity.Duration?.TotalMilliseconds,
                RequestJson = entity.RequestJson,
                ResponseJson = entity.ResponseJson,
                ErrorMessage = entity.ErrorMessage,
                EstimatedCost = entity.EstimatedCost,
                ActualCost = entity.ActualCost,
                CostCalculationDetails = entity.CostCalculationDetails,
                RetryCount = entity.RetryCount,
                NextRetryAt = entity.NextRetryAt,
                LeasedBy = entity.LeasedBy,
                LeaseExpiryTime = entity.LeaseExpiryTime,
                Version = entity.Version,
                WebhookUrl = entity.WebhookUrl,
                WebhookDelivered = entity.WebhookDelivered,
                ProgressPercentage = entity.ProgressPercentage,
                StatusMessage = entity.StatusMessage
            };
        }

        /// <summary>
        /// Maps a FunctionCost entity to a FunctionCostDto
        /// </summary>
        public static FunctionCostDto ToDto(this FunctionCost entity)
        {
            return new FunctionCostDto
            {
                Id = entity.Id,
                CostName = entity.CostName,
                ProviderType = entity.ProviderType,
                Purpose = entity.Purpose,
                Description = entity.Description,
                BaseCost = entity.BaseCost,
                PricingModel = entity.PricingModel,
                CostPerExecution = entity.CostPerExecution,
                CostPerResult = entity.CostPerResult,
                CostPerToken = entity.CostPerToken,
                CostPerMinute = entity.CostPerMinute,
                TieredPricing = entity.TieredPricing,
                PricingConfiguration = entity.PricingConfiguration,
                IsActive = entity.IsActive,
                EffectiveDate = entity.EffectiveDate,
                ExpiryDate = entity.ExpiryDate,
                Priority = entity.Priority,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        /// <summary>
        /// Generates a key prefix for display purposes
        /// </summary>
        private static string GenerateKeyPrefix(string keyHash)
        {
            if (string.IsNullOrEmpty(keyHash))
            {
                return "condt_******...";
            }

            var prefixLength = Math.Min(6, keyHash.Length);
            var shortPrefix = keyHash.Substring(0, prefixLength).ToLower();
            return $"condt_{shortPrefix}...";
        }
    }
}

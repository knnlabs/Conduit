using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for virtual key types
    /// </summary>
    public static class VirtualKeyExtensions
    {
        /// <summary>
        /// Convert from VirtualKeyValidationInfoDto to VirtualKey entity
        /// </summary>
        /// <param name="dto">The dto to convert</param>
        /// <returns>A new VirtualKey entity</returns>
        public static VirtualKey? ToEntity(this VirtualKeyValidationInfoDto? dto)
        {
            if (dto == null)
            {
                return null;
            }

            return new VirtualKey
            {
                Id = dto.Id,
                KeyName = dto.KeyName,
                IsEnabled = dto.IsEnabled,
                CurrentSpend = dto.CurrentSpend,
                MaxBudget = dto.MaxBudget,
                ExpiresAt = dto.ExpiresAt,
                RateLimitRpm = dto.RateLimitRpm,
                RateLimitRpd = dto.RateLimitRpd,
                BudgetDuration = dto.BudgetDuration,
                BudgetStartDate = dto.BudgetStartDate,
                AllowedModels = dto.AllowedModels,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
    }
}

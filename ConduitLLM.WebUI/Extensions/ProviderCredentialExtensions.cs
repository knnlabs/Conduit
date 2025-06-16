using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for ProviderCredential and ProviderCredentialDto
    /// </summary>
    public static class ProviderCredentialExtensions
    {
        /// <summary>
        /// Convert from ProviderCredential entity to ProviderCredentialDto
        /// </summary>
        /// <param name="entity">The entity to convert</param>
        /// <returns>A ProviderCredentialDto instance, or null if the entity is null</returns>
        public static ProviderCredentialDto? ToDto(this ProviderCredential entity)
        {
            if (entity == null)
            {
                return null;
            }

            return new ProviderCredentialDto
            {
                Id = entity.Id,
                ProviderName = entity.ProviderName,
                ApiKey = entity.ApiKey ?? string.Empty,
                ApiBase = entity.BaseUrl ?? string.Empty,
                IsEnabled = entity.IsEnabled,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}

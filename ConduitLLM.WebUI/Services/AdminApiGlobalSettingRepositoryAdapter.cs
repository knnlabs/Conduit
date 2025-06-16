using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.WebUI.Interfaces;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Adapter that implements IGlobalSettingRepository using AdminApiClient for WebUI
    /// </summary>
    public class AdminApiGlobalSettingRepositoryAdapter : IGlobalSettingRepository
    {
        private readonly IAdminApiClient _adminApiClient;

        public AdminApiGlobalSettingRepositoryAdapter(IAdminApiClient adminApiClient)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
        }

        public async Task<GlobalSetting?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            // AdminApiClient doesn't support getting by ID, so we get all and filter
            var allSettings = await _adminApiClient.GetAllGlobalSettingsAsync();
            var settingDto = allSettings.FirstOrDefault(s => s.Id == id);
            return settingDto != null ? ConvertToEntity(settingDto) : null;
        }

        public async Task<GlobalSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        {
            var settingDto = await _adminApiClient.GetGlobalSettingByKeyAsync(key);
            return settingDto != null ? ConvertToEntity(settingDto) : null;
        }

        public async Task<List<GlobalSetting>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var settingDtos = await _adminApiClient.GetAllGlobalSettingsAsync();
            return settingDtos.Select(ConvertToEntity).ToList();
        }

        public async Task<int> CreateAsync(GlobalSetting globalSetting, CancellationToken cancellationToken = default)
        {
            var dto = ConvertToDto(globalSetting);
            var result = await _adminApiClient.UpsertGlobalSettingAsync(dto);
            return result?.Id ?? 0;
        }

        public async Task<bool> UpdateAsync(GlobalSetting globalSetting, CancellationToken cancellationToken = default)
        {
            var dto = ConvertToDto(globalSetting);
            var result = await _adminApiClient.UpsertGlobalSettingAsync(dto);
            return result != null;
        }

        public async Task<bool> UpsertAsync(string key, string value, string? description = null, CancellationToken cancellationToken = default)
        {
            var dto = new GlobalSettingDto
            {
                Key = key,
                Value = value,
                Description = description ?? string.Empty
            };
            var result = await _adminApiClient.UpsertGlobalSettingAsync(dto);
            return result != null;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            // Get the setting by ID first to find its key
            var setting = await GetByIdAsync(id, cancellationToken);
            if (setting == null) return false;

            return await _adminApiClient.DeleteGlobalSettingAsync(setting.Key);
        }

        public async Task<bool> DeleteByKeyAsync(string key, CancellationToken cancellationToken = default)
        {
            return await _adminApiClient.DeleteGlobalSettingAsync(key);
        }

        private static GlobalSetting ConvertToEntity(GlobalSettingDto dto)
        {
            return new GlobalSetting
            {
                Id = dto.Id,
                Key = dto.Key,
                Value = dto.Value,
                Description = dto.Description,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt
            };
        }

        private static GlobalSettingDto ConvertToDto(GlobalSetting entity)
        {
            return new GlobalSettingDto
            {
                Id = entity.Id,
                Key = entity.Key,
                Value = entity.Value,
                Description = entity.Description,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Extensions;

using Microsoft.Extensions.Logging;

using static ConduitLLM.Core.Extensions.LoggingSanitizer;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for managing global settings through the Admin API
    /// </summary>
    public class AdminGlobalSettingService : IAdminGlobalSettingService
    {
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly ILogger<AdminGlobalSettingService> _logger;

        /// <summary>
        /// Initializes a new instance of the AdminGlobalSettingService
        /// </summary>
        /// <param name="globalSettingRepository">The global setting repository</param>
        /// <param name="logger">The logger</param>
        public AdminGlobalSettingService(
            IGlobalSettingRepository globalSettingRepository,
            ILogger<AdminGlobalSettingService> logger)
        {
            _globalSettingRepository = globalSettingRepository ?? throw new ArgumentNullException(nameof(globalSettingRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<IEnumerable<GlobalSettingDto>> GetAllSettingsAsync()
        {
            try
            {
                _logger.LogInformation("Getting all global settings");

                var settings = await _globalSettingRepository.GetAllAsync();
                return settings.Select(s => s.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all global settings");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<GlobalSettingDto?> GetSettingByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation("Getting global setting with ID: {Id}", id);

                var setting = await _globalSettingRepository.GetByIdAsync(id);
                return setting?.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting global setting with ID {Id}", id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<GlobalSettingDto?> GetSettingByKeyAsync(string key)
        {
            try
            {
                _logger.LogInformation("Getting global setting with key: {Key}", key.Replace(Environment.NewLine, ""));

                var setting = await _globalSettingRepository.GetByKeyAsync(key);
                return setting?.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting global setting with key {Key}", key.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<GlobalSettingDto> CreateSettingAsync(CreateGlobalSettingDto setting)
        {
            try
            {
                _logger.LogInformation("Creating new global setting with key: {Key}", setting.Key.Replace(Environment.NewLine, ""));

                // Check if a setting with the same key already exists
                var existingSetting = await _globalSettingRepository.GetByKeyAsync(setting.Key);
                if (existingSetting != null)
                {
                    throw new InvalidOperationException($"A global setting with key '{setting.Key}' already exists");
                }

                // Convert to entity
                var entity = setting.ToEntity();

                // Save to database
                var id = await _globalSettingRepository.CreateAsync(entity);

                // Get the created setting
                var createdSetting = await _globalSettingRepository.GetByIdAsync(id);
                if (createdSetting == null)
                {
                    throw new InvalidOperationException($"Failed to retrieve newly created global setting with ID {id}");
                }

                return createdSetting.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating global setting with key {Key}", setting.Key.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateSettingAsync(UpdateGlobalSettingDto setting)
        {
            try
            {
                _logger.LogInformation("Updating global setting with ID: {Id}", setting.Id);

                // Get the existing setting
                var existingSetting = await _globalSettingRepository.GetByIdAsync(setting.Id);
                if (existingSetting == null)
                {
                    _logger.LogWarning("Global setting with ID {Id} not found", setting.Id);
                    return false;
                }

                // Update the entity
                existingSetting.UpdateFrom(setting);

                // Save changes
                return await _globalSettingRepository.UpdateAsync(existingSetting);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating global setting with ID {Id}", setting.Id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateSettingByKeyAsync(UpdateGlobalSettingByKeyDto setting)
        {
            try
            {
                _logger.LogInformation("Updating global setting with key: {Key}", setting.Key.Replace(Environment.NewLine, ""));

                // Upsert the setting
                return await _globalSettingRepository.UpsertAsync(setting.Key, setting.Value, setting.Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating global setting with key {Key}", setting.Key.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteSettingAsync(int id)
        {
            try
            {
                _logger.LogInformation("Deleting global setting with ID: {Id}", id);

                return await _globalSettingRepository.DeleteAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting global setting with ID {Id}", id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteSettingByKeyAsync(string key)
        {
            try
            {
                _logger.LogInformation("Deleting global setting with key: {Key}", key.Replace(Environment.NewLine, ""));

                return await _globalSettingRepository.DeleteByKeyAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting global setting with key {Key}", key.Replace(Environment.NewLine, ""));
                throw;
            }
        }
    }
}

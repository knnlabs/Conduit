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
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;

using MassTransit;
using Microsoft.Extensions.Logging;

using static ConduitLLM.Core.Extensions.LoggingSanitizer;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for managing global settings through the Admin API
    /// </summary>
    public class AdminGlobalSettingService : EventPublishingServiceBase, IAdminGlobalSettingService
    {
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly ILogger<AdminGlobalSettingService> _logger;

        /// <summary>
        /// Initializes a new instance of the AdminGlobalSettingService
        /// </summary>
        /// <param name="globalSettingRepository">The global setting repository</param>
        /// <param name="publishEndpoint">Optional event publishing endpoint (null if MassTransit not configured)</param>
        /// <param name="logger">The logger</param>
        public AdminGlobalSettingService(
            IGlobalSettingRepository globalSettingRepository,
            IPublishEndpoint? publishEndpoint,
            ILogger<AdminGlobalSettingService> logger)
            : base(publishEndpoint, logger)
        {
            _globalSettingRepository = globalSettingRepository ?? throw new ArgumentNullException(nameof(globalSettingRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Log event publishing configuration status
            LogEventPublishingConfiguration(nameof(AdminGlobalSettingService));
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

                // Publish GlobalSettingChanged event for cache synchronization
                await PublishEventAsync(
                    new GlobalSettingChanged
                    {
                        SettingId = createdSetting.Id,
                        SettingKey = createdSetting.Key,
                        ChangeType = "Created",
                        ChangedProperties = new[] { "Created" },
                        CorrelationId = Guid.NewGuid().ToString()
                    },
                    $"create global setting {createdSetting.Id}",
                    new { SettingKey = createdSetting.Key });

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

                // Track changed properties for event publishing
                var changedProperties = new List<string>();
                var originalKey = existingSetting.Key;
                
                // Check what properties will change
                if (setting.Value != null && existingSetting.Value != setting.Value)
                {
                    changedProperties.Add(nameof(existingSetting.Value));
                }
                
                if (setting.Description != null && existingSetting.Description != setting.Description)
                {
                    changedProperties.Add(nameof(existingSetting.Description));
                }

                // Only proceed if there are actual changes
                if (changedProperties.Count == 0)
                {
                    _logger.LogDebug("No changes detected for global setting {Id} - skipping update", setting.Id);
                    return true;
                }

                // Update the entity
                existingSetting.UpdateFrom(setting);

                // Save changes
                var result = await _globalSettingRepository.UpdateAsync(existingSetting);
                
                if (result)
                {
                    // Publish GlobalSettingChanged event for cache invalidation
                    await PublishEventAsync(
                        new GlobalSettingChanged
                        {
                            SettingId = existingSetting.Id,
                            SettingKey = originalKey,
                            ChangeType = "Updated",
                            ChangedProperties = changedProperties.ToArray(),
                            CorrelationId = Guid.NewGuid().ToString()
                        },
                        $"update global setting {setting.Id}",
                        new { SettingKey = originalKey, ChangedProperties = string.Join(", ", changedProperties) });
                }
                
                return result;
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

                // Get existing setting to determine if this is an update or create
                var existingSetting = await _globalSettingRepository.GetByKeyAsync(setting.Key);
                var isCreate = existingSetting == null;
                
                // Upsert the setting
                var result = await _globalSettingRepository.UpsertAsync(setting.Key, setting.Value, setting.Description);
                
                if (result)
                {
                    // Get the setting after upsert to get the ID
                    var updatedSetting = await _globalSettingRepository.GetByKeyAsync(setting.Key);
                    if (updatedSetting != null)
                    {
                        // Publish GlobalSettingChanged event
                        await PublishEventAsync(
                            new GlobalSettingChanged
                            {
                                SettingId = updatedSetting.Id,
                                SettingKey = setting.Key,
                                ChangeType = isCreate ? "Created" : "Updated",
                                ChangedProperties = isCreate ? new[] { "Created" } : new[] { "Value", "Description" },
                                CorrelationId = Guid.NewGuid().ToString()
                            },
                            $"{(isCreate ? "create" : "update")} global setting by key {setting.Key}",
                            new { SettingKey = setting.Key, ChangeType = isCreate ? "Created" : "Updated" });
                    }
                }
                
                return result;
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

                // Get the setting before deleting for event publishing
                var setting = await _globalSettingRepository.GetByIdAsync(id);
                if (setting == null)
                {
                    _logger.LogWarning("Global setting with ID {Id} not found", id);
                    return false;
                }

                var result = await _globalSettingRepository.DeleteAsync(id);
                
                if (result)
                {
                    // Publish GlobalSettingChanged event for cache invalidation
                    await PublishEventAsync(
                        new GlobalSettingChanged
                        {
                            SettingId = setting.Id,
                            SettingKey = setting.Key,
                            ChangeType = "Deleted",
                            ChangedProperties = new[] { "Deleted" },
                            CorrelationId = Guid.NewGuid().ToString()
                        },
                        $"delete global setting {id}",
                        new { SettingKey = setting.Key });
                }
                
                return result;
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

                // Get the setting before deleting for event publishing
                var setting = await _globalSettingRepository.GetByKeyAsync(key);
                if (setting == null)
                {
                    _logger.LogWarning("Global setting with key {Key} not found", key.Replace(Environment.NewLine, ""));
                    return false;
                }

                var result = await _globalSettingRepository.DeleteByKeyAsync(key);
                
                if (result)
                {
                    // Publish GlobalSettingChanged event for cache invalidation
                    await PublishEventAsync(
                        new GlobalSettingChanged
                        {
                            SettingId = setting.Id,
                            SettingKey = setting.Key,
                            ChangeType = "Deleted",
                            ChangedProperties = new[] { "Deleted" },
                            CorrelationId = Guid.NewGuid().ToString()
                        },
                        $"delete global setting by key {key}",
                        new { SettingKey = key });
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting global setting with key {Key}", key.Replace(Environment.NewLine, ""));
                throw;
            }
        }
    }
}

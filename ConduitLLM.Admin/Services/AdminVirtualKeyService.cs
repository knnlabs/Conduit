using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;

using MassTransit;
using Microsoft.Extensions.Logging;

using static ConduitLLM.Core.Extensions.LoggingSanitizer;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for managing virtual keys through the Admin API
    /// </summary>
    public class AdminVirtualKeyService : EventPublishingServiceBase, IAdminVirtualKeyService
    {
        private readonly IVirtualKeyRepository _virtualKeyRepository;
        private readonly IVirtualKeySpendHistoryRepository _spendHistoryRepository;
        private readonly IVirtualKeyGroupRepository _groupRepository;
        private readonly IVirtualKeyCache? _cache; // Optional cache for invalidation
        private readonly IMediaLifecycleService? _mediaLifecycleService; // Optional media lifecycle service
        private readonly IModelProviderMappingRepository _modelProviderMappingRepository;
        private readonly IModelCapabilityService _modelCapabilityService;
        private readonly ILogger<AdminVirtualKeyService> _logger;
        private const int KeyLengthBytes = 32; // Generate a 256-bit key

        /// <summary>
        /// Initializes a new instance of the AdminVirtualKeyService class
        /// </summary>
        /// <param name="virtualKeyRepository">The virtual key repository</param>
        /// <param name="spendHistoryRepository">The spend history repository</param>
        /// <param name="cache">Optional Redis cache for immediate invalidation (null if not configured)</param>
        /// <param name="publishEndpoint">Optional event publishing endpoint (null if MassTransit not configured)</param>
        /// <param name="logger">The logger</param>
        /// <param name="modelProviderMappingRepository">The model provider mapping repository</param>
        /// <param name="modelCapabilityService">The model capability service</param>
        /// <param name="mediaLifecycleService">Optional media lifecycle service for cleaning up associated media files (null if not configured)</param>
        /// <param name="groupRepository">The virtual key group repository</param>
        public AdminVirtualKeyService(
            IVirtualKeyRepository virtualKeyRepository,
            IVirtualKeySpendHistoryRepository spendHistoryRepository,
            IVirtualKeyGroupRepository groupRepository,
            IVirtualKeyCache? cache,
            IPublishEndpoint? publishEndpoint,
            ILogger<AdminVirtualKeyService> logger,
            IModelProviderMappingRepository modelProviderMappingRepository,
            IModelCapabilityService modelCapabilityService,
            IMediaLifecycleService? mediaLifecycleService = null)
            : base(publishEndpoint, logger)
        {
            _virtualKeyRepository = virtualKeyRepository ?? throw new ArgumentNullException(nameof(virtualKeyRepository));
            _spendHistoryRepository = spendHistoryRepository ?? throw new ArgumentNullException(nameof(spendHistoryRepository));
            _groupRepository = groupRepository ?? throw new ArgumentNullException(nameof(groupRepository));
            _cache = cache; // Optional - can be null if Redis not configured
            _mediaLifecycleService = mediaLifecycleService; // Optional - can be null if media lifecycle management not configured
            _modelProviderMappingRepository = modelProviderMappingRepository ?? throw new ArgumentNullException(nameof(modelProviderMappingRepository));
            _modelCapabilityService = modelCapabilityService ?? throw new ArgumentNullException(nameof(modelCapabilityService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Log event publishing configuration status
            LogEventPublishingConfiguration(nameof(AdminVirtualKeyService));
        }

        /// <inheritdoc />
        public async Task<CreateVirtualKeyResponseDto> GenerateVirtualKeyAsync(CreateVirtualKeyRequestDto request)
        {
            _logger.LogInformation("Generating new virtual key with name: {KeyName}", (request.KeyName ?? "").Replace(Environment.NewLine, ""));

            // Generate a secure random key
            var keyBytes = new byte[KeyLengthBytes];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(keyBytes);
            var apiKey = Convert.ToBase64String(keyBytes);

            // Add the standard prefix
            apiKey = VirtualKeyConstants.KeyPrefix + apiKey;

            // Hash the key for storage
            var keyHash = ComputeSha256Hash(apiKey);

            // Verify the group exists
            var existingGroup = await _groupRepository.GetByIdAsync(request.VirtualKeyGroupId);
            if (existingGroup == null)
            {
                throw new InvalidOperationException($"Virtual key group {request.VirtualKeyGroupId} not found. Ensure the group exists before creating keys.");
            }
            
            // Warn if the group has zero balance
            if (existingGroup.Balance <= 0)
            {
                _logger.LogWarning(
                    "Virtual key group {GroupId} has zero balance. Keys in this group cannot make API calls until funded.",
                    request.VirtualKeyGroupId);
            }
            
            var groupId = request.VirtualKeyGroupId;

            // Create the virtual key entity
            var virtualKey = new VirtualKey
            {
                KeyName = request.KeyName ?? string.Empty,
                KeyHash = keyHash,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ExpiresAt = request.ExpiresAt,
                VirtualKeyGroupId = groupId,
                IsEnabled = true,
                AllowedModels = request.AllowedModels,
                Metadata = request.Metadata,
                RateLimitRpm = request.RateLimitRpm,
                RateLimitRpd = request.RateLimitRpd
            };

            // Save to database
            var id = await _virtualKeyRepository.CreateAsync(virtualKey);

            // The entity is saved with an ID, now retrieve it to get all properties
            virtualKey = await _virtualKeyRepository.GetByIdAsync(id);
            if (virtualKey == null)
            {
                throw new InvalidOperationException($"Failed to retrieve newly created virtual key with ID {id}");
            }

            // No longer need to initialize spend history - budget is tracked at group level

            // Publish VirtualKeyCreated event for cache synchronization
            // This is critical for the Core API to recognize the new key
            await PublishEventAsync(
                new VirtualKeyCreated
                {
                    KeyId = virtualKey.Id,
                    KeyHash = virtualKey.KeyHash,
                    KeyName = virtualKey.KeyName,
                    CreatedAt = virtualKey.CreatedAt,
                    IsEnabled = virtualKey.IsEnabled,
                    AllowedModels = virtualKey.AllowedModels,
                    VirtualKeyGroupId = virtualKey.VirtualKeyGroupId,
                    CorrelationId = Guid.NewGuid().ToString()
                },
                $"create virtual key {virtualKey.Id}",
                new { KeyName = virtualKey.KeyName });

            // Map to response DTO
            var keyDto = MapToDto(virtualKey);

            // Return response with the generated key
            return new CreateVirtualKeyResponseDto
            {
                VirtualKey = apiKey,
                KeyInfo = keyDto
            };
        }

        /// <inheritdoc />
        public async Task<VirtualKeyDto?> GetVirtualKeyInfoAsync(int id)
        {
            _logger.LogInformation("Getting virtual key info for ID: {KeyId}", id);

            var key = await _virtualKeyRepository.GetByIdAsync(id);
            if (key == null)
            {
                _logger.LogWarning("Virtual key with ID {KeyId} not found", id);
                return null;
            }

            return MapToDto(key);
        }

        /// <inheritdoc />
        public async Task<List<VirtualKeyDto>> ListVirtualKeysAsync(int? virtualKeyGroupId = null)
        {
            if (virtualKeyGroupId.HasValue)
            {
                _logger.LogInformation("Listing virtual keys for group {GroupId}", virtualKeyGroupId.Value);
                var keysByGroup = await _virtualKeyRepository.GetByVirtualKeyGroupIdAsync(virtualKeyGroupId.Value);
                return keysByGroup.ConvertAll(MapToDto);
            }
            else
            {
                _logger.LogInformation("Listing all virtual keys");
                var keys = await _virtualKeyRepository.GetAllAsync();
                return keys.ConvertAll(MapToDto);
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateVirtualKeyAsync(int id, UpdateVirtualKeyRequestDto request)
        {
            _logger.LogInformation("Updating virtual key with ID: {KeyId}", id);

            var key = await _virtualKeyRepository.GetByIdAsync(id);
            if (key == null)
            {
                _logger.LogWarning("Virtual key with ID {KeyId} not found", id);
                return false;
            }

            // Track changed properties for event publishing
            var changedProperties = new List<string>();

            // Update properties and track changes
            if (request.KeyName != null && key.KeyName != request.KeyName)
            {
                key.KeyName = request.KeyName;
                changedProperties.Add(nameof(key.KeyName));
            }

            if (request.AllowedModels != null && key.AllowedModels != request.AllowedModels)
            {
                key.AllowedModels = request.AllowedModels;
                changedProperties.Add(nameof(key.AllowedModels));
            }

            if (request.VirtualKeyGroupId.HasValue && key.VirtualKeyGroupId != request.VirtualKeyGroupId.Value)
            {
                // Verify the new group exists
                var newGroup = await _groupRepository.GetByIdAsync(request.VirtualKeyGroupId.Value);
                if (newGroup == null)
                {
                    throw new InvalidOperationException($"Virtual key group with ID {request.VirtualKeyGroupId.Value} not found");
                }
                key.VirtualKeyGroupId = request.VirtualKeyGroupId.Value;
                changedProperties.Add(nameof(key.VirtualKeyGroupId));
            }

            if (request.IsEnabled.HasValue && key.IsEnabled != request.IsEnabled.Value)
            {
                key.IsEnabled = request.IsEnabled.Value;
                changedProperties.Add(nameof(key.IsEnabled));
            }

            if (request.ExpiresAt.HasValue && key.ExpiresAt != request.ExpiresAt)
            {
                key.ExpiresAt = request.ExpiresAt;
                changedProperties.Add(nameof(key.ExpiresAt));
            }

            if (request.Metadata != null && key.Metadata != request.Metadata)
            {
                key.Metadata = request.Metadata;
                changedProperties.Add(nameof(key.Metadata));
            }

            if (request.RateLimitRpm.HasValue && key.RateLimitRpm != request.RateLimitRpm)
            {
                key.RateLimitRpm = request.RateLimitRpm;
                changedProperties.Add(nameof(key.RateLimitRpm));
            }

            if (request.RateLimitRpd.HasValue && key.RateLimitRpd != request.RateLimitRpd)
            {
                key.RateLimitRpd = request.RateLimitRpd;
                changedProperties.Add(nameof(key.RateLimitRpd));
            }

            // Only proceed if there are actual changes
            if (changedProperties.Count() == 0)
            {
                _logger.LogDebug("No changes detected for virtual key {KeyId} - skipping update", id);
                return true;
            }

            key.UpdatedAt = DateTime.UtcNow;

            // Save changes
            var result = await _virtualKeyRepository.UpdateAsync(key);

            if (result)
            {
                // Publish VirtualKeyUpdated event for cache invalidation and cross-service coordination
                await PublishEventAsync(
                    new VirtualKeyUpdated
                    {
                        KeyId = key.Id,
                        KeyHash = key.KeyHash,
                        ChangedProperties = changedProperties.ToArray(),
                        CorrelationId = Guid.NewGuid().ToString()
                    },
                    $"update virtual key {id}",
                    new { ChangedProperties = string.Join(", ", changedProperties) });
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteVirtualKeyAsync(int id)
        {
            _logger.LogInformation("Deleting virtual key with ID: {KeyId}", id);

            var key = await _virtualKeyRepository.GetByIdAsync(id);
            if (key == null)
            {
                _logger.LogWarning("Virtual key with ID {KeyId} not found", id);
                return false;
            }

            // Cleanup associated media files if media lifecycle service is available
            if (_mediaLifecycleService != null)
            {
                try
                {
                    var deletedMediaCount = await _mediaLifecycleService.DeleteMediaForVirtualKeyAsync(id);
                    if (deletedMediaCount > 0)
                    {
                        _logger.LogInformation("Deleted {Count} media files for virtual key {KeyId}", deletedMediaCount, id);
                    }
                }
                catch (Exception ex)
                {
                    // Log the error but don't fail the virtual key deletion
                    _logger.LogError(ex, "Failed to delete media files for virtual key {KeyId}, but continuing with key deletion", id);
                }
            }
            else
            {
                _logger.LogWarning("Media lifecycle service not available, media files for virtual key {KeyId} will become orphaned", id);
            }
            
            var result = await _virtualKeyRepository.DeleteAsync(id);

            if (result)
            {
                // Publish VirtualKeyDeleted event for cache invalidation and cleanup
                await PublishEventAsync(
                    new VirtualKeyDeleted
                    {
                        KeyId = key.Id,
                        KeyHash = key.KeyHash,
                        KeyName = key.KeyName,
                        CorrelationId = Guid.NewGuid().ToString()
                    },
                    $"delete virtual key {key.Id}",
                    new { KeyName = key.KeyName });
            }

            return result;
        }


        /// <inheritdoc />
        public async Task<VirtualKeyValidationResult> ValidateVirtualKeyAsync(string key, string? requestedModel = null)
        {
            _logger.LogInformation("Validating virtual key and checking if model {Model} is allowed", (requestedModel ?? "any").Replace(Environment.NewLine, ""));

            var result = new VirtualKeyValidationResult { IsValid = false };

            if (string.IsNullOrEmpty(key))
            {
                result.ErrorMessage = "Key cannot be empty";
                return result;
            }

            if (!key.StartsWith(VirtualKeyConstants.KeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                result.ErrorMessage = "Invalid key format: doesn't start with required prefix";
                return result;
            }

            // Hash the key for lookup
            string keyHash = ComputeSha256Hash(key);

            // Look up the key in the database
            var virtualKey = await _virtualKeyRepository.GetByKeyHashAsync(keyHash);
            if (virtualKey == null)
            {
                result.ErrorMessage = "Key not found";
                return result;
            }

            // Check if key is enabled
            if (!virtualKey.IsEnabled)
            {
                result.ErrorMessage = "Key is disabled";
                return result;
            }

            // Check expiration
            if (virtualKey.ExpiresAt.HasValue && virtualKey.ExpiresAt.Value < DateTime.UtcNow)
            {
                result.ErrorMessage = "Key has expired";
                return result;
            }

            // Check group balance
            var group = await _groupRepository.GetByKeyIdAsync(virtualKey.Id);
            if (group != null && group.Balance <= 0)
            {
                result.ErrorMessage = "Budget depleted";
                return result;
            }

            // Check if model is allowed (if specified)
            if (!string.IsNullOrEmpty(requestedModel) && !string.IsNullOrEmpty(virtualKey.AllowedModels))
            {
                bool isModelAllowed = IsModelAllowed(requestedModel, virtualKey.AllowedModels);

                if (!isModelAllowed)
                {
                    result.ErrorMessage = $"Model {requestedModel} is not allowed for this key";
                    return result;
                }
            }

            // All validations passed
            result.IsValid = true;
            result.VirtualKeyId = virtualKey.Id;
            result.KeyName = virtualKey.KeyName;
            result.AllowedModels = virtualKey.AllowedModels;
            // Budget info is now at group level, not included in validation result

            return result;
        }




        /// <inheritdoc />
        public async Task<VirtualKeyValidationInfoDto?> GetValidationInfoAsync(int id)
        {
            _logger.LogInformation("Getting validation info for virtual key ID {KeyId}", id);

            var key = await _virtualKeyRepository.GetByIdAsync(id);
            if (key == null)
            {
                return null;
            }

            return new VirtualKeyValidationInfoDto
            {
                Id = key.Id,
                KeyName = key.KeyName,
                AllowedModels = key.AllowedModels,
                VirtualKeyGroupId = key.VirtualKeyGroupId,
                IsEnabled = key.IsEnabled,
                ExpiresAt = key.ExpiresAt,
                RateLimitRpm = key.RateLimitRpm,
                RateLimitRpd = key.RateLimitRpd
            };
        }

        /// <summary>
        /// Maps a VirtualKey entity to a VirtualKeyDto
        /// </summary>
        /// <param name="key">The entity to map</param>
        /// <returns>The mapped DTO</returns>
        private static VirtualKeyDto MapToDto(VirtualKey key)
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
        /// Generates a key prefix for display purposes
        /// </summary>
        /// <param name="keyHash">The key hash</param>
        /// <returns>A prefix showing part of the key</returns>
        private static string GenerateKeyPrefix(string keyHash)
        {
            // Handle null or empty keyHash to prevent exceptions in tests
            if (string.IsNullOrEmpty(keyHash))
            {
                return "condt_******...";
            }

            // Generate a prefix like "condt_abc123..." from the hash
            // This is for display purposes only
            var prefixLength = Math.Min(6, keyHash.Length);
            var shortPrefix = keyHash.Substring(0, prefixLength).ToLower();
            return $"condt_{shortPrefix}...";
        }

        /// <summary>
        /// Computes a SHA256 hash of the input string
        /// </summary>
        /// <param name="input">The input to hash</param>
        /// <returns>The hash as a hexadecimal string</returns>
        private static string ComputeSha256Hash(string input)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));

            var builder = new StringBuilder();
            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }

            return builder.ToString();
        }


        /// <summary>
        /// Checks if a requested model is allowed based on the AllowedModels string
        /// </summary>
        /// <param name="requestedModel">The model being requested</param>
        /// <param name="allowedModels">Comma-separated string of allowed models</param>
        /// <returns>True if the model is allowed, false otherwise</returns>
        private static bool IsModelAllowed(string requestedModel, string allowedModels)
        {
            if (string.IsNullOrEmpty(allowedModels))
                return true; // No restrictions

            var allowedModelsList = allowedModels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // First check for exact match
            if (allowedModelsList.Any(m => string.Equals(m, requestedModel, StringComparison.OrdinalIgnoreCase)))
                return true;

            // Then check for wildcard/prefix matches
            foreach (var allowedModel in allowedModelsList)
            {
                // Handle wildcards like "gpt-4*" to match any GPT-4 model
                if (allowedModel.EndsWith("*", StringComparison.OrdinalIgnoreCase) &&
                    allowedModel.Length > 1)
                {
                    string prefix = allowedModel.Substring(0, allowedModel.Length - 1);
                    if (requestedModel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public async Task PerformMaintenanceAsync()
        {
            _logger.LogInformation("Starting virtual key maintenance tasks");

            // TODO: Media Lifecycle Maintenance - Add the following tasks:
            // 1. Clean up expired media (based on MediaRecord.ExpiresAt)
            // 2. Clean up orphaned media (virtual key deleted but media remains)
            // 3. Prune old media based on retention policy (e.g., >90 days)
            // 4. Update storage usage statistics per virtual key
            // See: docs/TODO-Media-Lifecycle-Management.md for implementation plan

            try
            {
                // Get all virtual keys
                var allKeys = await _virtualKeyRepository.GetAllAsync();
                _logger.LogInformation("Processing maintenance for {KeyCount} virtual keys", allKeys.Count());

                int keysDisabled = 0;

                foreach (var key in allKeys)
                {
                    try
                    {
                        // Budget resets are no longer performed in the bank account model
                        // Only check and disable expired keys
                        if (key.IsEnabled && key.ExpiresAt.HasValue && key.ExpiresAt.Value < DateTime.UtcNow)
                        {
                            key.IsEnabled = false;
                            key.UpdatedAt = DateTime.UtcNow;

                            var updated = await _virtualKeyRepository.UpdateAsync(key);
                            if (updated)
                            {
                                keysDisabled++;
                                _logger.LogInformation("Disabled expired virtual key {KeyId} ({KeyName})",
                                    key.Id, key.KeyName.Replace(Environment.NewLine, ""));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing maintenance for virtual key {KeyId}", key.Id);
                        // Continue processing other keys even if one fails
                    }
                }

                _logger.LogInformation("Virtual key maintenance completed. Keys disabled: {KeysDisabled}", keysDisabled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during virtual key maintenance");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<VirtualKeyDiscoveryPreviewDto?> PreviewDiscoveryAsync(int id, string? capability = null)
        {
            _logger.LogInformation("Previewing discovery for virtual key {KeyId} with capability filter: {Capability}", 
                id, capability ?? "none");

            // Get the virtual key
            var virtualKey = await _virtualKeyRepository.GetByIdAsync(id);
            if (virtualKey == null)
            {
                _logger.LogWarning("Virtual key with ID {KeyId} not found", id);
                return null;
            }

            // Get all model mappings
            var allMappings = await _modelProviderMappingRepository.GetAllAsync();
            var enabledMappings = allMappings.Where(m => 
                m.IsEnabled && 
                m.Provider != null && 
                m.Provider.IsEnabled).ToList();

            var models = new List<DiscoveredModelDto>();

            // Check each model mapping against the virtual key's allowed models
            foreach (var mapping in enabledMappings)
            {
                // Check if model is allowed for this virtual key
                if (!IsModelAllowed(virtualKey, mapping.ModelAlias))
                {
                    continue;
                }

                // Build capabilities dictionary for this model
                var capabilities = await BuildCapabilitiesAsync(mapping.ModelAlias);

                // Filter by capability if specified
                if (!string.IsNullOrEmpty(capability))
                {
                    if (!capabilities.ContainsKey(capability))
                    {
                        continue;
                    }

                    if (capabilities[capability] is Dictionary<string, object> capDict)
                    {
                        if (!capDict.ContainsKey("supported") || capDict["supported"] is not bool supported || !supported)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }
                }

                var model = new DiscoveredModelDto
                {
                    Id = mapping.ModelAlias,
                    DisplayName = mapping.ModelAlias,
                    Capabilities = capabilities
                };

                models.Add(model);
            }

            return new VirtualKeyDiscoveryPreviewDto
            {
                Data = models,
                Count = models.Count()
            };
        }

        /// <summary>
        /// Builds the capabilities dictionary for a model
        /// </summary>
        private async Task<Dictionary<string, object>> BuildCapabilitiesAsync(string modelAlias)
        {
            var capabilities = new Dictionary<string, object>();

            // Basic capabilities - always included for all models
            capabilities["chat"] = new Dictionary<string, object> { ["supported"] = true };
            capabilities["chat_stream"] = new Dictionary<string, object> { ["supported"] = true };

            // Check vision support
            if (await _modelCapabilityService.SupportsVisionAsync(modelAlias))
            {
                capabilities["vision"] = new Dictionary<string, object> { ["supported"] = true };
            }

            // Check audio transcription support
            if (await _modelCapabilityService.SupportsAudioTranscriptionAsync(modelAlias))
            {
                var audioCapabilities = new Dictionary<string, object> { ["supported"] = true };
                
                var supportedLanguages = await _modelCapabilityService.GetSupportedLanguagesAsync(modelAlias);
                if (supportedLanguages.Count() > 0)
                {
                    audioCapabilities["supported_languages"] = supportedLanguages;
                }

                var supportedFormats = await _modelCapabilityService.GetSupportedFormatsAsync(modelAlias);
                if (supportedFormats.Count() > 0)
                {
                    audioCapabilities["supported_formats"] = supportedFormats;
                }

                capabilities["audio_transcription"] = audioCapabilities;
            }

            // Check text-to-speech support
            if (await _modelCapabilityService.SupportsTextToSpeechAsync(modelAlias))
            {
                var ttsCapabilities = new Dictionary<string, object> { ["supported"] = true };
                
                var supportedVoices = await _modelCapabilityService.GetSupportedVoicesAsync(modelAlias);
                if (supportedVoices.Count() > 0)
                {
                    ttsCapabilities["supported_voices"] = supportedVoices;
                }

                var supportedLanguages = await _modelCapabilityService.GetSupportedLanguagesAsync(modelAlias);
                if (supportedLanguages.Count() > 0)
                {
                    ttsCapabilities["supported_languages"] = supportedLanguages;
                }

                capabilities["text_to_speech"] = ttsCapabilities;
            }

            // Check realtime audio support
            if (await _modelCapabilityService.SupportsRealtimeAudioAsync(modelAlias))
            {
                capabilities["realtime_audio"] = new Dictionary<string, object> { ["supported"] = true };
            }

            // Check video generation support
            if (await _modelCapabilityService.SupportsVideoGenerationAsync(modelAlias))
            {
                capabilities["video_generation"] = new Dictionary<string, object> 
                { 
                    ["supported"] = true,
                    ["max_duration_seconds"] = 6,
                    ["supported_resolutions"] = new List<string> { "720x480", "1280x720", "1920x1080" },
                    ["supported_fps"] = new List<int> { 24, 30 },
                    ["supports_custom_styles"] = true
                };
            }

            // TODO: Add image generation support when method is available
            // For now, check if model contains "dall-e" or similar patterns
            if (modelAlias.Contains("dall-e", StringComparison.OrdinalIgnoreCase) ||
                modelAlias.Contains("stable-diffusion", StringComparison.OrdinalIgnoreCase) ||
                modelAlias.Contains("midjourney", StringComparison.OrdinalIgnoreCase))
            {
                capabilities["image_generation"] = new Dictionary<string, object>
                {
                    ["supported"] = true,
                    ["supported_sizes"] = new List<string> { "256x256", "512x512", "1024x1024", "1024x1792", "1792x1024" }
                };
            }

            return capabilities;
        }

        /// <summary>
        /// Checks if a model is allowed for a virtual key based on AllowedModels restrictions
        /// </summary>
        private bool IsModelAllowed(VirtualKey virtualKey, string modelAlias)
        {
            // If no AllowedModels specified, all models are allowed
            if (string.IsNullOrWhiteSpace(virtualKey.AllowedModels))
            {
                return true;
            }

            var allowedModels = virtualKey.AllowedModels
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim())
                .ToList();

            foreach (var allowedModel in allowedModels)
            {
                // Check for exact match
                if (allowedModel.Equals(modelAlias, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Check for wildcard/prefix match (e.g., "gpt-4*")
                if (allowedModel.EndsWith("*"))
                {
                    var prefix = allowedModel.Substring(0, allowedModel.Length - 1);
                    if (modelAlias.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <inheritdoc />
        public async Task<VirtualKeyDto?> GetVirtualKeyByIdAsync(int id)
        {
            var key = await _virtualKeyRepository.GetByIdAsync(id);
            if (key == null)
            {
                return null;
            }
            return MapToDto(key);
        }

        /// <inheritdoc />
        public async Task<VirtualKeyGroupDto?> GetKeyGroupAsync(int id)
        {
            var group = await _groupRepository.GetByKeyIdAsync(id);
            if (group == null)
            {
                return null;
            }

            return new VirtualKeyGroupDto
            {
                Id = group.Id,
                ExternalGroupId = group.ExternalGroupId,
                GroupName = group.GroupName,
                Balance = group.Balance,
                LifetimeCreditsAdded = group.LifetimeCreditsAdded,
                LifetimeSpent = group.LifetimeSpent,
                CreatedAt = group.CreatedAt,
                UpdatedAt = group.UpdatedAt,
                VirtualKeyCount = group.VirtualKeys?.Count ?? 0
            };
        }

        /// <inheritdoc />
        public async Task<VirtualKeyUsageDto?> GetUsageByKeyAsync(string keyValue)
        {
            if (string.IsNullOrEmpty(keyValue))
            {
                _logger.LogWarning("GetUsageByKeyAsync called with empty key value");
                return null;
            }

            if (!keyValue.StartsWith(VirtualKeyConstants.KeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("GetUsageByKeyAsync called with invalid key format (missing prefix)");
                return null;
            }

            // Hash the key for lookup
            var keyHash = ComputeSha256Hash(keyValue);
            
            // Get the virtual key by hash
            var virtualKey = await _virtualKeyRepository.GetByKeyHashAsync(keyHash);
            if (virtualKey == null)
            {
                _logger.LogInformation("Virtual key not found for hash lookup");
                return null;
            }

            // Get the group information
            var group = await _groupRepository.GetByKeyIdAsync(virtualKey.Id);
            if (group == null)
            {
                _logger.LogWarning("Virtual key group not found for key {KeyId}", virtualKey.Id);
                return null;
            }

            // Get spending history for this specific key
            var spendHistory = await _spendHistoryRepository.GetByVirtualKeyIdAsync(virtualKey.Id);
            var totalRequests = spendHistory.Count();
            // Note: VirtualKeySpendHistory doesn't track individual tokens, only amounts
            // We'll need to estimate based on spending or leave it as 0
            var totalTokens = 0L; // Token tracking would require different data structure
            var lastUsedAt = spendHistory.OrderByDescending(s => s.Timestamp).FirstOrDefault()?.Timestamp;

            return new VirtualKeyUsageDto
            {
                KeyId = virtualKey.Id,
                KeyName = virtualKey.KeyName,
                GroupId = group.Id,
                GroupName = group.GroupName,
                Balance = group.Balance,
                LifetimeCreditsAdded = group.LifetimeCreditsAdded,
                LifetimeSpent = group.LifetimeSpent,
                TotalRequests = totalRequests,
                TotalTokens = totalTokens,
                IsEnabled = virtualKey.IsEnabled,
                ExpiresAt = virtualKey.ExpiresAt,
                CreatedAt = virtualKey.CreatedAt,
                LastUsedAt = lastUsedAt,
                RateLimitRpm = virtualKey.RateLimitRpm,
                RateLimitRpd = virtualKey.RateLimitRpd,
                AllowedModels = virtualKey.AllowedModels
            };
        }
    }
}

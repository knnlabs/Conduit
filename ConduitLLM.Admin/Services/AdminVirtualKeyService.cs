using System.Security.Cryptography;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;

using MassTransit;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for managing virtual keys through the Admin API
    /// </summary>
    public partial class AdminVirtualKeyService : EventPublishingServiceBase, IAdminVirtualKeyService
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


    }
}

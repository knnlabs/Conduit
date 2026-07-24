using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Extensions;

using ConduitLLM.Configuration.Messaging;

using Microsoft.Extensions.Logging;
using System.Text.Json;
using VirtualKeyUtilities = ConduitLLM.Configuration.Utilities.VirtualKeyUtilities;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Base class for virtual key services providing shared CRUD operations with
    /// event publishing and extensibility hooks for caching and media cleanup.
    /// </summary>
    public abstract class VirtualKeyServiceBase : EventPublishingServiceBase
    {
        protected readonly IVirtualKeyRepository VirtualKeyRepository;
        protected readonly IVirtualKeyGroupRepository GroupRepository;
        protected readonly IVirtualKeySpendHistoryRepository SpendHistoryRepository;

        protected VirtualKeyServiceBase(
            IVirtualKeyRepository virtualKeyRepository,
            IVirtualKeyGroupRepository groupRepository,
            IVirtualKeySpendHistoryRepository spendHistoryRepository,
            IEventBus? eventBus,
            ILogger logger)
            : base(eventBus, logger)
        {
            VirtualKeyRepository = virtualKeyRepository ?? throw new ArgumentNullException(nameof(virtualKeyRepository));
            GroupRepository = groupRepository ?? throw new ArgumentNullException(nameof(groupRepository));
            SpendHistoryRepository = spendHistoryRepository ?? throw new ArgumentNullException(nameof(spendHistoryRepository));
        }

        #region Virtual Hooks

        /// <summary>Called after a virtual key is created and saved to the database.</summary>
        protected virtual Task OnVirtualKeyCreatedAsync(VirtualKey key) => Task.CompletedTask;

        /// <summary>Called after a virtual key is updated. Subclasses can use this for cache invalidation.</summary>
        protected virtual Task OnVirtualKeyUpdatedAsync(VirtualKey key, string[] changedProperties) => Task.CompletedTask;

        /// <summary>Called before a virtual key is deleted. Subclasses can use this for media cleanup.</summary>
        protected virtual Task OnBeforeVirtualKeyDeleteAsync(int keyId) => Task.CompletedTask;

        /// <summary>Called after a virtual key is deleted. Subclasses can use this for cache invalidation.</summary>
        protected virtual Task OnVirtualKeyDeletedAsync(VirtualKey key) => Task.CompletedTask;

        #endregion

        #region CRUD Operations

        public virtual async Task<CreateVirtualKeyResponseDto> GenerateVirtualKeyAsync(CreateVirtualKeyRequestDto request)
        {
            var keyValue = VirtualKeyUtilities.GenerateSecureKey();
            var keyWithPrefix = VirtualKeyConstants.KeyPrefix + keyValue;
            var keyHash = VirtualKeyUtilities.HashKey(keyWithPrefix);

            // Verify the group exists
            var existingGroup = await GroupRepository.GetByIdAsync(request.VirtualKeyGroupId);
            if (existingGroup == null)
            {
                throw new InvalidOperationException(
                    $"Virtual key group {request.VirtualKeyGroupId} not found. Ensure the group exists before creating keys.");
            }

            if (existingGroup.Balance <= 0)
            {
                Logger.LogWarning(
                    "Virtual key group {GroupId} has zero balance. Keys in this group cannot make API calls until funded.",
                    request.VirtualKeyGroupId);
            }

            var virtualKey = new VirtualKey
            {
                KeyName = request.KeyName ?? string.Empty,
                KeyHash = keyHash,
                AllowedModels = request.AllowedModels is { Count: > 0 }
                    ? string.Join(',', request.AllowedModels)
                    : null,
                VirtualKeyGroupId = existingGroup.Id,
                IsEnabled = true,
                ExpiresAt = request.ExpiresAt,
                Metadata = request.Metadata is null ? null : JsonSerializer.Serialize(request.Metadata),
                RateLimitRpm = request.RateLimitRpm,
                RateLimitRpd = request.RateLimitRpd,
                RateLimitTpm = request.RateLimitTpm,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createdId = await VirtualKeyRepository.CreateAsync(virtualKey);

            virtualKey = await VirtualKeyRepository.GetByIdAsync(createdId);
            if (virtualKey == null)
            {
                throw new InvalidOperationException($"Failed to retrieve newly created virtual key with ID {createdId}");
            }

            Logger.LogInformation("Created new virtual key: {KeyName} (ID: {KeyId})",
                LoggingSanitizer.S(virtualKey.KeyName), virtualKey.Id);

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

            await OnVirtualKeyCreatedAsync(virtualKey);

            return new CreateVirtualKeyResponseDto
            {
                VirtualKey = keyWithPrefix,
                KeyInfo = VirtualKeyUtilities.MapToDto(virtualKey)
            };
        }

        public virtual async Task<VirtualKeyDto?> GetVirtualKeyInfoAsync(int id)
        {
            var virtualKey = await VirtualKeyRepository.GetByIdAsync(id);
            if (virtualKey == null)
            {
                Logger.LogWarning("Virtual key with ID {KeyId} not found", id);
                return null;
            }

            return VirtualKeyUtilities.MapToDto(virtualKey);
        }

        public virtual async Task<List<VirtualKeyDto>> ListVirtualKeysAsync()
        {
            var virtualKeys = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                VirtualKeyRepository.GetPaginatedAsync);
            Logger.LogDebug("Listed {Count} virtual keys", virtualKeys.Count);
            return [.. virtualKeys.Select(VirtualKeyUtilities.MapToDto)];
        }

        public virtual async Task<bool> UpdateVirtualKeyAsync(int id, UpdateVirtualKeyRequestDto request)
        {
            var key = await VirtualKeyRepository.GetByIdAsync(id);
            if (key == null)
            {
                Logger.LogWarning("Virtual key with ID {KeyId} not found for update", id);
                return false;
            }

            // Track actual changes
            var changedProperties = new List<string>();

            if (request.KeyName != null && key.KeyName != request.KeyName)
            {
                key.KeyName = request.KeyName;
                changedProperties.Add(nameof(key.KeyName));
            }

            if (request.AllowedModels != null)
            {
                var allowedModels = request.AllowedModels.Count == 0
                    ? null
                    : string.Join(',', request.AllowedModels);
                if (key.AllowedModels != allowedModels)
                {
                    key.AllowedModels = allowedModels;
                    changedProperties.Add(nameof(key.AllowedModels));
                }
            }

            if (request.VirtualKeyGroupId.HasValue && key.VirtualKeyGroupId != request.VirtualKeyGroupId.Value)
            {
                var newGroup = await GroupRepository.GetByIdAsync(request.VirtualKeyGroupId.Value);
                if (newGroup == null)
                {
                    throw new InvalidOperationException(
                        $"Virtual key group with ID {request.VirtualKeyGroupId.Value} not found");
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

            if (request.Metadata != null)
            {
                var metadata = request.Metadata.Count == 0
                    ? null
                    : JsonSerializer.Serialize(request.Metadata);
                if (key.Metadata != metadata)
                {
                    key.Metadata = metadata;
                    changedProperties.Add(nameof(key.Metadata));
                }
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

            if (request.RateLimitTpm.HasValue && key.RateLimitTpm != request.RateLimitTpm)
            {
                key.RateLimitTpm = request.RateLimitTpm;
                changedProperties.Add(nameof(key.RateLimitTpm));
            }

            if (!changedProperties.Any())
            {
                Logger.LogDebug("No changes detected for virtual key {KeyId} — skipping update", id);
                return true;
            }

            key.UpdatedAt = DateTime.UtcNow;
            var success = await VirtualKeyRepository.UpdateAsync(key);

            if (success)
            {
                var changed = changedProperties.ToArray();

                await PublishEventAsync(
                    new VirtualKeyUpdated
                    {
                        KeyId = key.Id,
                        KeyHash = key.KeyHash,
                        ChangedProperties = changed,
                        CorrelationId = Guid.NewGuid().ToString()
                    },
                    $"update virtual key {id}",
                    new { ChangedProperties = string.Join(", ", changed) });

                await OnVirtualKeyUpdatedAsync(key, changed);

                Logger.LogInformation("Updated virtual key {KeyId} ({KeyName}), changed: [{ChangedProperties}]",
                    id, LoggingSanitizer.S(key.KeyName), string.Join(", ", changed));
            }

            return success;
        }

        public virtual async Task<bool> DeleteVirtualKeyAsync(int id)
        {
            var key = await VirtualKeyRepository.GetByIdAsync(id);
            if (key == null)
            {
                Logger.LogWarning("Virtual key with ID {KeyId} not found for deletion", id);
                return false;
            }

            await OnBeforeVirtualKeyDeleteAsync(id);

            var success = await VirtualKeyRepository.DeleteAsync(id);

            if (success)
            {
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

                await OnVirtualKeyDeletedAsync(key);

                Logger.LogInformation("Deleted virtual key {KeyId} ({KeyName})",
                    id, LoggingSanitizer.S(key.KeyName));
            }

            return success;
        }

        public virtual async Task<bool> ResetSpendAsync(int id)
        {
            var virtualKey = await VirtualKeyRepository.GetByIdAsync(id);
            if (virtualKey == null) return false;

            var group = await GroupRepository.GetByIdAsync(virtualKey.VirtualKeyGroupId);
            if (group == null)
            {
                Logger.LogError("Virtual key {KeyId} has invalid group ID {GroupId}",
                    id, virtualKey.VirtualKeyGroupId);
                return false;
            }

            if (group.LifetimeSpent > 0)
            {
                var spendHistory = new VirtualKeySpendHistory
                {
                    VirtualKeyId = virtualKey.Id,
                    Amount = group.LifetimeSpent,
                    Date = DateTime.UtcNow
                };
                await SpendHistoryRepository.CreateAsync(spendHistory);

                group.LifetimeSpent = 0;
                group.UpdatedAt = DateTime.UtcNow;
                await GroupRepository.UpdateAsync(group);
            }

            virtualKey.UpdatedAt = DateTime.UtcNow;
            return await VirtualKeyRepository.UpdateAsync(virtualKey);
        }

        #endregion
    }
}

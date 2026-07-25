using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Services;
using ConduitLLM.Providers.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Endpoints
{
    public partial class ProviderCredentialsEndpoints
    {
        /// <summary>
        /// Gets all key credentials for a specific provider
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <returns>List of key credentials for the provider</returns>
        public async Task<IResult> GetProviderKeyCredentials(int providerId)
        {
            var keys = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                _keyRepository.GetByProviderIdPaginatedAsync, providerId);
            var result = keys.Select(ToKeyDto);

            return Ok(result);
        }

        /// <summary>
        /// Gets a specific key credential
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <param name="keyId">The ID of the key</param>
        /// <returns>The key credential</returns>
        public async Task<IResult> GetProviderKeyCredential(int providerId, int keyId)
        {
            var key = await _keyRepository.GetByIdAsync(keyId);

            if (key == null || key.ProviderId != providerId)
            {
                Logger.LogWarning("Key credential not found {KeyId} for provider {ProviderId}", keyId, providerId);
                return AdminResults.NotFoundEntity("Key credential", keyId);
            }

            return Ok(ToKeyDto(key));
        }

        /// <summary>
        /// Creates a new key credential for a provider
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <param name="request">The request containing key credential details</param>
        /// <returns>The created key credential</returns>
        public async Task<IResult> CreateProviderKeyCredential(int providerId, CreateKeyRequest request)
        {
            // Verify provider exists
            var provider = await _providerRepository.GetByIdAsync(providerId);
            if (provider == null)
            {
                return AdminResults.NotFoundEntity("Provider", providerId);
            }

            // Every declared secret must be supplied up front: a key that is missing one cannot
            // authenticate, and the failure would otherwise surface as an opaque provider rejection.
            var missingSecrets = ProviderConfigurationRegistry.GetMissingRequiredSecrets(
                provider.ProviderType, request.SecretSettings);
            if (missingSecrets.Count > 0)
            {
                return BadRequest(
                    $"{provider.ProviderType} requires: {string.Join(", ", missingSecrets)}.");
            }

            var keyCredential = new ProviderKeyCredential
            {
                ProviderId = providerId,
                ApiKey = request.ApiKey,
                KeyName = request.KeyName,
                BaseUrl = request.BaseUrl,
                SecretSettings = _secretProtector.ProtectAll(request.SecretSettings),
                IsPrimary = request.IsPrimary,
                IsEnabled = request.IsEnabled,
                ProviderAccountGroup = (short)(request.ProviderAccountGroup ?? 0),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createdKeyId = await _keyRepository.CreateAsync(keyCredential);

            // After CreateAsync, keyCredential has its Id populated and IsPrimary potentially modified
            // Publish key created event
            PublishEventFireAndForget(new ConduitLLM.Configuration.Events.ProviderKeyCredentialCreated
            {
                KeyId = createdKeyId,
                ProviderId = providerId,
                IsPrimary = keyCredential.IsPrimary,
                IsEnabled = keyCredential.IsEnabled,
                CorrelationId = Guid.NewGuid()
            }, "create provider key", new { ProviderId = providerId, KeyId = createdKeyId });

            LogAdminAudit("Created", "ProviderKeyCredential", createdKeyId, $"Provider: {providerId}, KeyName: {LoggingSanitizer.S(keyCredential.KeyName)}");
            AdminOperationsMetricsService.RecordConfigurationChange("providerkey", "create");

            return Results.Created($"/v1/admin/providers/{providerId}/keys/{createdKeyId}", ToKeyDto(keyCredential));
        }

        /// <summary>
        /// Updates a key credential
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <param name="keyId">The ID of the key</param>
        /// <param name="request">The update request containing new key credential values</param>
        /// <returns>No content if successful</returns>
        public async Task<IResult> UpdateProviderKeyCredential(int providerId, int keyId, UpdateKeyRequest request)
        {
            var key = await _keyRepository.GetByIdAsync(keyId);
            if (key == null || key.ProviderId != providerId)
            {
                Logger.LogWarning("Key credential not found for update {KeyId}", keyId);
                return AdminResults.NotFoundEntity("Key credential", keyId);
            }

            // Track changes with before/after values
            var changes = new List<(string Property, string? OldValue, string? NewValue)>();

            if (!string.IsNullOrEmpty(request.KeyName) && key.KeyName != request.KeyName)
            {
                changes.Add(("KeyName", key.KeyName, request.KeyName));
                key.KeyName = request.KeyName;
            }
            if (!string.IsNullOrEmpty(request.ApiKey))
            {
                changes.Add(("ApiKey", "***", "***")); // Never log API key values
                key.ApiKey = request.ApiKey;
            }
            if (request.BaseUrl != null && key.BaseUrl != request.BaseUrl)
            {
                changes.Add(("BaseUrl", key.BaseUrl, request.BaseUrl));
                key.BaseUrl = request.BaseUrl;
            }
            if (request.SecretSettings != null)
            {
                // Replace wholesale, and never record the values: only which keys were set.
                changes.Add(("SecretSettings", "***", "***"));
                key.SecretSettings = _secretProtector.ProtectAll(request.SecretSettings);
            }
            if (request.IsPrimary.HasValue && key.IsPrimary != request.IsPrimary.Value)
            {
                changes.Add(("IsPrimary", key.IsPrimary.ToString(), request.IsPrimary.Value.ToString()));
                key.IsPrimary = request.IsPrimary.Value;
            }
            if (request.IsEnabled.HasValue && key.IsEnabled != request.IsEnabled.Value)
            {
                changes.Add(("IsEnabled", key.IsEnabled.ToString(), request.IsEnabled.Value.ToString()));
                key.IsEnabled = request.IsEnabled.Value;
            }
            if (request.ProviderAccountGroup.HasValue && key.ProviderAccountGroup != (short)request.ProviderAccountGroup.Value)
            {
                changes.Add(("ProviderAccountGroup", key.ProviderAccountGroup.ToString(), request.ProviderAccountGroup.Value.ToString()));
                key.ProviderAccountGroup = (short)request.ProviderAccountGroup.Value;
            }

            key.UpdatedAt = DateTime.UtcNow;

            await _keyRepository.UpdateAsync(key);

            var changedProperties = changes.Count > 0
                ? changes.Select(c => c.Property).ToArray()
                : Array.Empty<string>();

            if (changes.Count > 0)
            {
                LogAdminAuditWithChanges("ProviderKeyCredential", keyId, changes, $"Provider: {providerId}");
            }
            else
            {
                LogAdminAudit("Updated", "ProviderKeyCredential", keyId, $"Provider: {providerId} (no changes detected)");
            }
            AdminOperationsMetricsService.RecordConfigurationChange("providerkey", "update");

            // Publish key updated event
            PublishEventFireAndForget(new ConduitLLM.Configuration.Events.ProviderKeyCredentialUpdated
            {
                KeyId = keyId,
                ProviderId = providerId,
                ChangedProperties = changedProperties,
                CorrelationId = Guid.NewGuid()
            }, "update provider key", new { ProviderId = providerId, KeyId = keyId });

            return Ok(ToKeyDto(key));
        }

        private static ProviderKeyCredentialDto ToKeyDto(ProviderKeyCredential key) => new()
        {
            Id = key.Id,
            ProviderId = key.ProviderId,
            KeyName = key.KeyName,
            IsPrimary = key.IsPrimary,
            IsEnabled = key.IsEnabled,
            ProviderAccountGroup = key.ProviderAccountGroup,
            ApiKey = key.ApiKey is null ? "***" : "***" + key.ApiKey[^Math.Min(4, key.ApiKey.Length)..],
            BaseUrl = key.BaseUrl,
            // Names only. Secret values are write-only by contract and never leave the server.
            ConfiguredSecretSettings = key.SecretSettings?.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray()
                ?? Array.Empty<string>(),
            CreatedAt = key.CreatedAt,
            UpdatedAt = key.UpdatedAt
        };

        /// <summary>
        /// Deletes a key credential
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <param name="keyId">The ID of the key to delete</param>
        /// <returns>No content if successful</returns>
        public async Task<IResult> DeleteProviderKeyCredential(int providerId, int keyId)
        {
            var key = await _keyRepository.GetByIdAsync(keyId);
            if (key == null || key.ProviderId != providerId)
            {
                Logger.LogWarning("Key credential not found for deletion {KeyId}", keyId);
                return AdminResults.NotFoundEntity("Key credential", keyId);
            }

            await _keyRepository.DeleteAsync(keyId);

            LogAdminAudit("Deleted", "ProviderKeyCredential", keyId, $"Provider: {providerId}, KeyName: {LoggingSanitizer.S(key.KeyName)}");
            AdminOperationsMetricsService.RecordConfigurationChange("providerkey", "delete");

            // Publish key deleted event
            PublishEventFireAndForget(new ConduitLLM.Configuration.Events.ProviderKeyCredentialDeleted
            {
                KeyId = keyId,
                ProviderId = providerId,
                CorrelationId = Guid.NewGuid()
            }, "delete provider key", new { ProviderId = providerId, KeyId = keyId });

            return NoContent();
        }

        /// <summary>
        /// Sets a key as the primary key for a provider
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <param name="keyId">The ID of the key to set as primary</param>
        /// <returns>No content if successful</returns>
        public async Task<IResult> SetPrimaryKey(int providerId, int keyId)
        {
            var key = await _keyRepository.GetByIdAsync(keyId);
            if (key == null || key.ProviderId != providerId)
            {
                Logger.LogWarning("Key credential not found {KeyId} for provider {ProviderId}", keyId, providerId);
                return AdminResults.NotFoundEntity("Key credential", keyId);
            }

            // Unset all other primary keys for this provider
            var allKeys = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                _keyRepository.GetByProviderIdPaginatedAsync, providerId);
            foreach (var otherKey in allKeys.Where(k => k.IsPrimary && k.Id != keyId))
            {
                otherKey.IsPrimary = false;
                otherKey.UpdatedAt = DateTime.UtcNow;
                await _keyRepository.UpdateAsync(otherKey);
            }

            // Set this key as primary
            key.IsPrimary = true;
            key.UpdatedAt = DateTime.UtcNow;
            await _keyRepository.UpdateAsync(key);

            LogAdminAudit("SetPrimary", "ProviderKeyCredential", keyId, $"Provider: {providerId}, KeyName: {LoggingSanitizer.S(key.KeyName)}");
            AdminOperationsMetricsService.RecordConfigurationChange("providerkey", "set_primary");

            // Publish primary key changed event
            PublishEventFireAndForget(new ConduitLLM.Configuration.Events.ProviderKeyCredentialPrimaryChanged
            {
                ProviderId = providerId,
                OldPrimaryKeyId = 0, // Not tracking old primary in this method
                NewPrimaryKeyId = keyId,
                CorrelationId = Guid.NewGuid()
            }, "set primary key", new { ProviderId = providerId, KeyId = keyId });

            return NoContent();
        }
    }
}

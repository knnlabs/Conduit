using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Admin.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers
{
    public partial class ProviderCredentialsController
    {
        /// <summary>
        /// Gets all key credentials for a specific provider
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <returns>List of key credentials for the provider</returns>
        [HttpGet("{providerId}/keys")]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetProviderKeyCredentials(int providerId)
        {
            try
            {
                var keys = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                    _keyRepository.GetByProviderIdPaginatedAsync, providerId);
                var result = keys.Select(k => new
                {
                    k.Id,
                    k.ProviderId,
                    k.KeyName,
                    k.IsPrimary,
                    k.IsEnabled,
                    k.ProviderAccountGroup,
                    ApiKey = k.ApiKey != null ? "***" + k.ApiKey.Substring(Math.Max(0, k.ApiKey.Length - 4)) : "***", // Mask API key
                    k.Organization,
                    k.BaseUrl,
                    k.CreatedAt,
                    k.UpdatedAt
                });
                return Ok(result);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting key credentials for provider {ProviderId}", providerId);
                return this.InternalServerError();
            }
        }

        /// <summary>
        /// Gets a specific key credential
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <param name="keyId">The ID of the key</param>
        /// <returns>The key credential</returns>
        [HttpGet("{providerId}/keys/{keyId}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetProviderKeyCredential(int providerId, int keyId)
        {
            try
            {
                var key = await _keyRepository.GetByIdAsync(keyId);

                if (key == null || key.ProviderId != providerId)
                {
                    Logger.LogWarning("Key credential not found {KeyId} for provider {ProviderId}", keyId, providerId);
                    return this.NotFoundEntity("Key credential", keyId);
                }

                return Ok(new
                {
                    key.Id,
                    key.ProviderId,
                    key.KeyName,
                    key.IsPrimary,
                    key.IsEnabled,
                    key.ProviderAccountGroup,
                    ApiKey = key.ApiKey != null ? "***" + key.ApiKey.Substring(Math.Max(0, key.ApiKey.Length - 4)) : "***", // Mask API key
                    key.Organization,
                    key.BaseUrl,
                    key.CreatedAt,
                    key.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting key credential {KeyId} for provider {ProviderId}", keyId, providerId);
                return this.InternalServerError();
            }
        }

        /// <summary>
        /// Creates a new key credential for a provider
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <param name="request">The request containing key credential details</param>
        /// <returns>The created key credential</returns>
        [HttpPost("{providerId}/keys")]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateProviderKeyCredential(int providerId, [FromBody] CreateKeyRequest request)
        {
            try
            {
                // Verify provider exists
                var provider = await _providerRepository.GetByIdAsync(providerId);
                if (provider == null)
                {
                    return this.NotFoundEntity("Provider", providerId);
                }

                var keyCredential = new ProviderKeyCredential
                {
                    ProviderId = providerId,
                    ApiKey = request.ApiKey,
                    KeyName = request.KeyName,
                    Organization = request.Organization,
                    BaseUrl = request.BaseUrl,
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

                LogAdminAudit("Created", "ProviderKeyCredential", createdKeyId, $"Provider: {providerId}, KeyName: {keyCredential.KeyName}");

                return CreatedAtAction(
                    nameof(GetProviderKeyCredential),
                    new { providerId = providerId, keyId = createdKeyId },
                    new
                    {
                        Id = createdKeyId,
                        keyCredential.ProviderId,
                        keyCredential.KeyName,
                        keyCredential.IsPrimary,
                        keyCredential.IsEnabled,
                        keyCredential.ProviderAccountGroup,
                        ApiKey = keyCredential.ApiKey != null ? "***" + keyCredential.ApiKey.Substring(Math.Max(0, keyCredential.ApiKey.Length - 4)) : "***",
                        keyCredential.Organization,
                        keyCredential.BaseUrl,
                        keyCredential.CreatedAt,
                        keyCredential.UpdatedAt
                    });
            }
            catch (InvalidOperationException ex)
            {
                Logger.LogWarning("Controller caught InvalidOperationException of type {ExceptionType} for provider {ProviderId}: {Message}",
                    ex.GetType().FullName, providerId, ex.Message);
                return this.BadRequestError(ex.Message, "invalid_operation");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Controller caught general Exception of type {ExceptionType} for provider {ProviderId}: {Message}",
                    ex.GetType().FullName, providerId, ex.Message);
                return this.InternalServerError();
            }
        }

        /// <summary>
        /// Updates a key credential
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <param name="keyId">The ID of the key</param>
        /// <param name="request">The update request containing new key credential values</param>
        /// <returns>No content if successful</returns>
        [HttpPut("{providerId}/keys/{keyId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateProviderKeyCredential(int providerId, int keyId, [FromBody] UpdateKeyRequest request)
        {
            try
            {
                var key = await _keyRepository.GetByIdAsync(keyId);
                if (key == null || key.ProviderId != providerId)
                {
                    Logger.LogWarning("Key credential not found for update {KeyId}", keyId);
                    return this.NotFoundEntity("Key credential", keyId);
                }

                // Update fields
                if (!string.IsNullOrEmpty(request.KeyName))
                    key.KeyName = request.KeyName;
                if (!string.IsNullOrEmpty(request.ApiKey))
                    key.ApiKey = request.ApiKey;
                if (request.Organization != null)
                    key.Organization = request.Organization;
                if (request.BaseUrl != null)
                    key.BaseUrl = request.BaseUrl;
                if (request.IsPrimary.HasValue)
                    key.IsPrimary = request.IsPrimary.Value;
                if (request.IsEnabled.HasValue)
                    key.IsEnabled = request.IsEnabled.Value;
                if (request.ProviderAccountGroup.HasValue)
                    key.ProviderAccountGroup = (short)request.ProviderAccountGroup.Value;
                
                key.UpdatedAt = DateTime.UtcNow;

                await _keyRepository.UpdateAsync(key);

                LogAdminAudit("Updated", "ProviderKeyCredential", keyId, $"Provider: {providerId}");

                // Publish key updated event
                PublishEventFireAndForget(new ConduitLLM.Configuration.Events.ProviderKeyCredentialUpdated
                {
                    KeyId = keyId,
                    ProviderId = providerId,
                    ChangedProperties = new[] { "KeyName", "Organization", "ProviderAccountGroup", "BaseUrl", "IsPrimary", "IsEnabled" },
                    CorrelationId = Guid.NewGuid()
                }, "update provider key", new { ProviderId = providerId, KeyId = keyId });

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                Logger.LogWarning(ex, "Invalid operation when updating key credential {KeyId}", keyId);
                return this.BadRequestError(ex.Message, "invalid_operation");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error updating key credential {KeyId}", keyId);
                return this.InternalServerError();
            }
        }

        /// <summary>
        /// Deletes a key credential
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <param name="keyId">The ID of the key to delete</param>
        /// <returns>No content if successful</returns>
        [HttpDelete("{providerId}/keys/{keyId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteProviderKeyCredential(int providerId, int keyId)
        {
            try
            {
                var key = await _keyRepository.GetByIdAsync(keyId);
                if (key == null || key.ProviderId != providerId)
                {
                    Logger.LogWarning("Key credential not found for deletion {KeyId}", keyId);
                    return this.NotFoundEntity("Key credential", keyId);
                }

                await _keyRepository.DeleteAsync(keyId);

                LogAdminAudit("Deleted", "ProviderKeyCredential", keyId, $"Provider: {providerId}");

                // Publish key deleted event
                PublishEventFireAndForget(new ConduitLLM.Configuration.Events.ProviderKeyCredentialDeleted
                {
                    KeyId = keyId,
                    ProviderId = providerId,
                    CorrelationId = Guid.NewGuid()
                }, "delete provider key", new { ProviderId = providerId, KeyId = keyId });

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                Logger.LogWarning(ex, "Invalid operation when deleting key credential {KeyId}", keyId);
                return this.BadRequestError(ex.Message, "invalid_operation");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error deleting key credential {KeyId}", keyId);
                return this.InternalServerError();
            }
        }

        /// <summary>
        /// Sets a key as the primary key for a provider
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <param name="keyId">The ID of the key to set as primary</param>
        /// <returns>No content if successful</returns>
        [HttpPost("{providerId}/keys/{keyId}/set-primary")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SetPrimaryKey(int providerId, int keyId)
        {
            try
            {
                var key = await _keyRepository.GetByIdAsync(keyId);
                if (key == null || key.ProviderId != providerId)
                {
                    Logger.LogWarning("Key credential not found {KeyId} for provider {ProviderId}", keyId, providerId);
                    return this.NotFoundEntity("Key credential", keyId);
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

                LogAdminAudit("SetPrimary", "ProviderKeyCredential", keyId, $"Provider: {providerId}");

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
            catch (InvalidOperationException ex)
            {
                Logger.LogWarning(ex, "Invalid operation when setting primary key {KeyId} for provider {ProviderId}", keyId, providerId);
                return this.BadRequestError(ex.Message, "invalid_operation");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error setting primary key {KeyId} for provider {ProviderId}", keyId, providerId);
                return this.InternalServerError();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using MassTransit;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using static ConduitLLM.Core.Extensions.LoggingSanitizer;
using ConduitLLM.Core.Controllers;
using ConduitLLM.Core.Events;
using ConduitLLM.Configuration.Events;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing provider credentials
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class ProviderCredentialsController : EventPublishingControllerBase
    {
        private readonly IProviderRepository _providerRepository;
        private readonly IProviderKeyCredentialRepository _keyRepository;
        private readonly ILLMClientFactory _clientFactory;
        private readonly ILogger<ProviderCredentialsController> _logger;

        /// <summary>
        /// Initializes a new instance of the ProviderCredentialsController
        /// </summary>
        public ProviderCredentialsController(
            IProviderRepository providerRepository,
            IProviderKeyCredentialRepository keyRepository,
            ILLMClientFactory clientFactory,
            IPublishEndpoint publishEndpoint,
            ILogger<ProviderCredentialsController> logger)
            : base(publishEndpoint, logger)
        {
            _providerRepository = providerRepository ?? throw new ArgumentNullException(nameof(providerRepository));
            _keyRepository = keyRepository ?? throw new ArgumentNullException(nameof(keyRepository));
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all provider configurations
        /// </summary>
        /// <returns>List of all providers</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllProviders()
        {
            try
            {
                var providers = await _providerRepository.GetAllAsync();
                var result = providers.Select(p => new
                {
                    p.Id,
                    p.ProviderType,
                    p.ProviderName,
                    p.BaseUrl,
                    p.IsEnabled,
                    p.CreatedAt,
                    p.UpdatedAt,
                    KeyCount = p.ProviderKeyCredentials?.Count ?? 0
                });
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all providers");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Gets a provider by ID
        /// </summary>
        /// <param name="id">The ID of the provider</param>
        /// <returns>The provider</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetProviderById(int id)
        {
            try
            {
                var provider = await _providerRepository.GetByIdAsync(id);

                if (provider == null)
                {
                    _logger.LogWarning("Provider not found {ProviderId}", id);
                    return NotFound(new { error = "Provider not found" });
                }

                return Ok(new
                {
                    provider.Id,
                    provider.ProviderType,
                    provider.ProviderName,
                    provider.BaseUrl,
                    provider.IsEnabled,
                    provider.CreatedAt,
                    provider.UpdatedAt,
                    KeyCount = provider.ProviderKeyCredentials?.Count ?? 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Creates a new provider
        /// </summary>
        /// <returns>The created provider</returns>
        [HttpPost]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateProvider([FromBody] CreateProviderRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var provider = new Provider
                {
                    ProviderType = request.ProviderType,
                    ProviderName = request.ProviderName,
                    BaseUrl = request.BaseUrl,
                    IsEnabled = request.IsEnabled,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var id = await _providerRepository.CreateAsync(provider);
                provider.Id = id;

                // Publish provider created event
                PublishEventFireAndForget(new ProviderCreated
                {
                    ProviderId = id,
                    ProviderType = provider.ProviderType.ToString(),
                    ProviderName = provider.ProviderName,
                    BaseUrl = provider.BaseUrl,
                    IsEnabled = provider.IsEnabled,
                    CreatedAt = provider.CreatedAt,
                    CorrelationId = Guid.NewGuid().ToString()
                }, "create provider");

                return CreatedAtAction(nameof(GetProviderById), new { id = provider.Id }, new
                {
                    provider.Id,
                    provider.ProviderType,
                    provider.ProviderName,
                    provider.BaseUrl,
                    provider.IsEnabled,
                    provider.CreatedAt,
                    provider.UpdatedAt,
                    KeyCount = 0
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation when creating provider");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating provider");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Updates a provider
        /// </summary>
        /// <param name="id">The ID of the provider to update</param>
        /// <param name="request">The update request containing new provider values</param>
        /// <returns>No content if successful</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateProvider(int id, [FromBody] UpdateProviderRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var provider = await _providerRepository.GetByIdAsync(id);
                if (provider == null)
                {
                    _logger.LogWarning("Provider not found for update {ProviderId}", id);
                    return NotFound(new { error = "Provider not found" });
                }

                var changedProperties = new List<string>();
                
                if (!string.IsNullOrEmpty(request.ProviderName) && provider.ProviderName != request.ProviderName)
                {
                    provider.ProviderName = request.ProviderName;
                    changedProperties.Add("ProviderName");
                }
                
                if (provider.BaseUrl != request.BaseUrl)
                {
                    provider.BaseUrl = request.BaseUrl;
                    changedProperties.Add("BaseUrl");
                }
                
                if (provider.IsEnabled != request.IsEnabled)
                {
                    provider.IsEnabled = request.IsEnabled;
                    changedProperties.Add("IsEnabled");
                }

                provider.UpdatedAt = DateTime.UtcNow;
                
                await _providerRepository.UpdateAsync(provider);

                // Publish provider updated event
                if (changedProperties.Any())
                {
                    PublishEventFireAndForget(new ProviderUpdated
                    {
                        ProviderId = id,
                        IsEnabled = provider.IsEnabled,
                        ChangedProperties = changedProperties.ToArray(),
                        CorrelationId = Guid.NewGuid().ToString()
                    }, "update provider", new { ProviderId = id, ChangedProperties = string.Join(", ", changedProperties) });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating provider with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Deletes a provider
        /// </summary>
        /// <param name="id">The ID of the provider to delete</param>
        /// <returns>No content if successful</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteProvider(int id)
        {
            try
            {
                var provider = await _providerRepository.GetByIdAsync(id);
                if (provider == null)
                {
                    _logger.LogWarning("Provider not found for deletion {ProviderId}", id);
                    return NotFound(new { error = "Provider not found" });
                }

                await _providerRepository.DeleteAsync(id);

                // Publish provider deleted event
                PublishEventFireAndForget(new ProviderDeleted
                {
                    ProviderId = id,
                    CorrelationId = Guid.NewGuid().ToString()
                }, "delete provider", new { ProviderId = id });

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting provider with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Tests the connection to a provider
        /// </summary>
        /// <param name="id">The ID of the provider to test</param>
        /// <returns>The test result</returns>
        [HttpPost("test/{id}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> TestProviderConnection(int id)
        {
            try
            {
                var provider = await _providerRepository.GetByIdAsync(id);
                if (provider == null)
                {
                    return NotFound(new { error = "Provider not found" });
                }

                // Get a client for this provider to test
                var client = _clientFactory.GetClientByProviderId(id);
                
                // Perform a simple test - list models
                try
                {
                    var models = await client.ListModelsAsync();
                    return Ok(new
                    {
                        Success = true,
                        ProviderId = id,
                        ProviderType = provider.ProviderType.ToString(),
                        ProviderName = provider.ProviderName,
                        ModelCount = models?.Count() ?? 0,
                        ResponseTime = DateTime.UtcNow,
                        Message = "Connection successful"
                    });
                }
                catch (Exception testEx)
                {
                    return Ok(new
                    {
                        Success = false,
                        ProviderId = id,
                        ProviderType = provider.ProviderType.ToString(),
                        ProviderName = provider.ProviderName,
                        ModelCount = 0,
                        ResponseTime = DateTime.UtcNow,
                        Message = testEx.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection for provider with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Tests a provider connection without saving
        /// </summary>
        /// <returns>The test result</returns>
        [HttpPost("test")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> TestProviderConnectionWithCredentials([FromBody] TestProviderRequest testRequest)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Create a temporary provider for testing
                var testProvider = new Provider
                {
                    Id = -1, // Temporary ID
                    ProviderType = testRequest.ProviderType,
                    ProviderName = "Test Provider",
                    BaseUrl = testRequest.BaseUrl,
                    IsEnabled = true
                };

                // Create a temporary key if provided
                if (!string.IsNullOrEmpty(testRequest.ApiKey))
                {
                    testProvider.ProviderKeyCredentials = new List<ProviderKeyCredential>
                    {
                        new ProviderKeyCredential
                        {
                            ApiKey = testRequest.ApiKey,
                            Organization = testRequest.Organization,
                            IsPrimary = true,
                            IsEnabled = true
                        }
                    };
                }

                // Test the connection
                var testKey = new ProviderKeyCredential 
                { 
                    ApiKey = testRequest.ApiKey, 
                    BaseUrl = testRequest.BaseUrl,
                    Organization = testRequest.Organization,
                    IsPrimary = true,
                    IsEnabled = true
                };
                var client = _clientFactory.CreateTestClient(testProvider, testKey);
                
                try
                {
                    var models = await client.ListModelsAsync();
                    return Ok(new
                    {
                        Success = true,
                        ProviderType = testProvider.ProviderType.ToString(),
                        ModelCount = models?.Count() ?? 0,
                        ResponseTime = DateTime.UtcNow,
                        Message = "Connection successful"
                    });
                }
                catch (Exception testEx)
                {
                    return Ok(new
                    {
                        Success = false,
                        ProviderType = testProvider.ProviderType.ToString(),
                        ModelCount = 0,
                        ResponseTime = DateTime.UtcNow,
                        Message = testEx.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection for provider {ProviderType}", testRequest?.ProviderType.ToString() ?? "unknown");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

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
                var keys = await _keyRepository.GetByProviderIdAsync(providerId);
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
                _logger.LogError(ex, "Error getting key credentials for provider {ProviderId}", providerId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
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
                    _logger.LogWarning("Key credential not found {KeyId} for provider {ProviderId}", keyId, providerId);
                    return NotFound(new { error = "Key credential not found" });
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
                _logger.LogError(ex, "Error getting key credential {KeyId} for provider {ProviderId}", keyId, providerId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Verify provider exists
                var provider = await _providerRepository.GetByIdAsync(providerId);
                if (provider == null)
                {
                    return NotFound(new { error = "Provider not found" });
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

                var createdKey = await _keyRepository.CreateAsync(keyCredential);

                // Publish key created event
                PublishEventFireAndForget(new ConduitLLM.Configuration.Events.ProviderKeyCredentialCreated
                {
                    KeyId = createdKey.Id,
                    ProviderId = providerId,
                    IsPrimary = keyCredential.IsPrimary,
                    IsEnabled = keyCredential.IsEnabled,
                    CorrelationId = Guid.NewGuid()
                }, "create provider key", new { ProviderId = providerId, KeyId = createdKey.Id });

                return CreatedAtAction(
                    nameof(GetProviderKeyCredential), 
                    new { providerId = providerId, keyId = createdKey.Id }, 
                    new
                    {
                        createdKey.Id,
                        createdKey.ProviderId,
                        createdKey.KeyName,
                        createdKey.IsPrimary,
                        createdKey.IsEnabled,
                        createdKey.ProviderAccountGroup,
                        ApiKey = createdKey.ApiKey != null ? "***" + createdKey.ApiKey.Substring(Math.Max(0, createdKey.ApiKey.Length - 4)) : "***",
                        createdKey.Organization,
                        createdKey.BaseUrl,
                        createdKey.CreatedAt,
                        createdKey.UpdatedAt
                    });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Controller caught InvalidOperationException of type {ExceptionType} for provider {ProviderId}: {Message}", 
                    ex.GetType().FullName, providerId, ex.Message);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Controller caught general Exception of type {ExceptionType} for provider {ProviderId}: {Message}", 
                    ex.GetType().FullName, providerId, ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred." });
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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var key = await _keyRepository.GetByIdAsync(keyId);
                if (key == null || key.ProviderId != providerId)
                {
                    _logger.LogWarning("Key credential not found for update {KeyId}", keyId);
                    return NotFound(new { error = "Key credential not found" });
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
                _logger.LogWarning(ex, "Invalid operation when updating key credential {KeyId}", keyId);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating key credential {KeyId}", keyId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
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
                    _logger.LogWarning("Key credential not found for deletion {KeyId}", keyId);
                    return NotFound(new { error = "Key credential not found" });
                }

                await _keyRepository.DeleteAsync(keyId);

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
                _logger.LogWarning(ex, "Invalid operation when deleting key credential {KeyId}", keyId);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting key credential {KeyId}", keyId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
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
                    _logger.LogWarning("Key credential not found {KeyId} for provider {ProviderId}", keyId, providerId);
                    return NotFound(new { error = "Key credential not found" });
                }

                // Unset all other primary keys for this provider
                var allKeys = await _keyRepository.GetByProviderIdAsync(providerId);
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
                _logger.LogWarning(ex, "Invalid operation when setting primary key {KeyId} for provider {ProviderId}", keyId, providerId);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting primary key {KeyId} for provider {ProviderId}", keyId, providerId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Tests a specific key credential
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <param name="keyId">The ID of the key to test</param>
        /// <returns>The test result</returns>
        [HttpPost("{providerId}/keys/{keyId}/test")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> TestProviderKeyCredential(int providerId, int keyId)
        {
            try
            {
                var key = await _keyRepository.GetByIdAsync(keyId);
                if (key == null || key.ProviderId != providerId)
                {
                    return NotFound(new { error = "Key credential not found" });
                }

                var provider = await _providerRepository.GetByIdAsync(providerId);
                if (provider == null)
                {
                    return NotFound(new { error = "Provider not found" });
                }

                // Test the connection with this specific key
                var client = _clientFactory.CreateTestClient(provider, key);
                
                try
                {
                    var models = await client.ListModelsAsync();
                    return Ok(new
                    {
                        Success = true,
                        ProviderId = providerId,
                        KeyId = keyId,
                        KeyName = key.KeyName,
                        ProviderType = provider.ProviderType.ToString(),
                        ProviderName = provider.ProviderName,
                        ModelCount = models?.Count() ?? 0,
                        ResponseTime = DateTime.UtcNow,
                        Message = "Connection successful"
                    });
                }
                catch (Exception testEx)
                {
                    return Ok(new
                    {
                        Success = false,
                        ProviderId = providerId,
                        KeyId = keyId,
                        KeyName = key.KeyName,
                        ProviderType = provider.ProviderType.ToString(),
                        ProviderName = provider.ProviderName,
                        ModelCount = 0,
                        ResponseTime = DateTime.UtcNow,
                        Message = testEx.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing key credential {KeyId} for provider {ProviderId}", keyId, providerId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }
    }

    /// <summary>
    /// Request model for creating a provider
    /// </summary>
    public class CreateProviderRequest
    {
        /// <summary>
        /// The type of provider to create
        /// </summary>
        public ProviderType ProviderType { get; set; }
        
        /// <summary>
        /// The name of the provider
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;
        
        /// <summary>
        /// The base URL for the provider (optional)
        /// </summary>
        public string? BaseUrl { get; set; }
        
        /// <summary>
        /// Whether the provider is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// Request model for updating a provider
    /// </summary>
    public class UpdateProviderRequest
    {
        /// <summary>
        /// The new name for the provider (optional)
        /// </summary>
        public string? ProviderName { get; set; }
        
        /// <summary>
        /// The new base URL for the provider (optional)
        /// </summary>
        public string? BaseUrl { get; set; }
        
        /// <summary>
        /// Whether the provider is enabled
        /// </summary>
        public bool IsEnabled { get; set; }
    }

    /// <summary>
    /// Request model for testing a provider connection
    /// </summary>
    public class TestProviderRequest
    {
        /// <summary>
        /// The type of provider to test
        /// </summary>
        public ProviderType ProviderType { get; set; }
        
        /// <summary>
        /// The API key to test
        /// </summary>
        public string? ApiKey { get; set; }
        
        /// <summary>
        /// The base URL to test (optional)
        /// </summary>
        public string? BaseUrl { get; set; }
        
        /// <summary>
        /// The organization to test (optional)
        /// </summary>
        public string? Organization { get; set; }
    }

    /// <summary>
    /// Event published when a provider is updated
    /// </summary>
    public class ProviderUpdated
    {
        /// <summary>
        /// The ID of the updated provider
        /// </summary>
        public int ProviderId { get; set; }
        
        /// <summary>
        /// Whether the provider is enabled after the update
        /// </summary>
        public bool IsEnabled { get; set; }
        
        /// <summary>
        /// List of properties that were changed
        /// </summary>
        public string[] ChangedProperties { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// Correlation ID for tracking the event
        /// </summary>
        public string CorrelationId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Event published when a provider is deleted
    /// </summary>
    public class ProviderDeleted
    {
        /// <summary>
        /// The ID of the deleted provider
        /// </summary>
        public int ProviderId { get; set; }
        
        /// <summary>
        /// Correlation ID for tracking the event
        /// </summary>
        public string CorrelationId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request model for creating a key credential
    /// </summary>
    public class CreateKeyRequest
    {
        /// <summary>
        /// The API key to create
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;
        
        /// <summary>
        /// The name for the key credential
        /// </summary>
        public string KeyName { get; set; } = string.Empty;
        
        /// <summary>
        /// The organization for the key (optional)
        /// </summary>
        public string? Organization { get; set; }
        
        /// <summary>
        /// The base URL for the key (optional)
        /// </summary>
        public string? BaseUrl { get; set; }
        
        /// <summary>
        /// Whether this is the primary key for the provider
        /// </summary>
        public bool IsPrimary { get; set; }
        
        /// <summary>
        /// Whether the key is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// The provider account group (optional)
        /// </summary>
        public int? ProviderAccountGroup { get; set; }
    }

    /// <summary>
    /// Request model for updating a key credential
    /// </summary>
    public class UpdateKeyRequest
    {
        /// <summary>
        /// The new name for the key (optional)
        /// </summary>
        public string? KeyName { get; set; }
        
        /// <summary>
        /// The new API key (optional)
        /// </summary>
        public string? ApiKey { get; set; }
        
        /// <summary>
        /// The new organization (optional)
        /// </summary>
        public string? Organization { get; set; }
        
        /// <summary>
        /// The new base URL (optional)
        /// </summary>
        public string? BaseUrl { get; set; }
        
        /// <summary>
        /// Whether this should be the primary key (optional)
        /// </summary>
        public bool? IsPrimary { get; set; }
        
        /// <summary>
        /// Whether the key is enabled (optional)
        /// </summary>
        public bool? IsEnabled { get; set; }
        
        /// <summary>
        /// The provider account group (optional)
        /// </summary>
        public int? ProviderAccountGroup { get; set; }
    }


}

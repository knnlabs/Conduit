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

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing provider credentials
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public partial class ProviderCredentialsController : EventPublishingControllerBase
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
    }
}
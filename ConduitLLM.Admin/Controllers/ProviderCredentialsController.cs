using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Core.Extensions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using static ConduitLLM.Core.Extensions.LoggingSanitizer;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing provider credentials
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class ProviderCredentialsController : ControllerBase
    {
        private readonly IAdminProviderCredentialService _providerCredentialService;
        private readonly ILogger<ProviderCredentialsController> _logger;

        /// <summary>
        /// Initializes a new instance of the ProviderCredentialsController
        /// </summary>
        /// <param name="providerCredentialService">The provider credential service</param>
        /// <param name="logger">The logger</param>
        public ProviderCredentialsController(
            IAdminProviderCredentialService providerCredentialService,
            ILogger<ProviderCredentialsController> logger)
        {
            _providerCredentialService = providerCredentialService ?? throw new ArgumentNullException(nameof(providerCredentialService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all provider credentials
        /// </summary>
        /// <returns>List of all provider credentials</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ProviderCredentialDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllProviderCredentials()
        {
            try
            {
                var credentials = await _providerCredentialService.GetAllProviderCredentialsAsync();
                return Ok(credentials);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all provider credentials");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Gets a provider credential by ID
        /// </summary>
        /// <param name="id">The ID of the provider credential</param>
        /// <returns>The provider credential</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ProviderCredentialDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetProviderCredentialById(int id)
        {
            try
            {
                var credential = await _providerCredentialService.GetProviderCredentialByIdAsync(id);

                if (credential == null)
                {
                    _logger.LogWarning("Provider credential not found {ProviderId}", id);
                    return NotFound(new { error = "Provider credential not found" });
                }

                return Ok(credential);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider credential with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Creates a new provider credential
        /// </summary>
        /// <param name="credential">The provider credential to create</param>
        /// <returns>The created provider credential</returns>
        [HttpPost]
        [ProducesResponseType(typeof(ProviderCredentialDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateProviderCredential([FromBody] CreateProviderCredentialDto credential)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var createdCredential = await _providerCredentialService.CreateProviderCredentialAsync(credential);
                return CreatedAtAction(nameof(GetProviderCredentialById), new { id = createdCredential.Id }, createdCredential);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation when creating provider credential");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating provider credential");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Updates a provider credential
        /// </summary>
        /// <param name="id">The ID of the provider credential to update</param>
        /// <param name="credential">The updated provider credential data</param>
        /// <returns>No content if successful</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateProviderCredential(int id, [FromBody] UpdateProviderCredentialDto credential)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Ensure ID in route matches ID in body
            if (id != credential.Id)
            {
                return BadRequest("ID in route must match ID in body");
            }

            try
            {
                var success = await _providerCredentialService.UpdateProviderCredentialAsync(credential);

                if (!success)
                {
                    _logger.LogWarning("Provider credential not found for update {ProviderId}", id);
                    return NotFound(new { error = "Provider credential not found" });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating provider credential with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Deletes a provider credential
        /// </summary>
        /// <param name="id">The ID of the provider credential to delete</param>
        /// <returns>No content if successful</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteProviderCredential(int id)
        {
            try
            {
                var success = await _providerCredentialService.DeleteProviderCredentialAsync(id);

                if (!success)
                {
                    _logger.LogWarning("Provider credential not found for deletion {ProviderId}", id);
                    return NotFound(new { error = "Provider credential not found" });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting provider credential with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Tests the connection to a provider
        /// </summary>
        /// <param name="id">The ID of the provider credential to test</param>
        /// <returns>The test result</returns>
        [HttpPost("test/{id}")]
        [ProducesResponseType(typeof(ProviderConnectionTestResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> TestProviderConnection(int id)
        {
            try
            {
                // We need to fetch the credential without masking the API key for testing
                // So we create a special DTO with the ID set for the service to fetch internally
                var testCredential = new ProviderCredentialDto { Id = id };
                
                var result = await _providerCredentialService.TestProviderConnectionAsync(testCredential);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection for provider credential with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Tests a provider connection without saving credentials
        /// </summary>
        /// <param name="testRequest">The provider credentials to test</param>
        /// <returns>The test result</returns>
        [HttpPost("test")]
        [ProducesResponseType(typeof(ProviderConnectionTestResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> TestProviderConnectionWithCredentials([FromBody] TestProviderConnectionDto testRequest)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _providerCredentialService.TestProviderConnectionAsync(testRequest);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection for provider {ProviderType}", (testRequest?.ProviderType.ToString() ?? "unknown").Replace(Environment.NewLine, ""));
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Gets all key credentials for a specific provider
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <returns>List of key credentials for the provider</returns>
        [HttpGet("{providerId}/keys")]
        [ProducesResponseType(typeof(IEnumerable<ProviderKeyCredentialDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetProviderKeyCredentials(int providerId)
        {
            try
            {
                var keys = await _providerCredentialService.GetProviderKeyCredentialsAsync(providerId);
                return Ok(keys);
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
        [ProducesResponseType(typeof(ProviderKeyCredentialDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetProviderKeyCredential(int providerId, int keyId)
        {
            try
            {
                var key = await _providerCredentialService.GetProviderKeyCredentialAsync(keyId);
                
                if (key == null || key.ProviderCredentialId != providerId)
                {
                    _logger.LogWarning("Key credential not found {KeyId} for provider {ProviderId}", keyId, providerId);
                    return NotFound(new { error = "Key credential not found" });
                }

                return Ok(key);
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
        /// <param name="createDto">The key credential to create</param>
        /// <returns>The created key credential</returns>
        [HttpPost("{providerId}/keys")]
        [ProducesResponseType(typeof(ProviderKeyCredentialDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateProviderKeyCredential(int providerId, [FromBody] CreateProviderKeyCredentialDto createDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var createdKey = await _providerCredentialService.CreateProviderKeyCredentialAsync(providerId, createDto);
                return CreatedAtAction(
                    nameof(GetProviderKeyCredential), 
                    new { providerId = providerId, keyId = createdKey.Id }, 
                    createdKey);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation when creating key credential for provider {ProviderId}", providerId);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating key credential for provider {ProviderId}", providerId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Updates a key credential
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <param name="keyId">The ID of the key</param>
        /// <param name="updateDto">The updated key credential data</param>
        /// <returns>No content if successful</returns>
        [HttpPut("{providerId}/keys/{keyId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateProviderKeyCredential(int providerId, int keyId, [FromBody] UpdateProviderKeyCredentialDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (keyId != updateDto.Id)
            {
                return BadRequest(new { error = "ID in route must match ID in body" });
            }

            try
            {
                var success = await _providerCredentialService.UpdateProviderKeyCredentialAsync(keyId, updateDto);
                
                if (!success)
                {
                    _logger.LogWarning("Key credential not found for update {KeyId}", keyId);
                    return NotFound(new { error = "Key credential not found" });
                }

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
                var success = await _providerCredentialService.DeleteProviderKeyCredentialAsync(keyId);
                
                if (!success)
                {
                    _logger.LogWarning("Key credential not found for deletion {KeyId}", keyId);
                    return NotFound(new { error = "Key credential not found" });
                }

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
                var success = await _providerCredentialService.SetPrimaryKeyAsync(providerId, keyId);
                
                if (!success)
                {
                    _logger.LogWarning("Key credential not found {KeyId} for provider {ProviderId}", keyId, providerId);
                    return NotFound(new { error = "Key credential not found" });
                }

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
        [ProducesResponseType(typeof(ProviderConnectionTestResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> TestProviderKeyCredential(int providerId, int keyId)
        {
            try
            {
                var result = await _providerCredentialService.TestProviderKeyCredentialAsync(providerId, keyId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing key credential {KeyId} for provider {ProviderId}", keyId, providerId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }
    }
}

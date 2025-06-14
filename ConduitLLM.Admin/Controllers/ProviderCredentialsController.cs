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
                    _logger.LogWarning("Provider credential not found {ProviderId}", S(id));
                    return NotFound(new { error = "Provider credential not found", id = id });
                }

                return Ok(credential);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider credential with ID {Id}", S(id));
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Gets a provider credential by provider name
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <returns>The provider credential</returns>
        [HttpGet("name/{providerName}")]
        [ProducesResponseType(typeof(ProviderCredentialDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetProviderCredentialByName(string providerName)
        {
            try
            {
                var credential = await _providerCredentialService.GetProviderCredentialByNameAsync(providerName);

                if (credential == null)
                {
                    _logger.LogWarning("Provider credential not found for provider {ProviderName}", S(providerName));
                    return NotFound(new { error = "Provider credential not found", provider = providerName });
                }

                return Ok(credential);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider credential for '{ProviderName}'", S(providerName));
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Gets all provider names
        /// </summary>
        /// <returns>List of provider names</returns>
        [HttpGet("names")]
        [ProducesResponseType(typeof(IEnumerable<ProviderDataDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllProviderNames()
        {
            try
            {
                var providerNames = await _providerCredentialService.GetAllProviderNamesAsync();
                return Ok(providerNames);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all provider names");
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
                    _logger.LogWarning("Provider credential not found for update {ProviderId}", S(id));
                    return NotFound(new { error = "Provider credential not found", id = id });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating provider credential with ID {Id}", S(id));
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
                    _logger.LogWarning("Provider credential not found for deletion {ProviderId}", S(id));
                    return NotFound(new { error = "Provider credential not found", id = id });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting provider credential with ID {Id}", S(id));
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
                var credential = await _providerCredentialService.GetProviderCredentialByIdAsync(id);

                if (credential == null)
                {
                    _logger.LogWarning("Provider credential not found for connection test {ProviderId}", S(id));
                    return NotFound(new { error = "Provider credential not found", id = id });
                }

                var result = await _providerCredentialService.TestProviderConnectionAsync(credential);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection for provider credential with ID {Id}", S(id));
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
        public async Task<IActionResult> TestProviderConnectionWithCredentials([FromBody] ProviderCredentialDto testRequest)
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
                _logger.LogError(ex, "Error testing connection for provider {ProviderName}", S(testRequest?.ProviderName));
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }
    }
}

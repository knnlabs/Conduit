using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Filters;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Functions.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers;

/// <summary>
/// Controller for managing function credentials.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "MasterKeyPolicy")]
[ServiceFilter(typeof(OperationLoggingFilter))]
public class FunctionCredentialsController : AdminControllerBase
{
    private readonly IFunctionCredentialRepository _credentialRepository;
    private readonly IFunctionConfigurationRepository _configurationRepository;
    private readonly IFunctionClientFactory _clientFactory;

    /// <summary>
    /// Initializes a new instance of the FunctionCredentialsController.
    /// </summary>
    public FunctionCredentialsController(
        IFunctionCredentialRepository credentialRepository,
        IFunctionConfigurationRepository configurationRepository,
        IFunctionClientFactory clientFactory,
        ILogger<FunctionCredentialsController> logger)
        : base(logger)
    {
        _credentialRepository = credentialRepository ?? throw new ArgumentNullException(nameof(credentialRepository));
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    }

    /// <summary>
    /// Gets all function credentials.
    /// </summary>
    /// <returns>List of all credentials</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllCredentials()
    {
        var credentials = await _credentialRepository.GetAllUnboundedAsync();
        return Ok(credentials);
    }

    /// <summary>
    /// Gets credentials for a specific function configuration (returns all credentials for the configuration's provider type).
    /// </summary>
    /// <param name="functionConfigurationId">The function configuration ID</param>
    /// <returns>List of credentials for the configuration's provider type</returns>
    [HttpGet("configuration/{functionConfigurationId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCredentialsByConfiguration(int functionConfigurationId)
    {
        // Get the configuration to determine its provider type
        var configuration = await _configurationRepository.GetByIdAsync(functionConfigurationId);
        if (configuration == null)
        {
            throw new KeyNotFoundException();
        }

        // Get credentials for this provider type
        var credentials = await _credentialRepository.GetByProviderTypeAsync(
            configuration.ProviderType);
        return Ok(credentials);
    }

    /// <summary>
    /// Gets a credential by ID.
    /// </summary>
    /// <param name="id">The ID of the credential</param>
    /// <returns>The credential</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCredentialById(int id)
    {
        var credential = await _credentialRepository.GetByIdAsync(id);
        if (credential == null)
        {
            return this.NotFoundEntity("Function credential", id);
        }
        return Ok(credential);
    }

    /// <summary>
    /// Creates a new function credential.
    /// </summary>
    /// <param name="credential">The credential to create</param>
    /// <returns>The created credential</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCredential(
        [FromBody] ConduitLLM.Functions.Entities.FunctionCredential credential)
    {
        if (credential == null)
        {
            return BadRequest(new ErrorResponseDto("Function credential data is required"));
        }

        int id = await _credentialRepository.CreateAsync(credential);

        // Fetch the created entity to return
        var created = await _credentialRepository.GetByIdAsync(id);

        LogAdminAudit("Created", "FunctionCredential", id, $"ProviderType: {credential.ProviderType}");
        return CreatedAtAction(
            nameof(GetCredentialById),
            new { id },
            created);
    }

    /// <summary>
    /// Updates an existing function credential.
    /// </summary>
    /// <param name="id">The ID of the credential to update</param>
    /// <param name="credential">The updated credential data</param>
    /// <returns>The updated credential</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCredential(
        int id,
        [FromBody] ConduitLLM.Functions.Entities.FunctionCredential credential)
    {
        if (credential == null)
        {
            return BadRequest(new ErrorResponseDto("Function credential data is required"));
        }

        if (id != credential.Id)
        {
            return BadRequest(new ErrorResponseDto("ID mismatch"));
        }

        await _credentialRepository.UpdateAsync(credential);

        // Fetch the updated entity to return
        var updated = await _credentialRepository.GetByIdAsync(id);

        if (updated == null)
        {
            throw new KeyNotFoundException();
        }

        LogAdminAudit("Updated", "FunctionCredential", id, $"ProviderType: {credential.ProviderType}");
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a function credential.
    /// </summary>
    /// <param name="id">The ID of the credential to delete</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCredential(int id)
    {
        var credential = await _credentialRepository.GetByIdAsync(id);
        await _credentialRepository.DeleteAsync(id);
        LogAdminAudit("Deleted", "FunctionCredential", id, credential != null ? $"ProviderType: {credential.ProviderType}" : null);
        return NoContent();
    }

    /// <summary>
    /// Tests a function credential by verifying authentication.
    /// </summary>
    /// <param name="testRequest">Test request containing configuration ID and optional API key override</param>
    /// <returns>Test result indicating whether authentication succeeded</returns>
    [HttpPost("test")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TestCredential([FromBody] TestCredentialRequest testRequest)
    {
        if (testRequest == null)
        {
            return BadRequest(new ErrorResponseDto("Test request data is required"));
        }

        // Get the credential
        var credential = await _credentialRepository.GetByIdAsync(testRequest.CredentialId);
        if (credential == null)
        {
            throw new KeyNotFoundException();
        }

        // Get any configuration that uses this provider type (for client factory)
        var configurations = await _configurationRepository.GetByProviderTypeAsync(credential.ProviderType);
        var configuration = configurations.FirstOrDefault();
        if (configuration == null)
        {
            throw new KeyNotFoundException();
        }

        // Create client and test authentication
        var client = await _clientFactory.GetClientAsync(
            credential.ProviderType,
            configuration.Id);

        var authResult = await client.VerifyAuthenticationAsync(
            testRequest.ApiKeyOverride ?? credential.ApiKey);

        return Ok(new
        {
            success = authResult.IsSuccess,
            message = authResult.Message,
            details = authResult.Details,
            durationMs = authResult.ResponseTimeMs
        });
    }

    /// <summary>
    /// Request model for testing credentials.
    /// </summary>
    public class TestCredentialRequest
    {
        /// <summary>
        /// The credential ID to test.
        /// </summary>
        public int CredentialId { get; set; }

        /// <summary>
        /// Optional API key to override the stored credential for testing.
        /// </summary>
        public string? ApiKeyOverride { get; set; }
    }
}

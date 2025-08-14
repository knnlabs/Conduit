using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ConduitLLM.Admin.Models;
using ConduitLLM.Admin.Services;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for authentication-related operations in the Admin API
    /// </summary>
    [ApiController]
    [Route("api/admin/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IEphemeralMasterKeyService _ephemeralMasterKeyService;
        private readonly ILogger<AuthController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthController"/> class.
        /// </summary>
        /// <param name="ephemeralMasterKeyService">The ephemeral master key service</param>
        /// <param name="logger">The logger</param>
        public AuthController(
            IEphemeralMasterKeyService ephemeralMasterKeyService,
            ILogger<AuthController> logger)
        {
            _ephemeralMasterKeyService = ephemeralMasterKeyService ?? throw new ArgumentNullException(nameof(ephemeralMasterKeyService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Generate an ephemeral master key for Admin API authentication
        /// </summary>
        /// <returns>The ephemeral master key and expiration information</returns>
        /// <response code="200">Ephemeral master key generated successfully</response>
        /// <response code="401">Authentication failed - master key required</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("ephemeral-master-key")]
        [Authorize(Policy = "MasterKeyPolicy")]
        [ProducesResponseType(typeof(EphemeralMasterKeyResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<EphemeralMasterKeyResponse>> GenerateEphemeralMasterKey()
        {
            try
            {
                // Create ephemeral master key
                var response = await _ephemeralMasterKeyService.CreateEphemeralMasterKeyAsync();

                _logger.LogInformation("Generated ephemeral master key");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate ephemeral master key");
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Internal Server Error",
                    Detail = "Failed to generate ephemeral master key"
                });
            }
        }
    }
}

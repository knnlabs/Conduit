using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Admin.Models;
using ConduitLLM.Admin.Services;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for authentication-related operations in the Admin API
    /// </summary>
    [ApiController]
    [Route("api/admin/auth")]
    public class AuthController : AdminControllerBase
    {
        private readonly IEphemeralMasterKeyService _ephemeralMasterKeyService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthController"/> class.
        /// </summary>
        /// <param name="ephemeralMasterKeyService">The ephemeral master key service</param>
        /// <param name="logger">The logger</param>
        public AuthController(
            IEphemeralMasterKeyService ephemeralMasterKeyService,
            ILogger<AuthController> logger)
            : base(logger)
        {
            _ephemeralMasterKeyService = ephemeralMasterKeyService ?? throw new ArgumentNullException(nameof(ephemeralMasterKeyService));
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
        public Task<IActionResult> GenerateEphemeralMasterKey()
        {
            return ExecuteAsync(
                async () =>
                {
                    // Create ephemeral master key
                    var response = await _ephemeralMasterKeyService.CreateEphemeralMasterKeyAsync();

                    Logger.LogInformation("Generated ephemeral master key");

                    return response;
                },
                Ok,
                "GenerateEphemeralMasterKey");
        }
    }
}

using ConduitLLM.Core.Controllers;
using ConduitLLM.Core.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Gateway.Models;
using ConduitLLM.Gateway.Services;

namespace ConduitLLM.Gateway.Controllers
{
    /// <summary>
    /// Controller for authentication-related operations
    /// </summary>
    [ApiController]
    [Route("v1/auth")]
    [Tags("Authentication")]
    public class AuthController : GatewayControllerBase
    {
        private readonly IEphemeralKeyService _ephemeralKeyService;

        public AuthController(
            IEphemeralKeyService ephemeralKeyService,
            ILogger<AuthController> logger)
            : base(logger)
        {
            _ephemeralKeyService = ephemeralKeyService ?? throw new ArgumentNullException(nameof(ephemeralKeyService));
        }

        /// <summary>
        /// Generate an ephemeral key for the authenticated virtual key
        /// </summary>
        /// <param name="request">Optional metadata for the ephemeral key</param>
        /// <returns>The ephemeral key and expiration information</returns>
        /// <response code="200">Ephemeral key generated successfully</response>
        /// <response code="401">Authentication failed</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("ephemeral-key")]
        [Authorize(AuthenticationSchemes = "VirtualKey")]
        [ProducesResponseType(typeof(EphemeralKeyResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> GenerateEphemeralKey([FromBody] GenerateEphemeralKeyRequest? request = null)
        {
            return ExecuteAsync(async () =>
            {
                // Get virtual key ID from claims
                var virtualKeyIdClaim = HttpContext.User.FindFirst("VirtualKeyId")?.Value;
                if (string.IsNullOrEmpty(virtualKeyIdClaim) || !int.TryParse(virtualKeyIdClaim, out int virtualKeyId))
                {
                    Logger.LogWarning("Failed to extract virtual key ID from claims");
                    return Unauthorized(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = "Virtual key not found in request context",
                            Type = "authentication_error",
                            Code = "unauthorized"
                        }
                    });
                }

                // Get the actual virtual key value from claims
                var virtualKey = HttpContext.User.FindFirst("VirtualKey")?.Value;
                if (string.IsNullOrEmpty(virtualKey))
                {
                    Logger.LogWarning("Failed to extract virtual key from claims");
                    return Unauthorized(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = "Virtual key not found in request context",
                            Type = "authentication_error",
                            Code = "unauthorized"
                        }
                    });
                }

                // Create ephemeral key with the actual virtual key
                var response = await _ephemeralKeyService.CreateEphemeralKeyAsync(
                    virtualKeyId,
                    virtualKey,
                    request?.Metadata);

                Logger.LogInformation("Generated ephemeral key for virtual key {VirtualKeyId}", virtualKeyId);

                return Ok(response);
            }, nameof(GenerateEphemeralKey));
        }
    }

    /// <summary>
    /// Request for generating an ephemeral key
    /// </summary>
    public class GenerateEphemeralKeyRequest
    {
        /// <summary>
        /// Optional metadata about the ephemeral key request
        /// </summary>
        public EphemeralKeyMetadata? Metadata { get; set; }
    }
}

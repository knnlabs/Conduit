using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ConduitLLM.Http.Models;
using ConduitLLM.Http.Services;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Controller for authentication-related operations
    /// </summary>
    [ApiController]
    [Route("v1/auth")]
    [Tags("Authentication")]
    public class AuthController : ControllerBase
    {
        private readonly IEphemeralKeyService _ephemeralKeyService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IEphemeralKeyService ephemeralKeyService,
            ILogger<AuthController> logger)
        {
            _ephemeralKeyService = ephemeralKeyService ?? throw new ArgumentNullException(nameof(ephemeralKeyService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GenerateEphemeralKey([FromBody] GenerateEphemeralKeyRequest? request = null)
        {
            try
            {
                // Get virtual key ID from claims
                var virtualKeyIdClaim = HttpContext.User.FindFirst("VirtualKeyId")?.Value;
                if (string.IsNullOrEmpty(virtualKeyIdClaim) || !int.TryParse(virtualKeyIdClaim, out int virtualKeyId))
                {
                    _logger.LogWarning("Failed to extract virtual key ID from claims");
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "Unauthorized",
                        Detail = "Virtual key not found in request context"
                    });
                }

                // Get the actual virtual key value from claims
                var virtualKey = HttpContext.User.FindFirst("VirtualKey")?.Value;
                if (string.IsNullOrEmpty(virtualKey))
                {
                    _logger.LogWarning("Failed to extract virtual key from claims");
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "Unauthorized",
                        Detail = "Virtual key not found in request context"
                    });
                }

                // Create ephemeral key with the actual virtual key
                var response = await _ephemeralKeyService.CreateEphemeralKeyAsync(
                    virtualKeyId, 
                    virtualKey,
                    request?.Metadata);

                _logger.LogInformation("Generated ephemeral key for virtual key {VirtualKeyId}", virtualKeyId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate ephemeral key");
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Internal Server Error",
                    Detail = "Failed to generate ephemeral key"
                });
            }
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
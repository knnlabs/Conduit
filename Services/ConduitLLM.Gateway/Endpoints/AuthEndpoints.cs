using ConduitLLM.Core.Models;

using Microsoft.AspNetCore.Authorization;
using ConduitLLM.Gateway.Filters;
using ConduitLLM.Gateway.Models;
using ConduitLLM.Gateway.Services;

namespace ConduitLLM.Gateway.Endpoints
{
    /// <summary>
    /// Controller for authentication-related operations
    /// </summary>
    public class AuthEndpoints : GatewayEndpointHandlerBase
    {
        private readonly IEphemeralKeyService _ephemeralKeyService;

        public AuthEndpoints(
            IEphemeralKeyService ephemeralKeyService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuthEndpoints> logger)
            : base(null, httpContextAccessor, logger)
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
        public async Task<IResult> GenerateEphemeralKey(GenerateEphemeralKeyRequest? request = null)
        {
            // Get virtual key ID from claims
            var virtualKeyIdClaim = HttpContext.User.FindFirst("VirtualKeyId")?.Value;
            if (string.IsNullOrEmpty(virtualKeyIdClaim) || !int.TryParse(virtualKeyIdClaim, out int virtualKeyId))
            {
                Logger.LogWarning("Failed to extract virtual key ID from claims");
                return OpenAIError(401, "Virtual key not found in request context", "unauthorized", "authentication_error");
            }

            // Get the actual virtual key value from claims
            var virtualKey = HttpContext.User.FindFirst("VirtualKey")?.Value;
            if (string.IsNullOrEmpty(virtualKey))
            {
                Logger.LogWarning("Failed to extract virtual key from claims");
                return OpenAIError(401, "Virtual key not found in request context", "unauthorized", "authentication_error");
            }

            // Create ephemeral key with the actual virtual key
            var response = await _ephemeralKeyService.CreateEphemeralKeyAsync(
                virtualKeyId,
                virtualKey,
                request?.Metadata);

            Logger.LogInformation("Generated ephemeral key for virtual key {VirtualKeyId}", virtualKeyId);

            return Ok(response);
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

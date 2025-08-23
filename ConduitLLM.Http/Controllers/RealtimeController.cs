using ConduitLLM.Core.Interfaces;

using Microsoft.AspNetCore.Authorization;
using ConduitLLM.Configuration.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Handles WebSocket connections for real-time audio streaming.
    /// </summary>
    /// <remarks>
    /// This controller manages WebSocket connections between clients and real-time audio providers,
    /// acting as a proxy that handles authentication, routing, usage tracking, and message translation.
    /// </remarks>
    [ApiController]
    [Route("v1/realtime")]
    [Authorize]
    public class RealtimeController : ControllerBase
    {
        private readonly ILogger<RealtimeController> _logger;
        private readonly IRealtimeProxyService _proxyService;
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly IRealtimeConnectionManager _connectionManager;

        /// <summary>
        /// Initializes a new instance of the RealtimeController class.
        /// </summary>
        public RealtimeController(
            ILogger<RealtimeController> logger,
            IRealtimeProxyService proxyService,
            IVirtualKeyService virtualKeyService,
            IRealtimeConnectionManager connectionManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _proxyService = proxyService ?? throw new ArgumentNullException(nameof(proxyService));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        }

        /// <summary>
        /// Establishes a WebSocket connection for real-time audio streaming.
        /// </summary>
        /// <param name="model">The model to use for the real-time session (e.g., "gpt-4o-realtime-preview")</param>
        /// <param name="provider">Optional provider override (defaults to routing based on model)</param>
        /// <returns>WebSocket connection or error response</returns>
        /// <response code="101">WebSocket connection established</response>
        /// <response code="400">Invalid request or WebSocket not supported</response>
        /// <response code="401">Authentication failed</response>
        /// <response code="403">Virtual key does not have access to real-time features</response>
        /// <response code="503">No available providers for the requested model</response>
        [HttpGet("connect")]
        [ProducesResponseType(StatusCodes.Status101SwitchingProtocols)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Connect(
            [FromQuery] string model,
            [FromQuery] string? provider = null)
        {
            if (!HttpContext.WebSockets.IsWebSocketRequest)
            {
                return BadRequest(new ErrorResponseDto("WebSocket connection required"));
            }

            // Extract virtual key from authorization header
            var virtualKey = ExtractVirtualKey();
            if (string.IsNullOrEmpty(virtualKey))
            {
                return Unauthorized(new ErrorResponseDto("Virtual key required"));
            }

            // Validate virtual key and check permissions
            var keyEntity = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKey, model);
            if (keyEntity == null)
            {
                return Unauthorized(new ErrorResponseDto("Invalid virtual key"));
            }

            // Check if the key has real-time permissions
            if (!HasRealtimePermissions(keyEntity))
            {
                return StatusCode(403, new ErrorResponseDto("Virtual key does not have real-time audio permissions"));
            }

            try
            {
                // Accept the WebSocket connection
                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();

                // Generate a unique connection ID
                var connectionId = Guid.NewGuid().ToString();

                _logger.LogInformation("WebSocket connection established. ConnectionId: {ConnectionId}, Model: {Model}, VirtualKeyId: {KeyId}",
                connectionId,
                model.Replace(Environment.NewLine, ""),
                keyEntity.Id);

                // Register the connection
                await _connectionManager.RegisterConnectionAsync(connectionId, keyEntity.Id, model, webSocket);

                try
                {
                    // Start the proxy session
                    await _proxyService.HandleConnectionAsync(
                        connectionId,
                        webSocket,
                        keyEntity,
                        model,
                        provider,
                        HttpContext.RequestAborted);
                }
                finally
                {
                    // Ensure connection is unregistered
                    await _connectionManager.UnregisterConnectionAsync(connectionId);
                }

                return new EmptyResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error handling WebSocket connection");
                return StatusCode(503, new { error = "Failed to establish real-time connection", details = ex.Message });
            }
        }

        /// <summary>
        /// Gets the status of active real-time connections for the authenticated user.
        /// </summary>
        /// <returns>List of active connection statuses</returns>
        [HttpGet("connections")]
        [ProducesResponseType(typeof(ConnectionStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetConnections()
        {
            var virtualKey = ExtractVirtualKey();
            if (string.IsNullOrEmpty(virtualKey))
            {
                return Unauthorized(new ErrorResponseDto("Virtual key required"));
            }

            var keyEntity = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKey);
            if (keyEntity == null)
            {
                return Unauthorized(new ErrorResponseDto("Invalid virtual key"));
            }

            var connections = await _connectionManager.GetActiveConnectionsAsync(keyEntity.Id);

            return Ok(new ConnectionStatusResponse
            {
                VirtualKeyId = keyEntity.Id,
                ActiveConnections = connections
            });
        }

        /// <summary>
        /// Terminates a specific real-time connection.
        /// </summary>
        /// <param name="connectionId">The ID of the connection to terminate</param>
        /// <returns>Success or error response</returns>
        [HttpDelete("connections/{connectionId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> TerminateConnection(string connectionId)
        {
            var virtualKey = ExtractVirtualKey();
            if (string.IsNullOrEmpty(virtualKey))
            {
                return Unauthorized(new ErrorResponseDto("Virtual key required"));
            }

            var keyEntity = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKey);
            if (keyEntity == null)
            {
                return Unauthorized(new ErrorResponseDto("Invalid virtual key"));
            }

            var terminated = await _connectionManager.TerminateConnectionAsync(connectionId, keyEntity.Id);
            if (!terminated)
            {
                return NotFound(new ErrorResponseDto("Connection not found or not owned by this key"));
            }

            return NoContent();
        }

        private string? ExtractVirtualKey()
        {
            // Try Authorization header first
            var authHeader = Request.Headers["Authorization"].ToString();
            if (!string.IsNullOrEmpty(authHeader))
            {
                if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return authHeader.Substring(7);
                }
            }

            // Try X-API-Key header
            var apiKeyHeader = Request.Headers["X-API-Key"].ToString();
            if (!string.IsNullOrEmpty(apiKeyHeader))
            {
                return apiKeyHeader;
            }

            return null;
        }

        private bool HasRealtimePermissions(ConduitLLM.Configuration.Entities.VirtualKey keyEntity)
        {
            // Check if the key has real-time permissions
            // This could be based on:
            // 1. A specific permission flag
            // 2. The models allowed for the key
            // 3. A feature flag in the key's metadata

            // For now, we'll allow all keys with audio model access
            // In production, you'd want more granular control

            if (keyEntity.AllowedModels?.Contains("realtime", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            // Check if any allowed model contains "realtime" or specific realtime models
            var realtimeModels = new[] { "gpt-4o-realtime-preview", "ultravox", "elevenlabs-conversational" };
            if (keyEntity.AllowedModels != null)
            {
                foreach (var model in keyEntity.AllowedModels.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (realtimeModels.Any(rm => model.Trim().Equals(rm, StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }
                }
            }

            return false; // Default to false - require explicit permissions
        }
    }

    /// <summary>
    /// Response model for connection status queries.
    /// </summary>
    public class ConnectionStatusResponse
    {
        /// <summary>
        /// The virtual key ID.
        /// </summary>
        public int VirtualKeyId { get; set; }

        /// <summary>
        /// List of active connections.
        /// </summary>
        public List<ConduitLLM.Core.Models.Realtime.ConnectionInfo> ActiveConnections { get; set; } = new();
    }
}

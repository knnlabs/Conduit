using System;
using System.IO;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Http.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using static ConduitLLM.Core.Extensions.LoggingSanitizer;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Controller for hybrid audio processing that chains STT, LLM, and TTS services.
    /// </summary>
    /// <remarks>
    /// This controller provides conversational AI capabilities for providers that don't have
    /// native real-time audio support, by orchestrating a pipeline of separate services.
    /// </remarks>
    [ApiController]
    [Route("v1/audio/hybrid")]
    [Authorize(AuthenticationSchemes = "VirtualKey")]
    public class HybridAudioController : ControllerBase
    {
        private readonly IHybridAudioService _hybridAudioService;
        private readonly ConduitLLM.Configuration.Services.IVirtualKeyService _virtualKeyService;
        private readonly ILogger<HybridAudioController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="HybridAudioController"/> class.
        /// </summary>
        /// <param name="hybridAudioService">The hybrid audio service.</param>
        /// <param name="virtualKeyService">The virtual key service.</param>
        /// <param name="logger">The logger instance.</param>
        public HybridAudioController(
            IHybridAudioService hybridAudioService,
            ConduitLLM.Configuration.Services.IVirtualKeyService virtualKeyService,
            ILogger<HybridAudioController> logger)
        {
            _hybridAudioService = hybridAudioService ?? throw new ArgumentNullException(nameof(hybridAudioService));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Processes audio input through the hybrid STT-LLM-TTS pipeline.
        /// </summary>
        /// <param name="file">The audio file to process.</param>
        /// <param name="sessionId">Optional session ID for maintaining conversation context.</param>
        /// <param name="language">Optional language code for transcription.</param>
        /// <param name="systemPrompt">Optional system prompt for the LLM.</param>
        /// <param name="voiceId">Optional voice ID for TTS synthesis.</param>
        /// <param name="outputFormat">Desired output audio format (default: mp3).</param>
        /// <param name="temperature">Temperature for LLM response generation (0.0-2.0).</param>
        /// <param name="maxTokens">Maximum tokens for the LLM response.</param>
        /// <returns>The synthesized audio response.</returns>
        /// <response code="200">Returns the synthesized audio data.</response>
        /// <response code="400">If the request is invalid.</response>
        /// <response code="401">If authentication fails.</response>
        /// <response code="403">If the user lacks audio permissions.</response>
        /// <response code="500">If an internal error occurs.</response>
        [HttpPost("process")]
        [Consumes("multipart/form-data")]
        [Produces("audio/mpeg", "audio/wav", "audio/flac")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ProcessAudio(
            IFormFile file,
            [FromForm] string? sessionId = null,
            [FromForm] string? language = null,
            [FromForm] string? systemPrompt = null,
            [FromForm] string? voiceId = null,
            [FromForm] string outputFormat = "mp3",
            [FromForm] double temperature = 0.7,
            [FromForm] int maxTokens = 150)
        {
            try
            {
                // Validate file
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { error = "No audio file provided" });
                }

                // Check permissions
                var apiKey = HttpContext.Items["ApiKey"]?.ToString();
                if (!string.IsNullOrEmpty(apiKey) && apiKey.StartsWith("vk-"))
                {
                    var virtualKey = await _virtualKeyService.GetVirtualKeyByKeyValueAsync(apiKey);
                    if (virtualKey == null || !virtualKey.IsEnabled)
                    {
                        return Forbid("Virtual key is not valid or enabled");
                    }
                }

                // Read audio data
                byte[] audioData;
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    audioData = memoryStream.ToArray();
                }

                // Determine audio format from content type or filename
                var audioFormat = GetAudioFormat(file.ContentType, file.FileName);

                // Create request
                var request = new HybridAudioRequest
                {
                    SessionId = sessionId,
                    AudioData = audioData,
                    AudioFormat = audioFormat,
                    Language = language,
                    SystemPrompt = systemPrompt,
                    VoiceId = voiceId,
                    OutputFormat = outputFormat,
                    Temperature = temperature,
                    MaxTokens = maxTokens,
                    EnableStreaming = false,
                    VirtualKey = apiKey
                };

                // Process audio
                var response = await _hybridAudioService.ProcessAudioAsync(request, HttpContext.RequestAborted);

                // Log usage
                _logger.LogInformation("Hybrid audio processed - Input: {InputDuration}s, Output: {OutputDuration}s, Session: {SessionId}",
                response.Metrics?.InputDurationSeconds,
                response.Metrics?.OutputDurationSeconds,
                (sessionId ?? "none").Replace(Environment.NewLine, ""));

                // Return audio file
                var contentType = GetContentType(response.AudioFormat);
                return File(response.AudioData, contentType, $"response.{response.AudioFormat}");
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex,
                "Invalid hybrid audio request");
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error processing hybrid audio");
                return StatusCode(500, new { error = "An error occurred processing the audio" });
            }
        }

        /// <summary>
        /// Creates a new conversation session for maintaining context.
        /// </summary>
        /// <param name="config">The session configuration.</param>
        /// <returns>The created session ID.</returns>
        /// <response code="200">Returns the session ID.</response>
        /// <response code="400">If the configuration is invalid.</response>
        /// <response code="401">If authentication fails.</response>
        /// <response code="403">If the user lacks audio permissions.</response>
        [HttpPost("sessions")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(CreateSessionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateSession([FromBody] HybridSessionConfig config)
        {
            try
            {
                // Check permissions
                var apiKey = HttpContext.Items["ApiKey"]?.ToString();
                if (!string.IsNullOrEmpty(apiKey) && apiKey.StartsWith("vk-"))
                {
                    var virtualKey = await _virtualKeyService.GetVirtualKeyByKeyValueAsync(apiKey);
                    if (virtualKey == null || !virtualKey.IsEnabled)
                    {
                        return Forbid("Virtual key is not valid or enabled");
                    }
                }

                // Create session
                var sessionId = await _hybridAudioService.CreateSessionAsync(config, HttpContext.RequestAborted);

                return Ok(new CreateSessionResponse { SessionId = sessionId });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error creating hybrid audio session");
                return StatusCode(500, new { error = "An error occurred creating the session" });
            }
        }

        /// <summary>
        /// Closes an active conversation session.
        /// </summary>
        /// <param name="sessionId">The session ID to close.</param>
        /// <returns>No content.</returns>
        /// <response code="204">Session closed successfully.</response>
        /// <response code="400">If the session ID is invalid.</response>
        /// <response code="401">If authentication fails.</response>
        [HttpDelete("sessions/{sessionId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CloseSession(string sessionId)
        {
            try
            {
                await _hybridAudioService.CloseSessionAsync(sessionId, HttpContext.RequestAborted);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error closing hybrid audio session");
                return StatusCode(500, new { error = "An error occurred closing the session" });
            }
        }

        /// <summary>
        /// Checks if the hybrid audio service is available.
        /// </summary>
        /// <returns>Service availability status.</returns>
        /// <response code="200">Returns the availability status.</response>
        /// <response code="401">If authentication fails.</response>
        [HttpGet("status")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(ServiceStatus), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                var isAvailable = await _hybridAudioService.IsAvailableAsync(HttpContext.RequestAborted);
                var metrics = await _hybridAudioService.GetLatencyMetricsAsync(HttpContext.RequestAborted);

                return Ok(new ServiceStatus
                {
                    Available = isAvailable,
                    LatencyMetrics = metrics
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error checking hybrid audio status");
                return Ok(new ServiceStatus { Available = false });
            }
        }

        private string GetAudioFormat(string contentType, string fileName)
        {
            // Try to determine from content type
            return contentType?.ToLower() switch
            {
                "audio/mpeg" => "mp3",
                "audio/mp3" => "mp3",
                "audio/wav" => "wav",
                "audio/wave" => "wav",
                "audio/webm" => "webm",
                "audio/flac" => "flac",
                "audio/ogg" => "ogg",
                _ => string.IsNullOrEmpty(Path.GetExtension(fileName)?.TrimStart('.').ToLower()) 
                    ? "mp3" 
                    : Path.GetExtension(fileName).TrimStart('.').ToLower()
            };
        }

        private string GetContentType(string format)
        {
            return format?.ToLower() switch
            {
                "mp3" => "audio/mpeg",
                "wav" => "audio/wav",
                "webm" => "audio/webm",
                "flac" => "audio/flac",
                "ogg" => "audio/ogg",
                _ => "audio/mpeg"
            };
        }

        /// <summary>
        /// Response for session creation.
        /// </summary>
        public class CreateSessionResponse
        {
            /// <summary>
            /// Gets or sets the created session ID.
            /// </summary>
            public string SessionId { get; set; } = string.Empty;
        }

        /// <summary>
        /// Service status response.
        /// </summary>
        public class ServiceStatus
        {
            /// <summary>
            /// Gets or sets whether the service is available.
            /// </summary>
            public bool Available { get; set; }

            /// <summary>
            /// Gets or sets the latency metrics.
            /// </summary>
            public HybridLatencyMetrics? LatencyMetrics { get; set; }
        }
    }
}

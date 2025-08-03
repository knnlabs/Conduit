using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
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
    /// Handles audio-related operations including transcription and text-to-speech.
    /// </summary>
    [ApiController]
    [Route("v1/audio")]
    [Authorize(Policy = "RequireVirtualKey")]
    [Tags("Audio")]
    public class AudioController : ControllerBase
    {
        private readonly IAudioRouter _audioRouter;
        private readonly ConduitLLM.Configuration.Services.IVirtualKeyService _virtualKeyService;
        private readonly ILogger<AudioController> _logger;
        private readonly ConduitLLM.Core.Interfaces.Configuration.IModelProviderMappingService _modelMappingService;

        public AudioController(
            IAudioRouter audioRouter,
            ConduitLLM.Configuration.Services.IVirtualKeyService virtualKeyService,
            ILogger<AudioController> logger,
            ConduitLLM.Core.Interfaces.Configuration.IModelProviderMappingService modelMappingService)
        {
            _audioRouter = audioRouter ?? throw new ArgumentNullException(nameof(audioRouter));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _modelMappingService = modelMappingService ?? throw new ArgumentNullException(nameof(modelMappingService));
        }

        /// <summary>
        /// Transcribes audio into text.
        /// </summary>
        /// <param name="file">The audio file to transcribe.</param>
        /// <param name="model">The model to use for transcription (e.g., "whisper-1").</param>
        /// <param name="language">The language of the input audio (ISO-639-1).</param>
        /// <param name="prompt">Optional text to guide the model's style.</param>
        /// <param name="response_format">The format of the transcript output.</param>
        /// <param name="temperature">Sampling temperature between 0 and 1.</param>
        /// <param name="timestamp_granularities">The timestamp granularities to populate.</param>
        /// <returns>The transcription result.</returns>
        [HttpPost("transcriptions")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(AudioTranscriptionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> TranscribeAudio(
            [Required] IFormFile file,
            [FromForm] string model = "whisper-1",
            [FromForm] string? language = null,
            [FromForm] string? prompt = null,
            [FromForm] string? response_format = null,
            [FromForm, Range(0, 1)] double? temperature = null,
            [FromForm] string[]? timestamp_granularities = null)
        {
            // Get virtual key from context
            var virtualKey = HttpContext.User.FindFirst("VirtualKey")?.Value;
            if (string.IsNullOrEmpty(virtualKey))
            {
                return Unauthorized(new ProblemDetails
                {
                    Title = "Unauthorized",
                    Detail = "Invalid or missing API key"
                });
            }

            // Validate file
            if (file.Length == 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = "Audio file is empty"
                });
            }

            // Check file size (25MB limit for OpenAI)
            const long maxFileSize = 25 * 1024 * 1024;
            if (file.Length > maxFileSize)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = $"Audio file exceeds maximum size of {maxFileSize / (1024 * 1024)}MB"
                });
            }

            try
            {
                // Get provider info for usage tracking
                try
                {
                    var modelMapping = await _modelMappingService.GetMappingByModelAliasAsync(model);
                    if (modelMapping != null)
                    {
                        HttpContext.Items["ProviderId"] = modelMapping.ProviderId;
                        HttpContext.Items["ProviderType"] = modelMapping.Provider?.ProviderType;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get provider info for model {Model}", model);
                }

                // Read file into memory
                byte[] audioData;
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    audioData = memoryStream.ToArray();
                }

                // Parse response format
                TranscriptionFormat? format = null;
                if (!string.IsNullOrEmpty(response_format))
                {
                    if (Enum.TryParse<TranscriptionFormat>(response_format, true, out var parsedFormat))
                    {
                        format = parsedFormat;
                    }
                    else
                    {
                        return BadRequest(new ProblemDetails
                        {
                            Title = "Invalid Request",
                            Detail = $"Invalid response_format: {response_format}"
                        });
                    }
                }

                // Create transcription request
                var request = new AudioTranscriptionRequest
                {
                    AudioData = audioData,
                    FileName = file.FileName,
                    Model = model,
                    Language = language,
                    Prompt = prompt,
                    ResponseFormat = format,
                    Temperature = temperature,
                    TimestampGranularity = timestamp_granularities?.Contains("word") == true ? TimestampGranularity.Word :
                                          timestamp_granularities?.Contains("segment") == true ? TimestampGranularity.Segment :
                                          TimestampGranularity.None
                };

                // Route to appropriate provider
                var client = await _audioRouter.GetTranscriptionClientAsync(request, virtualKey);
                if (client == null)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "No Provider Available",
                        Detail = "No audio transcription provider is available for this request"
                    });
                }

                // Perform transcription
                var response = await client.TranscribeAudioAsync(request);

                // Update spend based on estimated cost
                // Estimate cost based on audio duration (rough estimate: 1MB = 1 minute = $0.006)
                var estimatedMinutes = audioData.Length / (1024.0 * 1024.0);
                var estimatedCost = (decimal)(estimatedMinutes * 0.006);

                // Get virtual key entity to update spend
                var virtualKeyEntity = await _virtualKeyService.GetVirtualKeyByKeyValueAsync(virtualKey);
                if (virtualKeyEntity != null)
                {
                    await _virtualKeyService.UpdateSpendAsync(virtualKeyEntity.Id, estimatedCost);
                }

                // Return response based on format
                if (format == TranscriptionFormat.Vtt ||
                    format == TranscriptionFormat.Srt)
                {
                    return Content(response.Text, "text/plain");
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transcribing audio");
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Internal Server Error",
                    Detail = "An error occurred while transcribing the audio"
                });
            }
        }

        /// <summary>
        /// Generates audio from input text.
        /// </summary>
        /// <param name="request">The text-to-speech request.</param>
        /// <returns>The generated audio file.</returns>
        [HttpPost("speech")]
        [Consumes("application/json")]
        [Produces("audio/mpeg", "audio/opus", "audio/aac", "audio/flac", "audio/wav", "audio/pcm")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> GenerateSpeech([FromBody, Required] TextToSpeechRequestDto request)
        {
            // Get virtual key from context
            var virtualKey = HttpContext.User.FindFirst("VirtualKey")?.Value;
            if (string.IsNullOrEmpty(virtualKey))
            {
                return Unauthorized(new ProblemDetails
                {
                    Title = "Unauthorized",
                    Detail = "Invalid or missing API key"
                });
            }

            // Validate request
            if (string.IsNullOrWhiteSpace(request.Input))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = "Input text is required"
                });
            }

            // Validate input length (4096 chars limit for OpenAI)
            const int maxInputLength = 4096;
            if (request.Input.Length > maxInputLength)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = $"Input text exceeds maximum length of {maxInputLength} characters"
                });
            }

            try
            {
                // Get provider info for usage tracking
                try
                {
                    var modelMapping = await _modelMappingService.GetMappingByModelAliasAsync(request.Model);
                    if (modelMapping != null)
                    {
                        HttpContext.Items["ProviderId"] = modelMapping.ProviderId;
                        HttpContext.Items["ProviderType"] = modelMapping.Provider?.ProviderType;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get provider info for model {Model}", request.Model);
                }

                // Parse response format
                AudioFormat format = AudioFormat.Mp3;
                if (!string.IsNullOrEmpty(request.ResponseFormat))
                {
                    if (!Enum.TryParse<AudioFormat>(request.ResponseFormat, true, out format))
                    {
                        return BadRequest(new ProblemDetails
                        {
                            Title = "Invalid Request",
                            Detail = $"Invalid response_format: {request.ResponseFormat}"
                        });
                    }
                }

                // Create TTS request
                var ttsRequest = new TextToSpeechRequest
                {
                    Input = request.Input,
                    Model = request.Model,
                    Voice = request.Voice,
                    ResponseFormat = format,
                    Speed = request.Speed
                };

                // Route to appropriate provider
                var client = await _audioRouter.GetTextToSpeechClientAsync(ttsRequest, virtualKey);
                if (client == null)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "No Provider Available",
                        Detail = "No text-to-speech provider is available for this request"
                    });
                }

                // Check if streaming is requested
                if (HttpContext.Request.Headers.Accept.Contains("text/event-stream"))
                {
                    // Stream the audio
                    Response.ContentType = GetContentType(format);
                    Response.Headers["Cache-Control"] = "no-cache";
                    Response.Headers["X-Accel-Buffering"] = "no";

                    await foreach (var chunk in client.StreamSpeechAsync(ttsRequest, virtualKey))
                    {
                        if (chunk.Data != null && chunk.Data.Length > 0)
                        {
                            await Response.Body.WriteAsync(chunk.Data);
                            await Response.Body.FlushAsync();
                        }
                    }

                    return new EmptyResult();
                }
                else
                {
                    // Generate complete audio
                    var response = await client.CreateSpeechAsync(ttsRequest, virtualKey);

                    // Update spend based on estimated cost
                    // Estimate cost based on character count (rough estimate: $0.015 per 1K chars for tts-1)
                    var characterCount = request.Input.Length;
                    var estimatedCost = (decimal)(characterCount / 1000.0 * 0.015);

                    // Get virtual key entity to update spend
                    var virtualKeyEntity = await _virtualKeyService.GetVirtualKeyByKeyValueAsync(virtualKey);
                    if (virtualKeyEntity != null)
                    {
                        await _virtualKeyService.UpdateSpendAsync(virtualKeyEntity.Id, estimatedCost);
                    }

                    // Return audio file
                    return File(response.AudioData, GetContentType(format));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating speech");
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Internal Server Error",
                    Detail = "An error occurred while generating speech"
                });
            }
        }

        /// <summary>
        /// Translates audio into English text.
        /// </summary>
        /// <param name="file">The audio file to translate.</param>
        /// <param name="model">The model to use for translation.</param>
        /// <param name="prompt">Optional text to guide the model's style.</param>
        /// <param name="response_format">The format of the translation output.</param>
        /// <param name="temperature">Sampling temperature between 0 and 1.</param>
        /// <returns>The translation result.</returns>
        [HttpPost("translations")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(AudioTranscriptionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> TranslateAudio(
            [Required] IFormFile file,
            [FromForm] string model = "whisper-1",
            [FromForm] string? prompt = null,
            [FromForm] string? response_format = null,
            [FromForm, Range(0, 1)] double? temperature = null)
        {
            // Translation is just transcription with target language set to English
            return await TranscribeAudio(
                file,
                model,
                "en", // Force English output
                prompt,
                response_format,
                temperature,
                null);
        }

        private string GetContentType(AudioFormat format)
        {
            return format switch
            {
                AudioFormat.Mp3 => "audio/mpeg",
                AudioFormat.Opus => "audio/opus",
                AudioFormat.Aac => "audio/aac",
                AudioFormat.Flac => "audio/flac",
                AudioFormat.Wav => "audio/wav",
                AudioFormat.Pcm => "audio/pcm",
                _ => "audio/mpeg"
            };
        }

        private long EstimateTranscriptionTokens(string text)
        {
            // Rough estimate: 1 token per 4 characters
            return text.Length / 4;
        }

        private long EstimateTTSTokens(string text)
        {
            // Rough estimate: 1 token per 4 characters
            return text.Length / 4;
        }

        /// <summary>
        /// DTO for text-to-speech requests.
        /// </summary>
        public class TextToSpeechRequestDto
        {
            [Required]
            [JsonPropertyName("model")]
            public string Model { get; set; } = "tts-1";

            [Required]
            [JsonPropertyName("input")]
            public string Input { get; set; } = string.Empty;

            [Required]
            [JsonPropertyName("voice")]
            public string Voice { get; set; } = "alloy";

            [JsonPropertyName("response_format")]
            public string? ResponseFormat { get; set; }

            [JsonPropertyName("speed")]
            [Range(0.25, 4.0)]
            public double? Speed { get; set; }
        }
    }
}

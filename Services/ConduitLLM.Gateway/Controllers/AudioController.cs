using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Controllers;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Gateway.Authorization;
using ConduitLLM.Gateway.Constants;
using ConduitLLM.Gateway.UsageTracking;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Gateway.Controllers
{
    /// <summary>
    /// Handles audio transcription (speech-to-text) and speech synthesis (text-to-speech)
    /// following OpenAI's API format.
    /// </summary>
    [ApiController]
    [Route("v1/audio")]
    [Authorize(AuthenticationSchemes = "VirtualKey")]
    [RequireBalance]
    [Tags("Audio")]
    public class AudioController : GatewayControllerBase
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly IModelProviderMappingService _modelMappingService;
        private readonly ILogger<AudioController> _logger;

        public AudioController(
            ILLMClientFactory clientFactory,
            IModelProviderMappingService modelMappingService,
            ILogger<AudioController> logger,
            IEventBus eventBus)
            : base(eventBus, logger)
        {
            _clientFactory = clientFactory;
            _modelMappingService = modelMappingService;
            _logger = logger;
        }

        /// <summary>
        /// Transcribes uploaded audio to text (OpenAI <c>/audio/transcriptions</c> compatible).
        /// </summary>
        [HttpPost("transcriptions")]
        [RequestSizeLimit(26_214_400)] // 25 MB
        public async Task<IActionResult> CreateTranscription(
            [FromForm] IFormFile file,
            [FromForm] string model,
            [FromForm] string? language = null,
            [FromForm] string? prompt = null,
            [FromForm(Name = "response_format")] string? responseFormat = null,
            [FromForm] double? temperature = null,
            CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0)
                return OpenAIError(400, "An audio file is required.", "invalid_request_error", "invalid_request");
            if (string.IsNullOrEmpty(model))
                return OpenAIError(400, "A model is required.", "invalid_request_error", "invalid_request");

            var mapping = await _modelMappingService.GetMappingByModelAliasAsync(model);
            var supported = mapping?.ModelProviderTypeAssociation?.Model?.SupportsSpeechToText ?? false;
            if (mapping == null || !supported)
                return OpenAIError(400, $"Model {model} does not support speech-to-text transcription.", "invalid_request_error", "unsupported_model");

            StampBillingItems(mapping);

            var client = await _clientFactory.GetClientAsync(model, cancellationToken);
            var stt = client.FindInChain<IAudioTranscriptionClient>();
            if (stt == null)
                return OpenAIError(400, $"Model {model} does not support speech-to-text transcription.", "invalid_request_error", "unsupported_model");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);

            var request = new AudioTranscriptionRequest
            {
                Model = mapping.ProviderModelId,
                AudioData = ms.ToArray(),
                FileName = file.FileName,
                ContentType = file.ContentType,
                Language = language,
                Prompt = prompt,
                Temperature = temperature,
                ResponseFormat = responseFormat
            };

            var result = await stt.TranscribeAudioAsync(request, cancellationToken: cancellationToken);
            result.Model = model; // echo the caller's alias, not the provider model id

            // Hand the billable audio duration to the middleware, which never parses the response body.
            HttpContext.SetUsageContext(new AudioUsageContext
            {
                Model = model,
                AudioDurationSeconds = result.DurationSeconds
            });
            var transcriptionUsage = new ConduitLLM.Core.Models.Usage
            {
                AudioDurationSeconds = result.DurationSeconds
            };
            var transcriptionAccounting = HttpContext.GetOrCreateRequestAccountingContext();
            transcriptionAccounting.SetOperation(RequestOperation.Audio, CurrentVirtualKeyId, model);
            transcriptionAccounting.RecordProviderUsage(
                transcriptionUsage,
                model,
                UsageEvidenceSource.Provider);

            if (string.Equals(responseFormat, "text", StringComparison.OrdinalIgnoreCase))
                return Content(result.Text, "text/plain");
            return Ok(result);
        }

        /// <summary>
        /// Synthesizes speech from text (OpenAI <c>/audio/speech</c> compatible). Returns raw audio bytes.
        /// </summary>
        [HttpPost("speech")]
        public async Task<IActionResult> CreateSpeech(
            [FromBody] TextToSpeechRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null || string.IsNullOrEmpty(request.Model))
                return OpenAIError(400, "A model is required.", "invalid_request_error", "invalid_request");
            if (string.IsNullOrEmpty(request.Input))
                return OpenAIError(400, "Input text is required.", "invalid_request_error", "invalid_request");
            if (string.IsNullOrEmpty(request.Voice))
                return OpenAIError(400, "A voice is required.", "invalid_request_error", "invalid_request");

            var alias = request.Model;
            var mapping = await _modelMappingService.GetMappingByModelAliasAsync(alias);
            var supported = mapping?.ModelProviderTypeAssociation?.Model?.SupportsTextToSpeech ?? false;
            if (mapping == null || !supported)
                return OpenAIError(400, $"Model {alias} does not support text-to-speech.", "invalid_request_error", "unsupported_model");

            StampBillingItems(mapping);

            var client = await _clientFactory.GetClientAsync(alias, cancellationToken);
            var tts = client.FindInChain<ITextToSpeechClient>();
            if (tts == null)
                return OpenAIError(400, $"Model {alias} does not support text-to-speech.", "invalid_request_error", "unsupported_model");

            var characterCount = request.Input.Length;
            request.Model = mapping.ProviderModelId; // swap alias -> provider model id before dispatch

            var result = await tts.CreateSpeechAsync(request, cancellationToken: cancellationToken);

            // Hand the billable character count to the middleware, which never parses the binary body.
            HttpContext.SetUsageContext(new AudioUsageContext
            {
                Model = alias,
                TtsCharacters = characterCount
            });
            var speechUsage = new ConduitLLM.Core.Models.Usage
            {
                TtsCharacters = characterCount
            };
            var speechAccounting = HttpContext.GetOrCreateRequestAccountingContext();
            speechAccounting.SetOperation(RequestOperation.Audio, CurrentVirtualKeyId, alias);
            speechAccounting.RecordProviderUsage(
                speechUsage,
                alias,
                UsageEvidenceSource.Estimated);

            return File(result.AudioData, result.ContentType);
        }

        private void StampBillingItems(ConduitLLM.Configuration.Entities.ModelProviderMapping mapping)
        {
            HttpContext.Items["ProviderId"] = mapping.ProviderId;
            HttpContext.Items["ProviderType"] = mapping.Provider?.ProviderType;
            if (mapping.ModelProviderTypeAssociation?.ModelCostId != null)
                HttpContext.Items[HttpContextKeys.ModelCostId] = mapping.ModelProviderTypeAssociation.ModelCostId;
        }
    }
}

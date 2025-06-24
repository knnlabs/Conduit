using ConduitLLM.CoreClient.Client;
using ConduitLLM.CoreClient.Models;
using ConduitLLM.CoreClient.Utils;
using ConduitLLM.CoreClient.Exceptions;
using Microsoft.Extensions.Logging;
using System.Text;

namespace ConduitLLM.CoreClient.Services;

/// <summary>
/// Service for audio operations including speech-to-text, text-to-speech, and audio translation.
/// </summary>
public class AudioService
{
    private readonly BaseClient _client;
    private readonly ILogger<AudioService>? _logger;
    
    private const string TranscriptionsEndpoint = "/v1/audio/transcriptions";
    private const string TranslationsEndpoint = "/v1/audio/translations";
    private const string SpeechEndpoint = "/v1/audio/speech";
    private const string HybridProcessEndpoint = "/v1/audio/hybrid/process";

    // Supported audio file extensions and their MIME types
    private static readonly Dictionary<string, string> AudioMimeTypes = new()
    {
        { ".mp3", "audio/mpeg" },
        { ".wav", "audio/wav" },
        { ".flac", "audio/flac" },
        { ".ogg", "audio/ogg" },
        { ".aac", "audio/aac" },
        { ".opus", "audio/opus" },
        { ".m4a", "audio/mp4" },
        { ".webm", "audio/webm" }
    };

    /// <summary>
    /// Initializes a new instance of the AudioService class.
    /// </summary>
    /// <param name="client">The base client to use for HTTP requests.</param>
    /// <param name="logger">Optional logger instance.</param>
    public AudioService(BaseClient client, ILogger<AudioService>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger;
    }

    /// <summary>
    /// Transcribes audio to text using speech-to-text models.
    /// </summary>
    /// <param name="request">The transcription request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transcription response.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
    public async Task<AudioTranscriptionResponse> TranscribeAsync(
        AudioTranscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateTranscriptionRequest(request);
            
            _logger?.LogDebug("Transcribing audio with model {Model}, language: {Language}", 
                request.Model, request.Language ?? "auto-detect");

            using var formData = CreateAudioFormData(request.File);
            AddTranscriptionFields(formData, request);

            var httpResponse = await _client.HttpClientForServices.PostAsync(TranscriptionsEndpoint, formData, cancellationToken);
            await ErrorHandler.HandleErrorResponseAsync(httpResponse);

            var jsonResponse = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            var response = System.Text.Json.JsonSerializer.Deserialize<AudioTranscriptionResponse>(
                jsonResponse, _client.JsonSerializerOptionsForServices) ?? new AudioTranscriptionResponse();

            _logger?.LogDebug("Transcription completed successfully, text length: {Length} characters", 
                response.Text?.Length ?? 0);
            
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitCoreException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Translates audio to English text using speech-to-text models.
    /// </summary>
    /// <param name="request">The translation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The translation response.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
    public async Task<AudioTranslationResponse> TranslateAsync(
        AudioTranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateTranslationRequest(request);
            
            _logger?.LogDebug("Translating audio to English with model {Model}", request.Model);

            using var formData = CreateAudioFormData(request.File);
            AddTranslationFields(formData, request);

            var httpResponse = await _client.HttpClientForServices.PostAsync(TranslationsEndpoint, formData, cancellationToken);
            await ErrorHandler.HandleErrorResponseAsync(httpResponse);

            var jsonResponse = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            var response = System.Text.Json.JsonSerializer.Deserialize<AudioTranslationResponse>(
                jsonResponse, _client.JsonSerializerOptionsForServices) ?? new AudioTranslationResponse();

            _logger?.LogDebug("Translation completed successfully, text length: {Length} characters", 
                response.Text?.Length ?? 0);
            
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitCoreException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Generates speech from text using text-to-speech models.
    /// </summary>
    /// <param name="request">The speech generation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The speech response with audio data.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
    public async Task<TextToSpeechResponse> GenerateSpeechAsync(
        TextToSpeechRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateSpeechRequest(request);
            
            _logger?.LogDebug("Generating speech with model {Model}, voice {Voice}, text length: {Length}", 
                request.Model, request.Voice, request.Input?.Length ?? 0);

            // Convert enums to API format
            var apiRequest = new
            {
                model = ConvertTextToSpeechModelToString(request.Model),
                input = request.Input,
                voice = ConvertVoiceToString(request.Voice),
                response_format = request.ResponseFormat?.ToString().ToLowerInvariant(),
                speed = request.Speed,
                voice_settings = request.VoiceSettings
            };

            var audioData = await _client.PostForServiceAsync<byte[]>(
                SpeechEndpoint,
                apiRequest,
                cancellationToken);

            var response = new TextToSpeechResponse
            {
                Audio = audioData,
                Format = request.ResponseFormat ?? AudioFormat.Mp3,
                Metadata = new AudioMetadata
                {
                    Size = audioData.Length
                }
            };

            _logger?.LogDebug("Speech generation completed successfully, audio size: {Size} bytes", 
                audioData.Length);
            
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitCoreException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Processes audio through the hybrid pipeline (STT + LLM + TTS).
    /// </summary>
    /// <param name="request">The hybrid audio processing request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The hybrid audio response.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
    public async Task<HybridAudioResponse> ProcessHybridAsync(
        HybridAudioRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateHybridRequest(request);
            
            _logger?.LogDebug("Processing hybrid audio with transcription model {TranscriptionModel}, chat model {ChatModel}, speech model {SpeechModel}", 
                request.Models.Transcription, request.Models.Chat, request.Models.Speech);

            using var formData = CreateAudioFormData(request.File);
            AddHybridFields(formData, request);

            var httpResponse = await _client.HttpClientForServices.PostAsync(HybridProcessEndpoint, formData, cancellationToken);
            await ErrorHandler.HandleErrorResponseAsync(httpResponse);

            var jsonResponse = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            var response = System.Text.Json.JsonSerializer.Deserialize<HybridAudioResponse>(
                jsonResponse, _client.JsonSerializerOptionsForServices) ?? new HybridAudioResponse();

            _logger?.LogDebug("Hybrid processing completed successfully, output audio size: {Size} bytes", 
                response.Audio?.Length ?? 0);
            
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitCoreException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Creates a simple transcription request for quick speech-to-text conversion.
    /// </summary>
    /// <param name="audioFile">The audio file to transcribe.</param>
    /// <param name="model">Optional model to use (defaults to Whisper1).</param>
    /// <param name="language">Optional language code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transcribed text.</returns>
    public async Task<string> QuickTranscribeAsync(
        AudioFile audioFile,
        TranscriptionModel model = TranscriptionModel.Whisper1,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        var request = new AudioTranscriptionRequest
        {
            File = audioFile,
            Model = model,
            Language = language,
            ResponseFormat = TranscriptionFormat.Text
        };

        var response = await TranscribeAsync(request, cancellationToken);
        return response.Text;
    }

    /// <summary>
    /// Creates a simple speech generation request for quick text-to-speech conversion.
    /// </summary>
    /// <param name="text">The text to convert to speech.</param>
    /// <param name="voice">Optional voice to use (defaults to Alloy).</param>
    /// <param name="model">Optional model to use (defaults to Tts1).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated audio data.</returns>
    public async Task<byte[]> QuickSpeakAsync(
        string text,
        Voice voice = Voice.Alloy,
        TextToSpeechModel model = TextToSpeechModel.Tts1,
        CancellationToken cancellationToken = default)
    {
        var request = new TextToSpeechRequest
        {
            Model = model,
            Input = text,
            Voice = voice,
            ResponseFormat = AudioFormat.Mp3
        };

        var response = await GenerateSpeechAsync(request, cancellationToken);
        return response.Audio;
    }

    #region Validation Methods

    private static void ValidateTranscriptionRequest(AudioTranscriptionRequest request)
    {
        if (request?.File == null)
            throw new ConduitLLM.CoreClient.Exceptions.ValidationException("Audio file is required for transcription");

        if (request.Temperature.HasValue && (request.Temperature < 0 || request.Temperature > 1))
            throw new ConduitLLM.CoreClient.Exceptions.ValidationException("Temperature must be between 0 and 1");

        ValidateAudioFile(request.File);
    }

    private static void ValidateTranslationRequest(AudioTranslationRequest request)
    {
        if (request?.File == null)
            throw new ConduitLLM.CoreClient.Exceptions.ValidationException("Audio file is required for translation");

        if (request.Temperature.HasValue && (request.Temperature < 0 || request.Temperature > 1))
            throw new ConduitLLM.CoreClient.Exceptions.ValidationException("Temperature must be between 0 and 1");

        ValidateAudioFile(request.File);
    }

    private static void ValidateSpeechRequest(TextToSpeechRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Input))
            throw new ConduitLLM.CoreClient.Exceptions.ValidationException("Input text is required for speech generation");

        if (request.Input.Length > 4096)
            throw new ConduitLLM.CoreClient.Exceptions.ValidationException("Input text must be 4096 characters or less");

        if (request.Speed.HasValue && (request.Speed < 0.25 || request.Speed > 4.0))
            throw new ConduitLLM.CoreClient.Exceptions.ValidationException("Speed must be between 0.25 and 4.0");
    }

    private static void ValidateHybridRequest(HybridAudioRequest request)
    {
        if (request?.File == null)
            throw new ConduitLLM.CoreClient.Exceptions.ValidationException("Audio file is required for hybrid processing");

        if (request.Models == null)
            throw new ConduitLLM.CoreClient.Exceptions.ValidationException("Models configuration is required for hybrid processing");

        if (string.IsNullOrWhiteSpace(request.Models.Chat))
            throw new ConduitLLM.CoreClient.Exceptions.ValidationException("Chat model is required for hybrid processing");

        ValidateAudioFile(request.File);
    }

    private static void ValidateAudioFile(AudioFile file)
    {
        if (file?.Data == null || file.Data.Length == 0)
            throw new ConduitLLM.CoreClient.Exceptions.ValidationException("Audio file data is required");

        if (string.IsNullOrWhiteSpace(file.Filename))
            throw new ConduitLLM.CoreClient.Exceptions.ValidationException("Audio filename is required");

        // Validate file extension
        var extension = Path.GetExtension(file.Filename).ToLowerInvariant();
        if (!AudioMimeTypes.ContainsKey(extension))
        {
            var supportedFormats = string.Join(", ", AudioMimeTypes.Keys);
            throw new ConduitLLM.CoreClient.Exceptions.ValidationException($"Unsupported audio format '{extension}'. Supported formats: {supportedFormats}");
        }

        // Validate file size (25MB limit)
        const long maxSize = 25 * 1024 * 1024; // 25MB
        if (file.Data.Length > maxSize)
            throw new ConduitLLM.CoreClient.Exceptions.ValidationException($"Audio file too large. Maximum size is {maxSize / (1024 * 1024)}MB");
    }

    #endregion

    #region Helper Methods

    private static MultipartFormDataContent CreateAudioFormData(AudioFile audioFile)
    {
        var formData = new MultipartFormDataContent();
        
        var fileContent = new ByteArrayContent(audioFile.Data);
        
        // Set appropriate content type
        var extension = Path.GetExtension(audioFile.Filename).ToLowerInvariant();
        var contentType = AudioMimeTypes.TryGetValue(extension, out var mimeType) ? mimeType : "audio/mpeg";
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        
        formData.Add(fileContent, "file", audioFile.Filename);
        
        return formData;
    }

    private static void AddTranscriptionFields(MultipartFormDataContent formData, AudioTranscriptionRequest request)
    {
        formData.Add(new StringContent(ConvertTranscriptionModelToString(request.Model)), "model");
        
        if (!string.IsNullOrWhiteSpace(request.Language))
            formData.Add(new StringContent(request.Language), "language");
        
        if (!string.IsNullOrWhiteSpace(request.Prompt))
            formData.Add(new StringContent(request.Prompt), "prompt");
        
        if (request.ResponseFormat.HasValue)
            formData.Add(new StringContent(ConvertTranscriptionFormatToString(request.ResponseFormat.Value)), "response_format");
        
        if (request.Temperature.HasValue)
            formData.Add(new StringContent(request.Temperature.Value.ToString("F2")), "temperature");
        
        if (request.TimestampGranularities?.Length > 0)
        {
            var granularities = string.Join(",", request.TimestampGranularities.Select(ConvertTimestampGranularityToString));
            formData.Add(new StringContent(granularities), "timestamp_granularities");
        }
    }

    private static void AddTranslationFields(MultipartFormDataContent formData, AudioTranslationRequest request)
    {
        formData.Add(new StringContent(ConvertTranscriptionModelToString(request.Model)), "model");
        
        if (!string.IsNullOrWhiteSpace(request.Prompt))
            formData.Add(new StringContent(request.Prompt), "prompt");
        
        if (request.ResponseFormat.HasValue)
            formData.Add(new StringContent(ConvertTranscriptionFormatToString(request.ResponseFormat.Value)), "response_format");
        
        if (request.Temperature.HasValue)
            formData.Add(new StringContent(request.Temperature.Value.ToString("F2")), "temperature");
    }

    private static void AddHybridFields(MultipartFormDataContent formData, HybridAudioRequest request)
    {
        // Models configuration
        formData.Add(new StringContent(ConvertTranscriptionModelToString(request.Models.Transcription)), "models[transcription]");
        formData.Add(new StringContent(request.Models.Chat), "models[chat]");
        formData.Add(new StringContent(ConvertTextToSpeechModelToString(request.Models.Speech)), "models[speech]");
        
        // Voice
        formData.Add(new StringContent(ConvertVoiceToString(request.Voice)), "voice");
        
        // Optional fields
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            formData.Add(new StringContent(request.SystemPrompt), "system_prompt");
        
        if (!string.IsNullOrWhiteSpace(request.Context))
            formData.Add(new StringContent(request.Context), "context");
        
        if (!string.IsNullOrWhiteSpace(request.Language))
            formData.Add(new StringContent(request.Language), "language");
        
        if (request.Temperature?.Transcription.HasValue == true)
            formData.Add(new StringContent(request.Temperature.Transcription.Value.ToString("F2")), "temperature[transcription]");
        
        if (request.Temperature?.Chat.HasValue == true)
            formData.Add(new StringContent(request.Temperature.Chat.Value.ToString("F2")), "temperature[chat]");
        
        if (!string.IsNullOrWhiteSpace(request.SessionId))
            formData.Add(new StringContent(request.SessionId), "session_id");
    }

    #endregion

    #region Enum Conversion Methods

    private static string ConvertTranscriptionModelToString(TranscriptionModel model) => model switch
    {
        TranscriptionModel.Whisper1 => "whisper-1",
        TranscriptionModel.WhisperLarge => "whisper-large",
        TranscriptionModel.DeepgramNova => "deepgram-nova",
        TranscriptionModel.AzureStt => "azure-stt",
        TranscriptionModel.OpenaiWhisper => "openai-whisper",
        _ => "whisper-1"
    };

    private static string ConvertTextToSpeechModelToString(TextToSpeechModel model) => model switch
    {
        TextToSpeechModel.Tts1 => "tts-1",
        TextToSpeechModel.Tts1Hd => "tts-1-hd",
        TextToSpeechModel.ElevenlabsTts => "elevenlabs-tts",
        TextToSpeechModel.AzureTts => "azure-tts",
        TextToSpeechModel.OpenaiTts => "openai-tts",
        _ => "tts-1"
    };

    private static string ConvertVoiceToString(Voice voice) => voice switch
    {
        Voice.Alloy => "alloy",
        Voice.Echo => "echo",
        Voice.Fable => "fable",
        Voice.Onyx => "onyx",
        Voice.Nova => "nova",
        Voice.Shimmer => "shimmer",
        Voice.Rachel => "rachel",
        Voice.Adam => "adam",
        Voice.Antoni => "antoni",
        Voice.Arnold => "arnold",
        Voice.Josh => "josh",
        Voice.Sam => "sam",
        _ => "alloy"
    };

    private static string ConvertTranscriptionFormatToString(TranscriptionFormat format) => format switch
    {
        TranscriptionFormat.Json => "json",
        TranscriptionFormat.Text => "text",
        TranscriptionFormat.Srt => "srt",
        TranscriptionFormat.Vtt => "vtt",
        TranscriptionFormat.VerboseJson => "verbose_json",
        _ => "json"
    };

    private static string ConvertTimestampGranularityToString(TimestampGranularity granularity) => granularity switch
    {
        TimestampGranularity.Segment => "segment",
        TimestampGranularity.Word => "word",
        _ => "segment"
    };

    #endregion
}

/// <summary>
/// Utility methods for audio file handling.
/// </summary>
public static class AudioUtils
{
    /// <summary>
    /// Creates an AudioFile from a byte array with specified filename.
    /// </summary>
    /// <param name="data">The audio file data.</param>
    /// <param name="filename">The filename including extension.</param>
    /// <param name="contentType">Optional content type (will be inferred from filename if not provided).</param>
    /// <returns>An AudioFile instance.</returns>
    public static AudioFile FromBytes(byte[] data, string filename, string? contentType = null)
    {
        return new AudioFile
        {
            Data = data,
            Filename = filename,
            ContentType = contentType ?? GetContentTypeFromFilename(filename)
        };
    }

    /// <summary>
    /// Creates an AudioFile from a file path.
    /// </summary>
    /// <param name="filePath">The path to the audio file.</param>
    /// <returns>An AudioFile instance.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file doesn't exist.</exception>
    public static async Task<AudioFile> FromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Audio file not found: {filePath}");

        var data = await File.ReadAllBytesAsync(filePath);
        var filename = Path.GetFileName(filePath);
        var contentType = GetContentTypeFromFilename(filename);

        return new AudioFile
        {
            Data = data,
            Filename = filename,
            ContentType = contentType
        };
    }

    /// <summary>
    /// Gets the appropriate content type for an audio filename.
    /// </summary>
    /// <param name="filename">The filename including extension.</param>
    /// <returns>The MIME type for the audio file.</returns>
    public static string GetContentTypeFromFilename(string filename)
    {
        var extension = Path.GetExtension(filename).ToLowerInvariant();
        return extension switch
        {
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".flac" => "audio/flac",
            ".ogg" => "audio/ogg",
            ".aac" => "audio/aac",
            ".opus" => "audio/opus",
            ".m4a" => "audio/mp4",
            ".webm" => "audio/webm",
            _ => "audio/mpeg"
        };
    }

    /// <summary>
    /// Validates if the audio format is supported.
    /// </summary>
    /// <param name="filename">The filename to check.</param>
    /// <returns>True if the format is supported, false otherwise.</returns>
    public static bool IsFormatSupported(string filename)
    {
        var extension = Path.GetExtension(filename).ToLowerInvariant();
        var supportedExtensions = new[] { ".mp3", ".wav", ".flac", ".ogg", ".aac", ".opus", ".m4a", ".webm" };
        return supportedExtensions.Contains(extension);
    }

    /// <summary>
    /// Gets basic metadata about an audio file.
    /// </summary>
    /// <param name="audioFile">The audio file to analyze.</param>
    /// <returns>Basic metadata about the audio file.</returns>
    public static AudioMetadata GetBasicMetadata(AudioFile audioFile)
    {
        var extension = Path.GetExtension(audioFile.Filename).ToLowerInvariant();
        var format = extension switch
        {
            ".mp3" => AudioFormat.Mp3,
            ".wav" => AudioFormat.Wav,
            ".flac" => AudioFormat.Flac,
            ".ogg" => AudioFormat.Ogg,
            ".aac" => AudioFormat.Aac,
            ".opus" => AudioFormat.Opus,
            ".m4a" => AudioFormat.M4a,
            ".webm" => AudioFormat.Webm,
            _ => AudioFormat.Mp3
        };

        return new AudioMetadata
        {
            Size = audioFile.Data.Length,
            Duration = null, // Would require audio analysis library
            SampleRate = null, // Would require audio analysis library
            Channels = null, // Would require audio analysis library
        };
    }
}
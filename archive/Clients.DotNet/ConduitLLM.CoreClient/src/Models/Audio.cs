using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ConduitLLM.CoreClient.Models;

/// <summary>
/// Supported audio formats for input and output.
/// </summary>
public enum AudioFormat
{
    /// <summary>MP3 format.</summary>
    Mp3,
    /// <summary>WAV format.</summary>
    Wav,
    /// <summary>FLAC format.</summary>
    Flac,
    /// <summary>OGG format.</summary>
    Ogg,
    /// <summary>AAC format.</summary>
    Aac,
    /// <summary>Opus format.</summary>
    Opus,
    /// <summary>PCM format.</summary>
    Pcm,
    /// <summary>M4A format.</summary>
    M4a,
    /// <summary>WebM format.</summary>
    Webm
}

/// <summary>
/// Supported transcription output formats.
/// </summary>
public enum TranscriptionFormat
{
    /// <summary>JSON format.</summary>
    Json,
    /// <summary>Plain text format.</summary>
    Text,
    /// <summary>SRT subtitle format.</summary>
    Srt,
    /// <summary>VTT subtitle format.</summary>
    Vtt,
    /// <summary>Verbose JSON format with timestamps.</summary>
    VerboseJson
}

/// <summary>
/// Timestamp granularity options for transcriptions.
/// </summary>
public enum TimestampGranularity
{
    /// <summary>Segment-level timestamps.</summary>
    Segment,
    /// <summary>Word-level timestamps.</summary>
    Word
}

/// <summary>
/// Available text-to-speech models.
/// </summary>
public enum TextToSpeechModel
{
    /// <summary>OpenAI TTS-1 model.</summary>
    Tts1,
    /// <summary>OpenAI TTS-1-HD model.</summary>
    Tts1Hd,
    /// <summary>ElevenLabs TTS model.</summary>
    ElevenlabsTts,
    /// <summary>Azure TTS model.</summary>
    AzureTts,
    /// <summary>OpenAI TTS model.</summary>
    OpenaiTts
}

/// <summary>
/// Available voices for text-to-speech generation.
/// </summary>
public enum Voice
{
    /// <summary>Alloy voice.</summary>
    Alloy,
    /// <summary>Echo voice.</summary>
    Echo,
    /// <summary>Fable voice.</summary>
    Fable,
    /// <summary>Onyx voice.</summary>
    Onyx,
    /// <summary>Nova voice.</summary>
    Nova,
    /// <summary>Shimmer voice.</summary>
    Shimmer,
    /// <summary>Rachel voice.</summary>
    Rachel,
    /// <summary>Adam voice.</summary>
    Adam,
    /// <summary>Antoni voice.</summary>
    Antoni,
    /// <summary>Arnold voice.</summary>
    Arnold,
    /// <summary>Josh voice.</summary>
    Josh,
    /// <summary>Sam voice.</summary>
    Sam
}

/// <summary>
/// Available transcription models.
/// </summary>
public enum TranscriptionModel
{
    /// <summary>OpenAI Whisper-1 model.</summary>
    Whisper1,
    /// <summary>OpenAI Whisper-Large model.</summary>
    WhisperLarge,
    /// <summary>Deepgram Nova model.</summary>
    DeepgramNova,
    /// <summary>Azure Speech-to-Text model.</summary>
    AzureStt,
    /// <summary>OpenAI Whisper model.</summary>
    OpenaiWhisper
}

/// <summary>
/// Represents an audio file for processing.
/// </summary>
public class AudioFile
{
    /// <summary>
    /// Gets or sets the audio file data.
    /// </summary>
    [Required]
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets or sets the filename of the audio file.
    /// </summary>
    [Required]
    public string Filename { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MIME type of the audio file.
    /// </summary>
    public string? ContentType { get; set; }
}

/// <summary>
/// Advanced voice settings for text-to-speech generation.
/// </summary>
public class VoiceSettings
{
    /// <summary>
    /// Gets or sets the voice stability (0.0 to 1.0).
    /// </summary>
    [Range(0.0, 1.0)]
    public double? Stability { get; set; }

    /// <summary>
    /// Gets or sets the voice similarity boost (0.0 to 1.0).
    /// </summary>
    [Range(0.0, 1.0)]
    [JsonPropertyName("similarity_boost")]
    public double? SimilarityBoost { get; set; }

    /// <summary>
    /// Gets or sets the voice style exaggeration (0.0 to 1.0).
    /// </summary>
    [Range(0.0, 1.0)]
    public double? Style { get; set; }

    /// <summary>
    /// Gets or sets whether to use speaker boost for enhanced clarity.
    /// </summary>
    [JsonPropertyName("use_speaker_boost")]
    public bool? UseSpeakerBoost { get; set; }
}

/// <summary>
/// Request for audio transcription (speech-to-text).
/// </summary>
public class AudioTranscriptionRequest
{
    /// <summary>
    /// Gets or sets the audio file to transcribe.
    /// </summary>
    [Required]
    public AudioFile File { get; set; } = new();

    /// <summary>
    /// Gets or sets the model to use for transcription.
    /// </summary>
    [Required]
    public TranscriptionModel Model { get; set; }

    /// <summary>
    /// Gets or sets the language of the input audio (ISO-639-1 format).
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets an optional text to guide the model's style.
    /// </summary>
    public string? Prompt { get; set; }

    /// <summary>
    /// Gets or sets the format of the transcript output.
    /// </summary>
    [JsonPropertyName("response_format")]
    public TranscriptionFormat? ResponseFormat { get; set; }

    /// <summary>
    /// Gets or sets the sampling temperature (0 to 1).
    /// </summary>
    [Range(0.0, 1.0)]
    public double? Temperature { get; set; }

    /// <summary>
    /// Gets or sets the timestamp granularities to populate.
    /// </summary>
    [JsonPropertyName("timestamp_granularities")]
    public TimestampGranularity[]? TimestampGranularities { get; set; }
}

/// <summary>
/// Response from audio transcription.
/// </summary>
public class AudioTranscriptionResponse
{
    /// <summary>
    /// Gets or sets the transcribed text.
    /// </summary>
    [Required]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the task performed.
    /// </summary>
    public string? Task { get; set; }

    /// <summary>
    /// Gets or sets the language of the input audio.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets the duration of the input audio in seconds.
    /// </summary>
    public double? Duration { get; set; }

    /// <summary>
    /// Gets or sets the transcription segments with timestamps.
    /// </summary>
    public TranscriptionSegment[]? Segments { get; set; }

    /// <summary>
    /// Gets or sets the words with timestamps.
    /// </summary>
    public TranscriptionWord[]? Words { get; set; }

    /// <summary>
    /// Gets or sets the token usage information.
    /// </summary>
    public Usage? Usage { get; set; }
}

/// <summary>
/// Represents a segment of transcribed audio with timing information.
/// </summary>
public class TranscriptionSegment
{
    /// <summary>
    /// Gets or sets the unique identifier of the segment.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the seek offset of the segment.
    /// </summary>
    public int Seek { get; set; }

    /// <summary>
    /// Gets or sets the start time of the segment in seconds.
    /// </summary>
    public double Start { get; set; }

    /// <summary>
    /// Gets or sets the end time of the segment in seconds.
    /// </summary>
    public double End { get; set; }

    /// <summary>
    /// Gets or sets the text content of the segment.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the token IDs for the text content.
    /// </summary>
    public int[] Tokens { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Gets or sets the temperature parameter used for generation.
    /// </summary>
    public double Temperature { get; set; }

    /// <summary>
    /// Gets or sets the average log probability of the segment.
    /// </summary>
    [JsonPropertyName("avg_logprob")]
    public double AvgLogprob { get; set; }

    /// <summary>
    /// Gets or sets the compression ratio of the segment.
    /// </summary>
    [JsonPropertyName("compression_ratio")]
    public double CompressionRatio { get; set; }

    /// <summary>
    /// Gets or sets the probability of no speech.
    /// </summary>
    [JsonPropertyName("no_speech_prob")]
    public double NoSpeechProb { get; set; }
}

/// <summary>
/// Represents a word in transcribed audio with timing information.
/// </summary>
public class TranscriptionWord
{
    /// <summary>
    /// Gets or sets the text content of the word.
    /// </summary>
    public string Word { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the start time of the word in seconds.
    /// </summary>
    public double Start { get; set; }

    /// <summary>
    /// Gets or sets the end time of the word in seconds.
    /// </summary>
    public double End { get; set; }
}

/// <summary>
/// Request for audio translation to English.
/// </summary>
public class AudioTranslationRequest
{
    /// <summary>
    /// Gets or sets the audio file to translate.
    /// </summary>
    [Required]
    public AudioFile File { get; set; } = new();

    /// <summary>
    /// Gets or sets the model to use for translation.
    /// </summary>
    [Required]
    public TranscriptionModel Model { get; set; }

    /// <summary>
    /// Gets or sets an optional text to guide the model's style.
    /// </summary>
    public string? Prompt { get; set; }

    /// <summary>
    /// Gets or sets the format of the transcript output.
    /// </summary>
    [JsonPropertyName("response_format")]
    public TranscriptionFormat? ResponseFormat { get; set; }

    /// <summary>
    /// Gets or sets the sampling temperature (0 to 1).
    /// </summary>
    [Range(0.0, 1.0)]
    public double? Temperature { get; set; }
}

/// <summary>
/// Response from audio translation.
/// </summary>
public class AudioTranslationResponse
{
    /// <summary>
    /// Gets or sets the translated text (always in English).
    /// </summary>
    [Required]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the task performed.
    /// </summary>
    public string? Task { get; set; }

    /// <summary>
    /// Gets or sets the language of the input audio.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets the duration of the input audio in seconds.
    /// </summary>
    public double? Duration { get; set; }

    /// <summary>
    /// Gets or sets the translation segments with timestamps.
    /// </summary>
    public TranscriptionSegment[]? Segments { get; set; }

    /// <summary>
    /// Gets or sets the words with timestamps.
    /// </summary>
    public TranscriptionWord[]? Words { get; set; }

    /// <summary>
    /// Gets or sets the token usage information.
    /// </summary>
    public Usage? Usage { get; set; }
}

/// <summary>
/// Request for text-to-speech generation.
/// </summary>
public class TextToSpeechRequest
{
    /// <summary>
    /// Gets or sets the model to use for speech generation.
    /// </summary>
    [Required]
    public TextToSpeechModel Model { get; set; }

    /// <summary>
    /// Gets or sets the text to convert to speech (max 4096 characters).
    /// </summary>
    [Required]
    [MaxLength(4096)]
    public string Input { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the voice to use for speech generation.
    /// </summary>
    [Required]
    public Voice Voice { get; set; }

    /// <summary>
    /// Gets or sets the format to audio in.
    /// </summary>
    [JsonPropertyName("response_format")]
    public AudioFormat? ResponseFormat { get; set; }

    /// <summary>
    /// Gets or sets the speed of the generated audio (0.25 to 4.0).
    /// </summary>
    [Range(0.25, 4.0)]
    public double? Speed { get; set; }

    /// <summary>
    /// Gets or sets advanced voice settings.
    /// </summary>
    [JsonPropertyName("voice_settings")]
    public VoiceSettings? VoiceSettings { get; set; }
}

/// <summary>
/// Response from text-to-speech generation.
/// </summary>
public class TextToSpeechResponse
{
    /// <summary>
    /// Gets or sets the generated audio data.
    /// </summary>
    [Required]
    public byte[] Audio { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets or sets the format of the returned audio.
    /// </summary>
    public AudioFormat Format { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about the generation.
    /// </summary>
    public AudioMetadata? Metadata { get; set; }

    /// <summary>
    /// Gets or sets token usage information.
    /// </summary>
    public Usage? Usage { get; set; }
}

/// <summary>
/// Metadata about generated or processed audio.
/// </summary>
public class AudioMetadata
{
    /// <summary>
    /// Gets or sets the duration of the audio in seconds.
    /// </summary>
    public double? Duration { get; set; }

    /// <summary>
    /// Gets or sets the size of the audio data in bytes.
    /// </summary>
    public long? Size { get; set; }

    /// <summary>
    /// Gets or sets the sample rate of the audio.
    /// </summary>
    [JsonPropertyName("sample_rate")]
    public int? SampleRate { get; set; }

    /// <summary>
    /// Gets or sets the number of audio channels.
    /// </summary>
    public int? Channels { get; set; }

    /// <summary>
    /// Gets or sets the bit depth of the audio.
    /// </summary>
    [JsonPropertyName("bit_depth")]
    public int? BitDepth { get; set; }

    /// <summary>
    /// Gets or sets the bitrate in kbps.
    /// </summary>
    public int? Bitrate { get; set; }
}

/// <summary>
/// Configuration for hybrid audio processing models.
/// </summary>
public class HybridAudioModels
{
    /// <summary>
    /// Gets or sets the speech-to-text model.
    /// </summary>
    [Required]
    public TranscriptionModel Transcription { get; set; }

    /// <summary>
    /// Gets or sets the chat completion model for LLM processing.
    /// </summary>
    [Required]
    public string Chat { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the text-to-speech model.
    /// </summary>
    [Required]
    public TextToSpeechModel Speech { get; set; }
}

/// <summary>
/// Temperature settings for different stages of hybrid processing.
/// </summary>
public class HybridTemperatureSettings
{
    /// <summary>
    /// Gets or sets the temperature for transcription.
    /// </summary>
    public double? Transcription { get; set; }

    /// <summary>
    /// Gets or sets the temperature for chat completion.
    /// </summary>
    public double? Chat { get; set; }
}

/// <summary>
/// Request for hybrid audio processing (STT + LLM + TTS pipeline).
/// </summary>
public class HybridAudioRequest
{
    /// <summary>
    /// Gets or sets the input audio file for processing.
    /// </summary>
    [Required]
    public AudioFile File { get; set; } = new();

    /// <summary>
    /// Gets or sets the model configuration for each stage.
    /// </summary>
    [Required]
    public HybridAudioModels Models { get; set; } = new();

    /// <summary>
    /// Gets or sets the voice configuration for TTS output.
    /// </summary>
    [Required]
    public Voice Voice { get; set; }

    /// <summary>
    /// Gets or sets the system prompt for the LLM stage.
    /// </summary>
    [JsonPropertyName("system_prompt")]
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Gets or sets additional context for the conversation.
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Gets or sets the language settings.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets temperature settings for each stage.
    /// </summary>
    public HybridTemperatureSettings? Temperature { get; set; }

    /// <summary>
    /// Gets or sets voice settings for TTS.
    /// </summary>
    [JsonPropertyName("voice_settings")]
    public VoiceSettings? VoiceSettings { get; set; }

    /// <summary>
    /// Gets or sets the session ID for conversation continuity.
    /// </summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }
}

/// <summary>
/// Metadata for processing stages in hybrid audio.
/// </summary>
public class HybridStageMetadata
{
    /// <summary>
    /// Gets or sets transcription stage metadata.
    /// </summary>
    public TranscriptionStageMetadata Transcription { get; set; } = new();

    /// <summary>
    /// Gets or sets LLM stage metadata.
    /// </summary>
    public LlmStageMetadata Llm { get; set; } = new();

    /// <summary>
    /// Gets or sets speech generation stage metadata.
    /// </summary>
    public SpeechStageMetadata Speech { get; set; } = new();
}

/// <summary>
/// Metadata for the transcription stage.
/// </summary>
public class TranscriptionStageMetadata
{
    /// <summary>
    /// Gets or sets the processing duration in milliseconds.
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// Gets or sets the transcription confidence.
    /// </summary>
    public double? Confidence { get; set; }

    /// <summary>
    /// Gets or sets the detected language.
    /// </summary>
    public string? Language { get; set; }
}

/// <summary>
/// Metadata for the LLM stage.
/// </summary>
public class LlmStageMetadata
{
    /// <summary>
    /// Gets or sets the processing duration in milliseconds.
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// Gets or sets the number of tokens used.
    /// </summary>
    [JsonPropertyName("tokens_used")]
    public int TokensUsed { get; set; }

    /// <summary>
    /// Gets or sets the model used.
    /// </summary>
    [JsonPropertyName("model_used")]
    public string ModelUsed { get; set; } = string.Empty;
}

/// <summary>
/// Metadata for the speech generation stage.
/// </summary>
public class SpeechStageMetadata
{
    /// <summary>
    /// Gets or sets the processing duration in milliseconds.
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// Gets or sets the duration of the generated audio in seconds.
    /// </summary>
    [JsonPropertyName("audio_duration")]
    public double AudioDuration { get; set; }

    /// <summary>
    /// Gets or sets the format of the generated audio.
    /// </summary>
    public AudioFormat Format { get; set; }
}

/// <summary>
/// Usage statistics for hybrid audio processing.
/// </summary>
public class HybridAudioUsage
{
    /// <summary>
    /// Gets or sets transcription tokens used.
    /// </summary>
    [JsonPropertyName("transcription_tokens")]
    public int? TranscriptionTokens { get; set; }

    /// <summary>
    /// Gets or sets LLM token usage.
    /// </summary>
    [JsonPropertyName("llm_tokens")]
    public Usage LlmTokens { get; set; } = new();

    /// <summary>
    /// Gets or sets total processing time in milliseconds.
    /// </summary>
    [JsonPropertyName("total_processing_time_ms")]
    public double TotalProcessingTimeMs { get; set; }
}

/// <summary>
/// Response from hybrid audio processing.
/// </summary>
public class HybridAudioResponse
{
    /// <summary>
    /// Gets or sets the generated audio response.
    /// </summary>
    [Required]
    public byte[] Audio { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets or sets the transcribed input text.
    /// </summary>
    [Required]
    public string Transcription { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the LLM's text response.
    /// </summary>
    [JsonPropertyName("llm_response")]
    [Required]
    public string LlmResponse { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets metadata for each processing stage.
    /// </summary>
    public HybridStageMetadata Stages { get; set; } = new();

    /// <summary>
    /// Gets or sets combined usage statistics.
    /// </summary>
    public HybridAudioUsage Usage { get; set; } = new();

    /// <summary>
    /// Gets or sets session information.
    /// </summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }
}

/// <summary>
/// Audio processing options and constraints.
/// </summary>
public class AudioProcessingOptions
{
    /// <summary>
    /// Gets or sets the maximum file size in bytes (default: 25MB).
    /// </summary>
    [JsonPropertyName("max_file_size")]
    public long MaxFileSize { get; set; } = 25 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the supported audio formats.
    /// </summary>
    [JsonPropertyName("supported_formats")]
    public AudioFormat[] SupportedFormats { get; set; } = 
    {
        AudioFormat.Mp3, AudioFormat.Wav, AudioFormat.Flac, 
        AudioFormat.Ogg, AudioFormat.Aac, AudioFormat.Opus
    };

    /// <summary>
    /// Gets or sets the quality settings.
    /// </summary>
    public string Quality { get; set; } = "medium";

    /// <summary>
    /// Gets or sets whether to normalize audio.
    /// </summary>
    public bool Normalize { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to remove noise.
    /// </summary>
    public bool Denoise { get; set; } = false;
}

/// <summary>
/// Audio-specific error information.
/// </summary>
public class AudioError
{
    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional error details.
    /// </summary>
    public Dictionary<string, object>? Details { get; set; }
}
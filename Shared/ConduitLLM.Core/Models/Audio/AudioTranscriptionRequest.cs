using System.Text.Json;

namespace ConduitLLM.Core.Models.Audio
{
    /// <summary>
    /// A speech-to-text transcription request (OpenAI <c>/audio/transcriptions</c> compatible).
    /// Built by the Gateway controller from the uploaded audio file plus form fields.
    /// </summary>
    public class AudioTranscriptionRequest
    {
        /// <summary>The model (alias) to transcribe with.</summary>
        public required string Model { get; set; }

        /// <summary>The raw audio bytes to transcribe.</summary>
        public required byte[] AudioData { get; set; }

        /// <summary>The original file name (used to infer format for the multipart upload).</summary>
        public required string FileName { get; set; }

        /// <summary>The audio content type, e.g. <c>audio/mpeg</c>.</summary>
        public string? ContentType { get; set; }

        /// <summary>Optional ISO-639-1 language hint; auto-detected when omitted.</summary>
        public string? Language { get; set; }

        /// <summary>Optional prompt to guide the transcription style.</summary>
        public string? Prompt { get; set; }

        /// <summary>Optional sampling temperature (0-1).</summary>
        public double? Temperature { get; set; }

        /// <summary>Response format: <c>json</c> (default), <c>text</c>, or <c>verbose_json</c>.</summary>
        public string? ResponseFormat { get; set; }

        /// <summary>Optional end-user identifier.</summary>
        public string? User { get; set; }

        /// <summary>Optional provider chunking strategy, represented as a string or JSON object.</summary>
        public JsonElement? ChunkingStrategy { get; set; }

        /// <summary>Additional response data to include.</summary>
        public List<string>? Include { get; set; }

        /// <summary>Names corresponding to known speaker reference files.</summary>
        public List<string>? KnownSpeakerNames { get; set; }

        /// <summary>Known speaker reference audio files.</summary>
        public List<AudioTranscriptionReference>? KnownSpeakerReferences { get; set; }

        /// <summary>Whether the provider should stream transcription events.</summary>
        public bool? Stream { get; set; }

        /// <summary>Timestamp granularities requested in verbose responses.</summary>
        public List<string>? TimestampGranularities { get; set; }

        /// <summary>Provider-specific passthrough fields.</summary>
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    public sealed class AudioTranscriptionReference
    {
        public required byte[] AudioData { get; set; }
        public required string FileName { get; set; }
        public string? ContentType { get; set; }
    }
}

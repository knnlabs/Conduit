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

        /// <summary>Provider-specific passthrough fields.</summary>
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }
}

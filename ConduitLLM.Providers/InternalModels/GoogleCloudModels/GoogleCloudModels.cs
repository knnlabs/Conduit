using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ConduitLLM.Providers.InternalModels.GoogleCloudModels
{
    // Google Cloud Speech-to-Text Models

    /// <summary>
    /// Request for Google Cloud Speech-to-Text API
    /// </summary>
    public class GoogleCloudSpeechRequest
    {
        [JsonPropertyName("config")]
        public GoogleCloudSpeechConfig Config { get; set; } = null!;

        [JsonPropertyName("audio")]
        public GoogleCloudAudioContent Audio { get; set; } = null!;
    }

    /// <summary>
    /// Speech recognition configuration
    /// </summary>
    public class GoogleCloudSpeechConfig
    {
        [JsonPropertyName("encoding")]
        public string Encoding { get; set; } = null!;

        [JsonPropertyName("sampleRateHertz")]
        public int SampleRateHertz { get; set; }

        [JsonPropertyName("languageCode")]
        public string LanguageCode { get; set; } = null!;

        [JsonPropertyName("maxAlternatives")]
        public int? MaxAlternatives { get; set; }

        [JsonPropertyName("profanityFilter")]
        public bool? ProfanityFilter { get; set; }

        [JsonPropertyName("enableWordTimeOffsets")]
        public bool? EnableWordTimeOffsets { get; set; }

        [JsonPropertyName("enableAutomaticPunctuation")]
        public bool? EnableAutomaticPunctuation { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("useEnhanced")]
        public bool? UseEnhanced { get; set; }
    }

    /// <summary>
    /// Audio content for recognition
    /// </summary>
    public class GoogleCloudAudioContent
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("uri")]
        public string? Uri { get; set; }
    }

    /// <summary>
    /// Response from Google Cloud Speech-to-Text API
    /// </summary>
    public class GoogleCloudSpeechResponse
    {
        [JsonPropertyName("results")]
        public List<GoogleCloudSpeechResult>? Results { get; set; }

        [JsonPropertyName("totalBilledTime")]
        public string? TotalBilledTime { get; set; }
    }

    /// <summary>
    /// Speech recognition result
    /// </summary>
    public class GoogleCloudSpeechResult
    {
        [JsonPropertyName("alternatives")]
        public List<GoogleCloudSpeechAlternative>? Alternatives { get; set; }

        [JsonPropertyName("channelTag")]
        public int? ChannelTag { get; set; }

        [JsonPropertyName("languageCode")]
        public string? LanguageCode { get; set; }
    }

    /// <summary>
    /// Alternative transcription
    /// </summary>
    public class GoogleCloudSpeechAlternative
    {
        [JsonPropertyName("transcript")]
        public string Transcript { get; set; } = null!;

        [JsonPropertyName("confidence")]
        public float? Confidence { get; set; }

        [JsonPropertyName("words")]
        public List<GoogleCloudWordInfo>? Words { get; set; }
    }

    /// <summary>
    /// Word-level information
    /// </summary>
    public class GoogleCloudWordInfo
    {
        [JsonPropertyName("startTime")]
        public double? StartTime { get; set; }

        [JsonPropertyName("endTime")]
        public double? EndTime { get; set; }

        [JsonPropertyName("word")]
        public string Word { get; set; } = null!;

        [JsonPropertyName("confidence")]
        public float? Confidence { get; set; }

        [JsonPropertyName("speakerTag")]
        public int? SpeakerTag { get; set; }
    }

    // Google Cloud Text-to-Speech Models

    /// <summary>
    /// Request for Google Cloud Text-to-Speech API
    /// </summary>
    public class GoogleCloudTTSRequest
    {
        [JsonPropertyName("input")]
        public GoogleCloudTTSInput Input { get; set; } = null!;

        [JsonPropertyName("voice")]
        public GoogleCloudTTSVoice Voice { get; set; } = null!;

        [JsonPropertyName("audioConfig")]
        public GoogleCloudTTSAudioConfig AudioConfig { get; set; } = null!;
    }

    /// <summary>
    /// Text input for synthesis
    /// </summary>
    public class GoogleCloudTTSInput
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("ssml")]
        public string? Ssml { get; set; }
    }

    /// <summary>
    /// Voice selection parameters
    /// </summary>
    public class GoogleCloudTTSVoice
    {
        [JsonPropertyName("languageCode")]
        public string LanguageCode { get; set; } = null!;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("ssmlGender")]
        public string? SsmlGender { get; set; }
    }

    /// <summary>
    /// Audio configuration for synthesis
    /// </summary>
    public class GoogleCloudTTSAudioConfig
    {
        [JsonPropertyName("audioEncoding")]
        public string AudioEncoding { get; set; } = null!;

        [JsonPropertyName("speakingRate")]
        public double? SpeakingRate { get; set; }

        [JsonPropertyName("pitch")]
        public double? Pitch { get; set; }

        [JsonPropertyName("volumeGainDb")]
        public double? VolumeGainDb { get; set; }

        [JsonPropertyName("sampleRateHertz")]
        public int? SampleRateHertz { get; set; }

        [JsonPropertyName("effectsProfileId")]
        public List<string>? EffectsProfileId { get; set; }
    }

    /// <summary>
    /// Response from Google Cloud Text-to-Speech API
    /// </summary>
    public class GoogleCloudTTSResponse
    {
        [JsonPropertyName("audioContent")]
        public string AudioContent { get; set; } = null!;

        [JsonPropertyName("timepoints")]
        public List<GoogleCloudTimepoint>? Timepoints { get; set; }

        [JsonPropertyName("audioConfig")]
        public GoogleCloudTTSAudioConfig? AudioConfig { get; set; }
    }

    /// <summary>
    /// Timepoint information
    /// </summary>
    public class GoogleCloudTimepoint
    {
        [JsonPropertyName("markName")]
        public string? MarkName { get; set; }

        [JsonPropertyName("timeSeconds")]
        public double TimeSeconds { get; set; }
    }

    /// <summary>
    /// Response for listing available voices
    /// </summary>
    public class GoogleCloudVoicesResponse
    {
        [JsonPropertyName("voices")]
        public List<GoogleCloudVoiceInfo>? Voices { get; set; }
    }

    /// <summary>
    /// Information about an available voice
    /// </summary>
    public class GoogleCloudVoiceInfo
    {
        [JsonPropertyName("languageCodes")]
        public List<string>? LanguageCodes { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = null!;

        [JsonPropertyName("ssmlGender")]
        public string SsmlGender { get; set; } = null!;

        [JsonPropertyName("naturalSampleRateHertz")]
        public int NaturalSampleRateHertz { get; set; }
    }
}
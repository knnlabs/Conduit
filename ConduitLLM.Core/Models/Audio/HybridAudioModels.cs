using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Core.Models.Audio
{
    /// <summary>
    /// Request for processing audio through the hybrid STT-LLM-TTS pipeline.
    /// </summary>
    public class HybridAudioRequest
    {
        /// <summary>
        /// Gets or sets the session ID for maintaining conversation context.
        /// </summary>
        /// <value>
        /// The unique identifier of the conversation session. If null, a single-turn interaction is performed.
        /// </value>
        public string? SessionId { get; set; }

        /// <summary>
        /// Gets or sets the audio data to be processed.
        /// </summary>
        /// <value>
        /// The raw audio bytes in a supported format (e.g., MP3, WAV, WebM).
        /// </value>
        [Required]
        public byte[] AudioData { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Gets or sets the format of the input audio.
        /// </summary>
        /// <value>
        /// The audio format identifier (e.g., "mp3", "wav", "webm").
        /// </value>
        [Required]
        public string AudioFormat { get; set; } = "mp3";

        /// <summary>
        /// Gets or sets the language of the input audio.
        /// </summary>
        /// <value>
        /// ISO 639-1 language code (e.g., "en", "es", "fr"). If null, automatic detection is used.
        /// </value>
        public string? Language { get; set; }

        /// <summary>
        /// Gets or sets the system prompt for the LLM.
        /// </summary>
        /// <value>
        /// Instructions that define the assistant's behavior and personality.
        /// </value>
        public string? SystemPrompt { get; set; }

        /// <summary>
        /// Gets or sets the preferred voice for TTS output.
        /// </summary>
        /// <value>
        /// The voice identifier. If null, the default voice is used.
        /// </value>
        public string? VoiceId { get; set; }

        /// <summary>
        /// Gets or sets the desired output audio format.
        /// </summary>
        /// <value>
        /// The audio format for the response (e.g., "mp3", "wav"). Defaults to "mp3".
        /// </value>
        public string OutputFormat { get; set; } = "mp3";

        /// <summary>
        /// Gets or sets the temperature for LLM response generation.
        /// </summary>
        /// <value>
        /// Controls randomness in responses. Range: 0.0 to 2.0. Default: 0.7.
        /// </value>
        [Range(0.0, 2.0)]
        public double Temperature { get; set; } = 0.7;

        /// <summary>
        /// Gets or sets the maximum tokens for the LLM response.
        /// </summary>
        /// <value>
        /// Limits the length of the generated response. Default: 150.
        /// </value>
        [Range(1, 4096)]
        public int MaxTokens { get; set; } = 150;

        /// <summary>
        /// Gets or sets whether to enable streaming mode.
        /// </summary>
        /// <value>
        /// If true, responses are streamed for lower latency. Default: false.
        /// </value>
        public bool EnableStreaming { get; set; } = false;

        /// <summary>
        /// Gets or sets custom metadata for the request.
        /// </summary>
        /// <value>
        /// Additional key-value pairs for tracking or customization.
        /// </value>
        public Dictionary<string, string>? Metadata { get; set; }
    }

    /// <summary>
    /// Response from the hybrid audio processing pipeline.
    /// </summary>
    public class HybridAudioResponse
    {
        /// <summary>
        /// Gets or sets the generated audio data.
        /// </summary>
        /// <value>
        /// The synthesized speech audio in the requested format.
        /// </value>
        public byte[] AudioData { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Gets or sets the format of the output audio.
        /// </summary>
        /// <value>
        /// The audio format identifier (e.g., "mp3", "wav").
        /// </value>
        public string AudioFormat { get; set; } = "mp3";

        /// <summary>
        /// Gets or sets the transcribed text from the input audio.
        /// </summary>
        /// <value>
        /// The text representation of the user's speech input.
        /// </value>
        public string TranscribedText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the LLM-generated response text.
        /// </summary>
        /// <value>
        /// The text response before TTS conversion.
        /// </value>
        public string ResponseText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the detected language of the input.
        /// </summary>
        /// <value>
        /// ISO 639-1 language code of the detected language.
        /// </value>
        public string? DetectedLanguage { get; set; }

        /// <summary>
        /// Gets or sets the voice used for synthesis.
        /// </summary>
        /// <value>
        /// The identifier of the voice used for TTS.
        /// </value>
        public string? VoiceUsed { get; set; }

        /// <summary>
        /// Gets or sets the duration of the output audio.
        /// </summary>
        /// <value>
        /// The length of the generated audio in seconds.
        /// </value>
        public double DurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the processing metrics.
        /// </summary>
        /// <value>
        /// Timing information for each pipeline stage.
        /// </value>
        public ProcessingMetrics? Metrics { get; set; }

        /// <summary>
        /// Gets or sets the session ID if part of a conversation.
        /// </summary>
        /// <value>
        /// The conversation session identifier.
        /// </value>
        public string? SessionId { get; set; }

        /// <summary>
        /// Gets or sets custom metadata from the response.
        /// </summary>
        /// <value>
        /// Additional key-value pairs from processing.
        /// </value>
        public Dictionary<string, string>? Metadata { get; set; }
    }

    /// <summary>
    /// Represents a chunk of audio data in streaming responses.
    /// </summary>
    public class HybridAudioChunk
    {
        /// <summary>
        /// Gets or sets the chunk type.
        /// </summary>
        /// <value>
        /// The type of data in this chunk (e.g., "transcription", "text", "audio").
        /// </value>
        public string ChunkType { get; set; } = "audio";

        /// <summary>
        /// Gets or sets the audio data chunk.
        /// </summary>
        /// <value>
        /// Partial audio data, if this is an audio chunk.
        /// </value>
        public byte[]? AudioData { get; set; }

        /// <summary>
        /// Gets or sets the text content.
        /// </summary>
        /// <value>
        /// Text data for transcription or response chunks.
        /// </value>
        public string? TextContent { get; set; }

        /// <summary>
        /// Gets or sets whether this is the final chunk.
        /// </summary>
        /// <value>
        /// True if this is the last chunk in the stream.
        /// </value>
        public bool IsFinal { get; set; }

        /// <summary>
        /// Gets or sets the sequence number.
        /// </summary>
        /// <value>
        /// The order of this chunk in the stream.
        /// </value>
        public int SequenceNumber { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of this chunk.
        /// </summary>
        /// <value>
        /// When this chunk was generated.
        /// </value>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Configuration for a hybrid audio conversation session.
    /// </summary>
    public class HybridSessionConfig
    {
        /// <summary>
        /// Gets or sets the STT provider to use.
        /// </summary>
        /// <value>
        /// The identifier of the speech-to-text provider.
        /// </value>
        public string? SttProvider { get; set; }

        /// <summary>
        /// Gets or sets the LLM model to use.
        /// </summary>
        /// <value>
        /// The model identifier for text generation.
        /// </value>
        public string? LlmModel { get; set; }

        /// <summary>
        /// Gets or sets the TTS provider to use.
        /// </summary>
        /// <value>
        /// The identifier of the text-to-speech provider.
        /// </value>
        public string? TtsProvider { get; set; }

        /// <summary>
        /// Gets or sets the system prompt for the conversation.
        /// </summary>
        /// <value>
        /// Instructions that persist across the entire conversation.
        /// </value>
        public string? SystemPrompt { get; set; }

        /// <summary>
        /// Gets or sets the default voice for responses.
        /// </summary>
        /// <value>
        /// The voice identifier for TTS synthesis.
        /// </value>
        public string? DefaultVoice { get; set; }

        /// <summary>
        /// Gets or sets the conversation history limit.
        /// </summary>
        /// <value>
        /// Maximum number of turns to keep in context. Default: 10.
        /// </value>
        [Range(1, 100)]
        public int MaxHistoryTurns { get; set; } = 10;

        /// <summary>
        /// Gets or sets the session timeout.
        /// </summary>
        /// <value>
        /// Duration of inactivity before session expires. Default: 30 minutes.
        /// </value>
        public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Gets or sets whether to enable latency optimization.
        /// </summary>
        /// <value>
        /// If true, applies various optimizations to reduce latency.
        /// </value>
        public bool EnableLatencyOptimization { get; set; } = true;

        /// <summary>
        /// Gets or sets custom session metadata.
        /// </summary>
        /// <value>
        /// Additional configuration parameters.
        /// </value>
        public Dictionary<string, string>? Metadata { get; set; }
    }

    /// <summary>
    /// Metrics for processing stages in the hybrid pipeline.
    /// </summary>
    public class ProcessingMetrics
    {
        /// <summary>
        /// Gets or sets the STT processing time.
        /// </summary>
        /// <value>
        /// Time taken for speech-to-text conversion in milliseconds.
        /// </value>
        public double SttLatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the LLM processing time.
        /// </summary>
        /// <value>
        /// Time taken for response generation in milliseconds.
        /// </value>
        public double LlmLatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the TTS processing time.
        /// </summary>
        /// <value>
        /// Time taken for text-to-speech conversion in milliseconds.
        /// </value>
        public double TtsLatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the total processing time.
        /// </summary>
        /// <value>
        /// End-to-end latency in milliseconds.
        /// </value>
        public double TotalLatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the input audio duration.
        /// </summary>
        /// <value>
        /// Length of the input audio in seconds.
        /// </value>
        public double InputDurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the output audio duration.
        /// </summary>
        /// <value>
        /// Length of the generated audio in seconds.
        /// </value>
        public double OutputDurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the tokens used in LLM processing.
        /// </summary>
        /// <value>
        /// Token count for the LLM request and response.
        /// </value>
        public int TokensUsed { get; set; }
    }

    /// <summary>
    /// Latency metrics for the hybrid audio pipeline.
    /// </summary>
    public class HybridLatencyMetrics
    {
        /// <summary>
        /// Gets or sets the average STT latency.
        /// </summary>
        /// <value>
        /// Average time for speech-to-text across recent requests.
        /// </value>
        public double AverageSttLatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the average LLM latency.
        /// </summary>
        /// <value>
        /// Average time for LLM response generation.
        /// </value>
        public double AverageLlmLatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the average TTS latency.
        /// </summary>
        /// <value>
        /// Average time for text-to-speech synthesis.
        /// </value>
        public double AverageTtsLatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the average total latency.
        /// </summary>
        /// <value>
        /// Average end-to-end processing time.
        /// </value>
        public double AverageTotalLatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the 95th percentile latency.
        /// </summary>
        /// <value>
        /// P95 latency for the complete pipeline.
        /// </value>
        public double P95LatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the 99th percentile latency.
        /// </summary>
        /// <value>
        /// P99 latency for the complete pipeline.
        /// </value>
        public double P99LatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the sample count.
        /// </summary>
        /// <value>
        /// Number of requests used to calculate these metrics.
        /// </value>
        public int SampleCount { get; set; }

        /// <summary>
        /// Gets or sets when these metrics were calculated.
        /// </summary>
        /// <value>
        /// The timestamp of metric calculation.
        /// </value>
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    }
}

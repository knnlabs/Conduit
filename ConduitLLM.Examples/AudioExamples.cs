using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using ConduitLLM.Core;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Examples
{
    /// <summary>
    /// Examples demonstrating audio API usage in ConduitLLM.
    /// </summary>
    public class AudioExamples
    {
        private readonly IConduit _conduit;
        private readonly ILogger<AudioExamples> _logger;

        public AudioExamples(IConduit conduit, ILogger<AudioExamples> logger)
        {
            _conduit = conduit;
            _logger = logger;
        }

        /// <summary>
        /// Example: Transcribe audio file to text using Whisper.
        /// </summary>
        public async Task TranscribeAudioExample()
        {
            _logger.LogInformation("=== Audio Transcription Example ===");

            try
            {
                // Get a client that supports transcription (e.g., whisper-1)
                var client = _conduit.GetClient("whisper-1");

                if (client is IAudioTranscriptionClient transcriptionClient)
                {
                    // Load audio file (replace with actual audio file path)
                    var audioPath = "path/to/audio.mp3";
                    byte[] audioData = File.Exists(audioPath)
                        ? await File.ReadAllBytesAsync(audioPath)
                        : GenerateSampleAudioData(); // Dummy data for example

                    var request = new AudioTranscriptionRequest
                    {
                        AudioData = audioData,
                        Language = "en", // Optional - auto-detect if not specified
                        ResponseFormat = TranscriptionFormat.Text,
                        TimestampGranularity = TimestampGranularity.Segment
                    };

                    var response = await transcriptionClient.TranscribeAudioAsync(request);

                    _logger.LogInformation("Transcription: {Text}", response.Text);
                    _logger.LogInformation("Language: {Language}", response.Language);
                    _logger.LogInformation("Duration: {Duration}s", response.Duration);

                    // If segments are included
                    if (response.Segments != null)
                    {
                        foreach (var segment in response.Segments)
                        {
                            _logger.LogInformation("[{Start:F2}s - {End:F2}s] {Text}",
                                segment.Start, segment.End, segment.Text);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Client does not support audio transcription");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during transcription");
            }
        }

        /// <summary>
        /// Example: Convert text to speech.
        /// </summary>
        public async Task TextToSpeechExample()
        {
            _logger.LogInformation("=== Text-to-Speech Example ===");

            try
            {
                // Get a client that supports TTS (e.g., tts-1)
                var client = _conduit.GetClient("tts-1");

                if (client is ITextToSpeechClient ttsClient)
                {
                    // List available voices
                    var voices = await ttsClient.ListVoicesAsync();
                    _logger.LogInformation("Available voices: {Voices}", string.Join(", ", voices));

                    var request = new TextToSpeechRequest
                    {
                        Input = "Hello! This is a test of the ConduitLLM text-to-speech system. " +
                               "It supports multiple voices and languages.",
                        Voice = "alloy", // OpenAI voice
                        Model = "tts-1",
                        ResponseFormat = AudioFormat.Mp3,
                        Speed = 1.0
                    };

                    var response = await ttsClient.CreateSpeechAsync(request);

                    _logger.LogInformation("Generated audio: {Length} bytes", response.AudioData.Length);

                    // Save to file
                    var outputPath = "output_speech.mp3";
                    await File.WriteAllBytesAsync(outputPath, response.AudioData);
                    _logger.LogInformation("Audio saved to: {Path}", outputPath);
                }
                else
                {
                    _logger.LogWarning("Client does not support text-to-speech");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during text-to-speech");
            }
        }

        /// <summary>
        /// Example: Using audio with different providers.
        /// </summary>
        public async Task MultiProviderAudioExample()
        {
            _logger.LogInformation("=== Multi-Provider Audio Example ===");

            // Example 1: Use OpenAI for transcription
            var whisperClient = _conduit.GetClient("whisper-1");
            if (whisperClient is IAudioTranscriptionClient transcription)
            {
                _logger.LogInformation("Using OpenAI Whisper for transcription");
                // Transcribe audio...
            }

            // Example 2: Use ElevenLabs for high-quality TTS
            var elevenLabsClient = _conduit.GetClient("eleven_multilingual_v1");
            if (elevenLabsClient is ITextToSpeechClient tts)
            {
                _logger.LogInformation("Using ElevenLabs for text-to-speech");
                // Generate speech with ElevenLabs voices...
            }

            // Example 3: Use Ultravox for telephony
            var ultravoxClient = _conduit.GetClient("ultravox-telephony");
            if (ultravoxClient is IRealtimeAudioClient realtime)
            {
                _logger.LogInformation("Using Ultravox for telephony real-time audio");
                // Handle phone calls with 8kHz audio...
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Generates dummy audio data for examples.
        /// </summary>
        private byte[] GenerateSampleAudioData()
        {
            // In a real application, this would be actual audio data
            // For demo purposes, return some dummy bytes
            var random = new Random();
            var data = new byte[1024];
            random.NextBytes(data);
            return data;
        }

        /// <summary>
        /// Run all audio examples.
        /// </summary>
        public async Task RunAllExamples()
        {
            await TranscribeAudioExample();
            _logger.LogInformation("");

            await TextToSpeechExample();
            _logger.LogInformation("");

            await MultiProviderAudioExample();
        }
    }
}

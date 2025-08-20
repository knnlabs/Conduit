using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    public partial class HybridAudioService
    {
        /// <inheritdoc />
        public async Task<HybridAudioResponse> ProcessAudioAsync(
            HybridAudioRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var stopwatch = Stopwatch.StartNew();
            var metrics = new ProcessingMetrics();

            try
            {
                _logger.LogDebug("Starting hybrid audio processing");

                // Pre-process audio: noise reduction and normalization
                var processedAudioData = request.AudioData;
                if (request.EnableStreaming) // Only process if streaming is enabled (as a quality flag)
                {
                    // Apply noise reduction
                    processedAudioData = await _audioProcessingService.ReduceNoiseAsync(
                        processedAudioData,
                        request.AudioFormat,
                        0.7, // Moderate aggressiveness
                        cancellationToken);

                    // Normalize audio levels
                    processedAudioData = await _audioProcessingService.NormalizeAudioAsync(
                        processedAudioData,
                        request.AudioFormat,
                        -3.0, // Standard target level
                        cancellationToken);
                }

                // Step 1: Speech-to-Text
                var sttStart = stopwatch.ElapsedMilliseconds;
                // Create a minimal transcription request for routing
                var routingRequest = new AudioTranscriptionRequest
                {
                    Language = request.Language
                };
                var transcriptionClient = await _audioRouter.GetTranscriptionClientAsync(
                    routingRequest,
                    request.VirtualKey ?? string.Empty,
                    cancellationToken);

                if (transcriptionClient == null)
                    throw new InvalidOperationException("No STT provider available");

                // Check if format conversion is needed
                var supportedFormats = await transcriptionClient.GetSupportedFormatsAsync(cancellationToken);
                var audioDataForStt = processedAudioData;
                var audioFormatForStt = request.AudioFormat;

                if (!supportedFormats.Contains(request.AudioFormat))
                {
                    // Convert to a supported format (prefer wav for quality)
                    var targetFormat = supportedFormats.Contains("wav") ? "wav" : supportedFormats.FirstOrDefault() ?? "mp3";
                    if (_audioProcessingService.IsConversionSupported(request.AudioFormat, targetFormat))
                    {
                        audioDataForStt = await _audioProcessingService.ConvertFormatAsync(
                            processedAudioData,
                            request.AudioFormat,
                            targetFormat,
                            cancellationToken);
                        audioFormatForStt = targetFormat;
                        _logger.LogDebug("Converted audio from {Source} to {Target} for STT", request.AudioFormat, targetFormat);
                    }
                }

                var transcriptionRequest = new AudioTranscriptionRequest
                {
                    AudioData = audioDataForStt,
                    AudioFormat = audioFormatForStt == "mp3" ? AudioFormat.Mp3 :
                                 audioFormatForStt == "wav" ? AudioFormat.Wav :
                                 audioFormatForStt == "flac" ? AudioFormat.Flac :
                                 audioFormatForStt == "webm" ? AudioFormat.Mp3 : // WebM not in enum
                                 audioFormatForStt == "ogg" ? AudioFormat.Ogg :
                                 AudioFormat.Mp3,
                    Language = request.Language
                };

                var transcription = await transcriptionClient.TranscribeAudioAsync(
                    transcriptionRequest,
                    cancellationToken: cancellationToken);

                metrics.SttLatencyMs = stopwatch.ElapsedMilliseconds - sttStart;
                metrics.InputDurationSeconds = transcription.Duration ?? 0;

                _logger.LogDebug("Transcription completed: {Text}", transcription.Text);

                // Step 2: LLM Processing
                var llmStart = stopwatch.ElapsedMilliseconds;
                var messages = await BuildMessagesAsync(
                    request.SessionId,
                    transcription.Text,
                    request.SystemPrompt);

                var llmRequest = new ChatCompletionRequest
                {
                    Model = "gpt-4o-mini", // Default model, will be routed by ILLMRouter
                    Messages = messages,
                    Temperature = request.Temperature,
                    MaxTokens = request.MaxTokens,
                    Stream = false
                };

                var llmResponse = await _llmRouter.CreateChatCompletionAsync(
                    llmRequest,
                    cancellationToken: cancellationToken);

                var responseText = llmResponse.Choices?.FirstOrDefault()?.Message?.Content?.ToString() ?? "";
                metrics.LlmLatencyMs = stopwatch.ElapsedMilliseconds - llmStart;
                metrics.TokensUsed = llmResponse.Usage?.TotalTokens ?? 0;

                _logger.LogDebug("LLM response generated: {Text}", responseText);

                // Update session history if applicable
                if (!string.IsNullOrEmpty(request.SessionId) && _sessions.TryGetValue(request.SessionId, out var session))
                {
                    session.AddTurn(transcription.Text ?? string.Empty, responseText ?? string.Empty);
                    session.LastActivity = DateTime.UtcNow;
                }

                // Step 3: Text-to-Speech
                var ttsStart = stopwatch.ElapsedMilliseconds;
                // Create a minimal TTS request for routing
                var ttsRoutingRequest = new TextToSpeechRequest
                {
                    Voice = request.VoiceId ?? "alloy",
                    Input = responseText ?? string.Empty
                };
                var ttsClient = await _audioRouter.GetTextToSpeechClientAsync(
                    ttsRoutingRequest,
                    request.VirtualKey ?? string.Empty,
                    cancellationToken);

                if (ttsClient == null)
                    throw new InvalidOperationException("No TTS provider available");

                var ttsRequest = new TextToSpeechRequest
                {
                    Input = responseText ?? string.Empty,
                    Voice = request.VoiceId ?? "alloy", // Default voice if not specified
                    ResponseFormat = request.OutputFormat == "mp3" ? AudioFormat.Mp3 :
                                   request.OutputFormat == "wav" ? AudioFormat.Wav :
                                   request.OutputFormat == "flac" ? AudioFormat.Flac :
                                   request.OutputFormat == "ogg" ? AudioFormat.Ogg :
                                   AudioFormat.Mp3 // Default to MP3
                };

                var ttsResponse = await ttsClient.CreateSpeechAsync(
                    ttsRequest,
                    cancellationToken: cancellationToken);

                metrics.TtsLatencyMs = stopwatch.ElapsedMilliseconds - ttsStart;
                metrics.OutputDurationSeconds = ttsResponse.Duration ?? 0;

                _logger.LogDebug("TTS completed, audio size: {Size} bytes", ttsResponse.AudioData.Length);

                // Post-process TTS output: compress if needed
                var finalAudioData = ttsResponse.AudioData;
                var finalAudioFormat = ttsResponse.Format?.ToString().ToLower() ?? request.OutputFormat;

                // Apply compression for smaller file sizes (except for lossless formats)
                if (!new[] { "wav", "flac" }.Contains(finalAudioFormat.ToLower()))
                {
                    finalAudioData = await _audioProcessingService.CompressAudioAsync(
                        finalAudioData,
                        finalAudioFormat,
                        0.85, // High quality compression
                        cancellationToken);
                    _logger.LogDebug("Compressed audio from {Original} to {Compressed} bytes",
                        ttsResponse.AudioData.Length, finalAudioData.Length);
                }

                // Complete metrics
                metrics.TotalLatencyMs = stopwatch.ElapsedMilliseconds;
                RecordMetrics(metrics);

                // Build response
                return new HybridAudioResponse
                {
                    AudioData = finalAudioData,
                    AudioFormat = finalAudioFormat,
                    TranscribedText = transcription.Text ?? string.Empty,
                    ResponseText = responseText!,
                    DetectedLanguage = transcription.Language ?? request.Language,
                    VoiceUsed = ttsResponse.VoiceUsed,
                    DurationSeconds = metrics.OutputDurationSeconds,
                    Metrics = metrics,
                    SessionId = request.SessionId,
                    Metadata = request.Metadata
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in hybrid audio processing");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // For hybrid audio service, we can't check specific provider availability without a virtual key
                // The actual availability will be determined when ProcessConversationAsync is called with a valid key
                // For now, just check if the LLM router is available
                
                // Check LLM availability
                var testRequest = new ChatCompletionRequest
                {
                    Model = "gpt-4o-mini",
                    Messages = new List<Message> { new() { Role = "user", Content = "test" } }
                };
                try
                {
                    // Try to create a completion to check availability
                    await _llmRouter.CreateChatCompletionAsync(testRequest, cancellationToken: cancellationToken);
                }
                catch (Exception llmEx)
                {
                    _logger.LogWarning(llmEx, "LLM availability check failed");
                    return false;
                }

                // TTS availability will be checked when actually processing with a valid virtual key
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking hybrid audio availability");
                return false;
            }
        }
    }
}
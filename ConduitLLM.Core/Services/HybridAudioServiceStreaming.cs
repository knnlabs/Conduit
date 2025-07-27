using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
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
        public async IAsyncEnumerable<HybridAudioChunk> StreamProcessAudioAsync(
            HybridAudioRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var sequenceNumber = 0;

            _logger.LogDebug("Starting streaming hybrid audio processing");

            // Step 1: Process transcription
            AudioTranscriptionResponse? transcriptionResult = null;
            Exception? transcriptionError = null;

            try
            {
                transcriptionResult = await ProcessTranscriptionAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                transcriptionError = ex;
                _logger.LogError(ex, "Error in transcription phase");
            }

            if (transcriptionError != null)
                throw transcriptionError;

            if (transcriptionResult == null)
                throw new InvalidOperationException("Transcription failed");

            // Yield transcription chunk
            yield return new HybridAudioChunk
            {
                ChunkType = "transcription",
                TextContent = transcriptionResult.Text,
                SequenceNumber = sequenceNumber++,
                IsFinal = false
            };

            _logger.LogDebug("Transcription completed: {Text}", transcriptionResult.Text);

            // Step 2: Process LLM with streaming
            var responseBuilder = new StringBuilder();
            var ttsQueue = new Queue<string>();
            var llmError = await ProcessLlmStreamingAsync(
                request,
                transcriptionResult,
                responseBuilder,
                ttsQueue,
                cancellationToken);

            if (llmError != null)
            {
                _logger.LogError(llmError, "Error in LLM processing");
                throw llmError;
            }

            // Yield LLM text chunks
            var textChunks = GetTextChunks(responseBuilder, sequenceNumber);
            sequenceNumber += textChunks.Count;
            foreach (var chunk in textChunks)
            {
                yield return chunk;
            }

            // Step 3: Process TTS
            var ttsChunks = new List<HybridAudioChunk>();
            var ttsError = await ProcessTtsAsync(
                request,
                ttsQueue,
                ttsChunks,
                sequenceNumber,
                cancellationToken);

            if (ttsError != null)
            {
                _logger.LogError(ttsError, "Error in TTS processing");
                throw ttsError;
            }

            // Yield TTS chunks
            foreach (var chunk in ttsChunks)
            {
                yield return chunk;
            }

            // Final chunk
            yield return new HybridAudioChunk
            {
                ChunkType = "complete",
                SequenceNumber = sequenceNumber++,
                IsFinal = true
            };

            _logger.LogDebug("Streaming hybrid audio processing completed");
        }

        private async Task<AudioTranscriptionResponse> ProcessTranscriptionAsync(
            HybridAudioRequest request,
            CancellationToken cancellationToken)
        {
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
            var audioDataForStt = request.AudioData;
            var audioFormatForStt = request.AudioFormat;

            if (!supportedFormats.Contains(request.AudioFormat))
            {
                // Convert to a supported format (prefer wav for quality)
                var targetFormat = supportedFormats.Contains("wav") ? "wav" : supportedFormats.FirstOrDefault() ?? "mp3";
                if (_audioProcessingService.IsConversionSupported(request.AudioFormat, targetFormat))
                {
                    audioDataForStt = await _audioProcessingService.ConvertFormatAsync(
                        request.AudioData,
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

            return await transcriptionClient.TranscribeAudioAsync(
                transcriptionRequest,
                cancellationToken: cancellationToken);
        }

        private async Task<Exception?> ProcessLlmStreamingAsync(
            HybridAudioRequest request,
            AudioTranscriptionResponse transcription,
            StringBuilder responseBuilder,
            Queue<string> ttsQueue,
            CancellationToken cancellationToken)
        {
            try
            {
                var messages = await BuildMessagesAsync(
                    request.SessionId,
                    transcription.Text ?? string.Empty,
                    request.SystemPrompt);

                var llmRequest = new ChatCompletionRequest
                {
                    Model = "gpt-4o-mini", // Default model, will be routed by ILLMRouter
                    Messages = messages,
                    Temperature = request.Temperature,
                    MaxTokens = request.MaxTokens,
                    Stream = true // Enable streaming
                };

                await foreach (var chunk in _llmRouter.StreamChatCompletionAsync(llmRequest, cancellationToken: cancellationToken))
                {
                    var content = chunk.Choices?.FirstOrDefault()?.Delta?.Content;
                    if (!string.IsNullOrEmpty(content))
                    {
                        responseBuilder.Append(content);

                        // Queue text for TTS when we have a sentence
                        if (content.Contains('.') || content.Contains('!') || content.Contains('?'))
                        {
                            var sentences = ExtractCompleteSentences(responseBuilder);
                            foreach (var sentence in sentences)
                            {
                                ttsQueue.Enqueue(sentence);
                            }
                        }
                    }
                }

                // Process any remaining text
                var remainingText = responseBuilder.ToString();
                if (!string.IsNullOrWhiteSpace(remainingText))
                {
                    ttsQueue.Enqueue(remainingText);
                }

                // Update session history
                var fullResponse = responseBuilder.ToString();
                if (!string.IsNullOrEmpty(request.SessionId) && _sessions.TryGetValue(request.SessionId, out var session))
                {
                    session.AddTurn(transcription.Text ?? string.Empty, fullResponse);
                    session.LastActivity = DateTime.UtcNow;
                }

                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private List<HybridAudioChunk> GetTextChunks(StringBuilder responseBuilder, int startSequenceNumber)
        {
            var chunks = new List<HybridAudioChunk>();
            var text = responseBuilder.ToString();

            if (!string.IsNullOrEmpty(text))
            {
                // Split into smaller chunks for progressive display
                const int chunkSize = 100;
                for (int i = 0; i < text.Length; i += chunkSize)
                {
                    var chunkText = text.Substring(i, Math.Min(chunkSize, text.Length - i));
                    chunks.Add(new HybridAudioChunk
                    {
                        ChunkType = "text",
                        TextContent = chunkText,
                        SequenceNumber = startSequenceNumber + chunks.Count,
                        IsFinal = false
                    });
                }
            }

            return chunks;
        }

        private async Task<Exception?> ProcessTtsAsync(
            HybridAudioRequest request,
            Queue<string> ttsQueue,
            List<HybridAudioChunk> chunks,
            int startSequenceNumber,
            CancellationToken cancellationToken)
        {
            try
            {
                // Create a minimal TTS request for routing
                var ttsRoutingRequest = new TextToSpeechRequest
                {
                    Voice = request.VoiceId,
                    Input = "test" // Dummy text for routing only
                };
                var ttsClient = await _audioRouter.GetTextToSpeechClientAsync(
                    ttsRoutingRequest,
                    request.VirtualKey ?? string.Empty,
                    cancellationToken);

                if (ttsClient == null)
                    throw new InvalidOperationException("No TTS provider available");

                // Process TTS queue
                while (ttsQueue.Count > 0)
                {
                    var textToSpeak = ttsQueue.Dequeue();

                    var ttsRequest = new TextToSpeechRequest
                    {
                        Input = textToSpeak,
                        Voice = request.VoiceId ?? "alloy",
                        ResponseFormat = request.OutputFormat == "mp3" ? AudioFormat.Mp3 :
                                       request.OutputFormat == "wav" ? AudioFormat.Wav :
                                       request.OutputFormat == "flac" ? AudioFormat.Flac :
                                       request.OutputFormat == "ogg" ? AudioFormat.Ogg :
                                       AudioFormat.Mp3
                    };

                    // Check if provider supports streaming TTS
                    var supportedFormats = await ttsClient.GetSupportedFormatsAsync(cancellationToken);
                    if (supportedFormats.Contains("stream"))
                    {
                        // Stream TTS chunks
                        await foreach (var audioChunk in ttsClient.StreamSpeechAsync(ttsRequest, cancellationToken: cancellationToken))
                        {
                            chunks.Add(new HybridAudioChunk
                            {
                                ChunkType = "audio",
                                AudioData = audioChunk.Data,
                                SequenceNumber = startSequenceNumber + chunks.Count,
                                IsFinal = false
                            });
                        }
                    }
                    else
                    {
                        // Fall back to non-streaming TTS
                        var ttsResponse = await ttsClient.CreateSpeechAsync(
                            ttsRequest,
                            cancellationToken: cancellationToken);

                        chunks.Add(new HybridAudioChunk
                        {
                            ChunkType = "audio",
                            AudioData = ttsResponse.AudioData,
                            SequenceNumber = startSequenceNumber + chunks.Count,
                            IsFinal = false
                        });
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }
}

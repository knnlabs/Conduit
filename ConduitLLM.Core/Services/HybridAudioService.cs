using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Implements hybrid audio conversation by chaining STT, LLM, and TTS services.
    /// </summary>
    /// <remarks>
    /// This service provides conversational AI capabilities for providers that don't have
    /// native real-time audio support, by orchestrating a pipeline of separate services.
    /// </remarks>
    public partial class HybridAudioService : IHybridAudioService
    {
        private readonly ILLMRouter _llmRouter;
        private readonly ISimpleAudioRouter _audioRouter;
        private readonly ILogger<HybridAudioService> _logger;
        private readonly ICostCalculationService _costService;
        private readonly IContextManager _contextManager;
        private readonly IAudioProcessingService _audioProcessingService;

        // Session management
        private readonly ConcurrentDictionary<string, HybridSession> _sessions = new();
        private readonly Timer _sessionCleanupTimer;

        // Latency tracking
        private readonly Queue<ProcessingMetrics> _recentMetrics = new();
        private readonly object _metricsLock = new();
        private const int MaxMetricsSamples = 100;

        /// <summary>
        /// Initializes a new instance of the <see cref="HybridAudioService"/> class.
        /// </summary>
        /// <param name="llmRouter">The LLM router for text generation.</param>
        /// <param name="audioRouter">The audio router for STT and TTS.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="costService">The cost calculation service.</param>
        /// <param name="contextManager">The context manager for conversation history.</param>
        /// <param name="audioProcessingService">The audio processing service.</param>
        public HybridAudioService(
            ILLMRouter llmRouter,
            ISimpleAudioRouter audioRouter,
            ILogger<HybridAudioService> logger,
            ICostCalculationService costService,
            IContextManager contextManager,
            IAudioProcessingService audioProcessingService)
        {
            _llmRouter = llmRouter ?? throw new ArgumentNullException(nameof(llmRouter));
            _audioRouter = audioRouter ?? throw new ArgumentNullException(nameof(audioRouter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _costService = costService ?? throw new ArgumentNullException(nameof(costService));
            _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
            _audioProcessingService = audioProcessingService ?? throw new ArgumentNullException(nameof(audioProcessingService));

            // Start session cleanup timer
            _sessionCleanupTimer = new Timer(
                CleanupExpiredSessions,
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5));
        }

        /// <inheritdoc />
        public async Task<HybridAudioResponse> ProcessAudioAsync(
            HybridAudioRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

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
                var transcriptionClient = await _audioRouter.GetTranscriptionClientAsync(
                    request.Language,
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
                var ttsClient = await _audioRouter.GetTextToSpeechClientAsync(
                    request.VoiceId,
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

        // StreamProcessAudioAsync is implemented in HybridAudioServiceStreaming.cs

        /// <inheritdoc />
        public Task<string> CreateSessionAsync(
            HybridSessionConfig config,
            CancellationToken cancellationToken = default)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var sessionId = Guid.NewGuid().ToString();
            var session = new HybridSession
            {
                Id = sessionId,
                Config = config,
                CreatedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow
            };

            _sessions[sessionId] = session;
            _logger.LogInformation("Created hybrid audio session: {SessionId}", sessionId);

            return Task.FromResult(sessionId);
        }

        /// <inheritdoc />
        public Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentNullException(nameof(sessionId));

            if (_sessions.TryRemove(sessionId, out var session))
            {
                _logger.LogInformation("Closed hybrid audio session: {SessionId}", sessionId);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Check STT availability
                var sttClient = await _audioRouter.GetTranscriptionClientAsync(null, cancellationToken);
                if (sttClient == null || !await sttClient.SupportsTranscriptionAsync(cancellationToken: cancellationToken))
                    return false;

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
                catch
                {
                    return false;
                }

                // Check TTS availability
                var ttsClient = await _audioRouter.GetTextToSpeechClientAsync(null, cancellationToken);
                if (ttsClient == null || !await ttsClient.SupportsTextToSpeechAsync(cancellationToken: cancellationToken))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking hybrid audio availability");
                return false;
            }
        }

        /// <inheritdoc />
        public Task<HybridLatencyMetrics> GetLatencyMetricsAsync(CancellationToken cancellationToken = default)
        {
            lock (_metricsLock)
            {
                if (_recentMetrics.Count == 0)
                {
                    return Task.FromResult(new HybridLatencyMetrics
                    {
                        SampleCount = 0,
                        CalculatedAt = DateTime.UtcNow
                    });
                }

                var metrics = _recentMetrics.ToList();
                var totalLatencies = metrics.Select(m => m.TotalLatencyMs).OrderBy(l => l).ToList();

                return Task.FromResult(new HybridLatencyMetrics
                {
                    AverageSttLatencyMs = metrics.Average(m => m.SttLatencyMs),
                    AverageLlmLatencyMs = metrics.Average(m => m.LlmLatencyMs),
                    AverageTtsLatencyMs = metrics.Average(m => m.TtsLatencyMs),
                    AverageTotalLatencyMs = metrics.Average(m => m.TotalLatencyMs),
                    P95LatencyMs = GetPercentile(totalLatencies, 0.95),
                    P99LatencyMs = GetPercentile(totalLatencies, 0.99),
                    SampleCount = metrics.Count,
                    CalculatedAt = DateTime.UtcNow
                });
            }
        }

        private Task<List<Message>> BuildMessagesAsync(
            string? sessionId,
            string userInput,
            string? systemPrompt)
        {
            var messages = new List<Message>();

            // Add system prompt
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new Message
                {
                    Role = "system",
                    Content = systemPrompt
                });
            }
            else if (!string.IsNullOrEmpty(sessionId) && _sessions.TryGetValue(sessionId, out var session))
            {
                // Use session's system prompt
                if (!string.IsNullOrEmpty(session.Config.SystemPrompt))
                {
                    messages.Add(new Message
                    {
                        Role = "system",
                        Content = session.Config.SystemPrompt
                    });
                }

                // Add conversation history
                foreach (var turn in session.GetRecentTurns())
                {
                    messages.Add(new Message { Role = "user", Content = turn.UserInput });
                    messages.Add(new Message { Role = "assistant", Content = turn.AssistantResponse });
                }
            }

            // Add current user input
            messages.Add(new Message
            {
                Role = "user",
                Content = userInput
            });

            return Task.FromResult(messages);
        }

        private List<string> ExtractCompleteSentences(StringBuilder text)
        {
            var sentences = new List<string>();
            var currentText = text.ToString();
            var lastSentenceEnd = -1;

            for (int i = 0; i < currentText.Length; i++)
            {
                if (currentText[i] == '.' || currentText[i] == '!' || currentText[i] == '?')
                {
                    // Check if it's really the end of a sentence (not an abbreviation)
                    if (i + 1 < currentText.Length && char.IsWhiteSpace(currentText[i + 1]))
                    {
                        var sentence = currentText.Substring(lastSentenceEnd + 1, i - lastSentenceEnd).Trim();
                        if (!string.IsNullOrWhiteSpace(sentence))
                        {
                            sentences.Add(sentence);
                        }
                        lastSentenceEnd = i;
                    }
                }
            }

            // Remove extracted sentences from the builder
            if (lastSentenceEnd >= 0)
            {
                text.Remove(0, lastSentenceEnd + 1);
            }

            return sentences;
        }

        private void RecordMetrics(ProcessingMetrics metrics)
        {
            lock (_metricsLock)
            {
                _recentMetrics.Enqueue(metrics);
                while (_recentMetrics.Count > MaxMetricsSamples)
                {
                    _recentMetrics.Dequeue();
                }
            }
        }

        private double GetPercentile(List<double> sortedValues, double percentile)
        {
            if (sortedValues.Count == 0)
                return 0;

            var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
            return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
        }

        private void CleanupExpiredSessions(object? state)
        {
            var now = DateTime.UtcNow;
            var expiredSessions = _sessions
                .Where(kvp => now - kvp.Value.LastActivity > kvp.Value.Config.SessionTimeout)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var sessionId in expiredSessions)
            {
                if (_sessions.TryRemove(sessionId, out _))
                {
                    _logger.LogDebug("Cleaned up expired session: {SessionId}", sessionId);
                }
            }
        }

        /// <summary>
        /// Disposes of the service and cleans up resources.
        /// </summary>
        public void Dispose()
        {
            _sessionCleanupTimer?.Dispose();
            _sessions.Clear();
        }

        /// <summary>
        /// Represents a hybrid audio conversation session.
        /// </summary>
        private class HybridSession
        {
            public string Id { get; set; } = string.Empty;
            public HybridSessionConfig Config { get; set; } = new();
            public DateTime CreatedAt { get; set; }
            public DateTime LastActivity { get; set; }
            private readonly Queue<ConversationTurn> _history = new();

            public void AddTurn(string userInput, string assistantResponse)
            {
                _history.Enqueue(new ConversationTurn
                {
                    UserInput = userInput,
                    AssistantResponse = assistantResponse,
                    Timestamp = DateTime.UtcNow
                });

                // Maintain history limit
                while (_history.Count > Config.MaxHistoryTurns)
                {
                    _history.Dequeue();
                }
            }

            public IEnumerable<ConversationTurn> GetRecentTurns()
            {
                return _history.ToList();
            }
        }

        /// <summary>
        /// Represents a single turn in a conversation.
        /// </summary>
        private class ConversationTurn
        {
            public string UserInput { get; set; } = string.Empty;
            public string AssistantResponse { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
        }
    }
}

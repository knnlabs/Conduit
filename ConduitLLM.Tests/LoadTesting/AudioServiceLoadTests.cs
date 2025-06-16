using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Core.Services;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.LoadTesting
{
    /// <summary>
    /// Load tests for audio services.
    /// </summary>
    public class AudioServiceLoadTests : AudioLoadTestBase, IDisposable
    {
        private readonly List<byte[]> _testAudioSamples;
        private readonly List<string> _testTextSamples;
        private readonly Random _random = new();

        public AudioServiceLoadTests(ITestOutputHelper output) : base(output)
        {
            // Generate test data
            _testAudioSamples = GenerateTestAudioSamples(10);
            _testTextSamples = GenerateTestTextSamples(50);
        }

        protected override void ConfigureServices(IServiceCollection services)
        {
            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Warning);
            });

            // Add core services
            services.AddMemoryCache();
            services.AddHttpClient();
            
            // Add mock cache service
            var mockCacheService = new Mock<ICacheService>();
            mockCacheService.Setup(x => x.Get<It.IsAnyType>(It.IsAny<string>()))
                .Returns((object?)null);
            mockCacheService.Setup(x => x.Set(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>()))
                .Returns(true);
            services.AddSingleton<ICacheService>(mockCacheService.Object);
            
            // Add mock audio clients
            services.AddSingleton<IAudioTranscriptionClient>(CreateMockTranscriptionClient());
            services.AddSingleton<ITextToSpeechClient>(CreateMockTtsClient());
            services.AddSingleton<IRealtimeAudioClient>(CreateMockRealtimeClient());
            services.AddSingleton<ILLMClient>(CreateMockLLMClient());

            // Add audio services
            services.AddScoped<IAudioRouter, DefaultAudioRouter>();
            services.AddScoped<IAudioProcessingService, AudioProcessingService>();
            services.AddScoped<IHybridAudioService, HybridAudioService>();
            
            // Add performance services
            services.AddSingleton<IAudioConnectionPool, AudioConnectionPool>();
            services.AddScoped<IAudioStreamCache, AudioStreamCache>();
            services.AddScoped<IAudioCdnService, AudioCdnService>();
            services.AddScoped<PerformanceOptimizedAudioService>();

            // Add monitoring services
            services.AddSingleton<IAudioMetricsCollector, AudioMetricsCollector>();
            services.AddSingleton<IAudioAlertingService, AudioAlertingService>();
            services.AddSingleton<IAudioTracingService, AudioTracingService>();
            services.AddScoped<MonitoringAudioService>();

            // Configure options
            services.Configure<AudioConnectionPoolOptions>(options =>
            {
                options.MaxConnectionsPerProvider = 50;
                options.ConnectionTimeout = 30;
                options.MaxIdleTime = TimeSpan.FromMinutes(15);
                options.MaxConnectionAge = TimeSpan.FromHours(1);
            });

            services.Configure<AudioCacheOptions>(options =>
            {
                options.DefaultTranscriptionTtl = TimeSpan.FromMinutes(5);
                options.DefaultTtsTtl = TimeSpan.FromMinutes(10);
                options.MemoryCacheTtl = TimeSpan.FromMinutes(1);
                options.MaxMemoryCacheSizeBytes = 100 * 1024 * 1024;
            });

            services.Configure<AudioMetricsOptions>(options =>
            {
                options.AggregationInterval = TimeSpan.FromSeconds(10);
                options.EnableDetailedMetrics = true;
                options.MaxMetricAge = TimeSpan.FromHours(1);
            });
            
            services.Configure<AudioCdnOptions>(options =>
            {
                options.CdnBaseUrl = "https://cdn.example.com";
                options.MaxConcurrentUploads = 10;
            });
            
            services.Configure<AudioAlertingOptions>(options =>
            {
                options.EnableAlerts = true;
                options.AlertEvaluationInterval = TimeSpan.FromMinutes(1);
            });
        }

        [Fact]
        public async Task BasicLoadTest()
        {
            var config = new LoadTestConfig
            {
                TestName = "Basic Audio Load Test",
                Duration = TimeSpan.FromSeconds(30),
                ConcurrentUsers = 10,
                ThinkTimeMs = 500,
                OperationWeights = new()
                {
                    [AudioOperationType.Transcription] = 40,
                    [AudioOperationType.TextToSpeech] = 40,
                    [AudioOperationType.RealtimeSession] = 10,
                    [AudioOperationType.HybridConversation] = 10
                }
            };

            var result = await RunLoadTestAsync(config);

            // Assertions
            Assert.True(result.TotalOperations > 0, "Should have completed some operations");
            Assert.True(result.ErrorRate < 0.05, $"Error rate {result.ErrorRate:P1} should be less than 5%");
            Assert.True(result.Throughput > 5, $"Throughput {result.Throughput:F1} ops/sec should be greater than 5");
            
            // Check latencies
            foreach (var metrics in result.OperationMetrics.Values.Where(m => m.TotalCount > 0))
            {
                Assert.True(metrics.P95LatencyMs < 1000, $"P95 latency {metrics.P95LatencyMs:F1}ms should be less than 1000ms");
            }
        }

        [Fact]
        public async Task HighConcurrencyTest()
        {
            var config = new LoadTestConfig
            {
                TestName = "High Concurrency Test",
                Duration = TimeSpan.FromSeconds(20),
                ConcurrentUsers = 50,
                ThinkTimeMs = 100,
                OperationWeights = new()
                {
                    [AudioOperationType.Transcription] = 50,
                    [AudioOperationType.TextToSpeech] = 50,
                    [AudioOperationType.RealtimeSession] = 0,
                    [AudioOperationType.HybridConversation] = 0
                }
            };

            var result = await RunLoadTestAsync(config);

            Assert.True(result.ErrorRate < 0.10, $"Error rate {result.ErrorRate:P1} should be less than 10% under high load");
            Assert.True(result.Throughput > 20, $"Throughput {result.Throughput:F1} ops/sec should be greater than 20");
        }

        [Fact]
        public async Task SustainedLoadTest()
        {
            var config = new LoadTestConfig
            {
                TestName = "Sustained Load Test",
                Duration = TimeSpan.FromMinutes(2),
                ConcurrentUsers = 20,
                ThinkTimeMs = 1000,
                OperationWeights = new()
                {
                    [AudioOperationType.Transcription] = 30,
                    [AudioOperationType.TextToSpeech] = 30,
                    [AudioOperationType.RealtimeSession] = 20,
                    [AudioOperationType.HybridConversation] = 20
                }
            };

            var result = await RunLoadTestAsync(config);

            // Should maintain performance over time
            Assert.True(result.ErrorRate < 0.02, $"Error rate {result.ErrorRate:P1} should be less than 2% for sustained load");
            
            // Check for memory leaks or degradation
            var metricsCollector = _serviceProvider.GetRequiredService<IAudioMetricsCollector>();
            var snapshot = await metricsCollector.GetCurrentSnapshotAsync();
            Assert.True(snapshot.Resources.MemoryUsageMb < 500, "Memory usage should be reasonable");
        }

        [Fact]
        public async Task CacheEffectivenessTest()
        {
            // Use limited set of inputs to test caching
            var config = new LoadTestConfig
            {
                TestName = "Cache Effectiveness Test",
                Duration = TimeSpan.FromSeconds(30),
                ConcurrentUsers = 15,
                ThinkTimeMs = 200,
                OperationWeights = new()
                {
                    [AudioOperationType.Transcription] = 50,
                    [AudioOperationType.TextToSpeech] = 50,
                    [AudioOperationType.RealtimeSession] = 0,
                    [AudioOperationType.HybridConversation] = 0
                }
            };

            // Limit test data to increase cache hits
            _testAudioSamples.RemoveRange(5, _testAudioSamples.Count - 5);
            _testTextSamples.RemoveRange(10, _testTextSamples.Count - 10);

            var result = await RunLoadTestAsync(config);

            // With caching, latencies should be lower
            var transcriptionMetrics = result.OperationMetrics[AudioOperationType.Transcription];
            var ttsMetrics = result.OperationMetrics[AudioOperationType.TextToSpeech];

            Assert.True(transcriptionMetrics.P50LatencyMs < 50, "Cached transcriptions should be fast");
            Assert.True(ttsMetrics.P50LatencyMs < 50, "Cached TTS should be fast");
        }

        [Fact]
        public async Task FailoverTest()
        {
            var config = new LoadTestConfig
            {
                TestName = "Failover Test",
                Duration = TimeSpan.FromSeconds(30),
                ConcurrentUsers = 10,
                ThinkTimeMs = 500
            };

            // Simulate provider failures during test
            _ = Task.Run(async () =>
            {
                await Task.Delay(10000); // Wait 10 seconds
                SimulateProviderFailure("openai");
                await Task.Delay(10000); // Wait 10 more seconds
                RestoreProvider("openai");
            });

            var result = await RunLoadTestAsync(config);

            // Should handle failover gracefully
            Assert.True(result.ErrorRate < 0.15, $"Error rate {result.ErrorRate:P1} should be less than 15% even with failover");
        }

        protected override async Task ExecuteTranscriptionAsync(int userId, CancellationToken cancellationToken)
        {
            var audioService = _serviceProvider.GetRequiredService<MonitoringAudioService>();
            var audioData = _testAudioSamples[_random.Next(_testAudioSamples.Count)];
            
            var request = new AudioTranscriptionRequest
            {
                AudioData = audioData,
                Language = "en",
                ResponseFormat = TranscriptionFormat.Json
            };

            await audioService.TranscribeAudioAsync(request, $"test-key-{userId}", cancellationToken);
        }

        protected override async Task ExecuteTextToSpeechAsync(int userId, CancellationToken cancellationToken)
        {
            var audioService = _serviceProvider.GetRequiredService<MonitoringAudioService>();
            var text = _testTextSamples[_random.Next(_testTextSamples.Count)];
            
            var request = new TextToSpeechRequest
            {
                Input = text,
                Voice = "alloy",
                Model = "tts-1",
                ResponseFormat = AudioFormat.Mp3
            };

            await audioService.CreateSpeechAsync(request, $"test-key-{userId}", cancellationToken);
        }

        protected override async Task ExecuteRealtimeSessionAsync(int userId, CancellationToken cancellationToken)
        {
            var audioService = _serviceProvider.GetRequiredService<MonitoringAudioService>();
            
            var config = new RealtimeSessionConfig
            {
                Model = "gpt-4-realtime",
                Voice = "alloy",
                SystemPrompt = "You are a helpful assistant.",
                InputAudioFormat = RealtimeAudioFormat.Pcm16,
                OutputAudioFormat = RealtimeAudioFormat.Pcm16
            };

            var session = await audioService.CreateSessionAsync(config, $"test-key-{userId}", cancellationToken);
            
            try
            {
                var stream = audioService.StreamAudioAsync(session, cancellationToken);
                
                // Simulate conversation
                for (int i = 0; i < 3; i++)
                {
                    var frame = new RealtimeAudioFrame
                    {
                        AudioData = _testAudioSamples[0],
                        SampleRate = 24000,
                        Channels = 1,
                        DurationMs = 1000
                    };
                    await stream.SendAsync(frame, cancellationToken);
                    await Task.Delay(100, cancellationToken);
                    
                    // Receive response
                    await Task.Delay(500, cancellationToken);
                }
                
                await stream.CompleteAsync();
            }
            finally
            {
                await audioService.CloseSessionAsync(session, cancellationToken);
            }
        }

        protected override async Task ExecuteHybridConversationAsync(int userId, CancellationToken cancellationToken)
        {
            var hybridService = _serviceProvider.GetRequiredService<IHybridAudioService>();
            var audioData = _testAudioSamples[_random.Next(_testAudioSamples.Count)];
            
            var request = new HybridAudioRequest
            {
                AudioData = audioData,
                AudioFormat = AudioFormat.Mp3,
                Language = "en",
                SystemPrompt = "You are a helpful assistant.",
                Voice = "alloy",
                Temperature = 0.7,
                EnableStreaming = true
            };

            var response = await hybridService.ProcessAudioAsync(request, $"test-key-{userId}", cancellationToken);
        }

        private List<byte[]> GenerateTestAudioSamples(int count)
        {
            var samples = new List<byte[]>();
            for (int i = 0; i < count; i++)
            {
                // Generate dummy audio data (would be real audio in production)
                var size = 10000 + _random.Next(50000);
                var data = new byte[size];
                _random.NextBytes(data);
                samples.Add(data);
            }
            return samples;
        }

        private List<string> GenerateTestTextSamples(int count)
        {
            var samples = new List<string>
            {
                "Hello, how can I help you today?",
                "What's the weather like?",
                "Can you explain quantum computing?",
                "Tell me a joke",
                "What are the latest news headlines?",
                "How do I cook pasta?",
                "What's the meaning of life?",
                "Can you help me with my homework?",
                "What time is it?",
                "Tell me about artificial intelligence"
            };

            // Duplicate and vary samples to reach desired count
            while (samples.Count < count)
            {
                var baseSample = samples[_random.Next(Math.Min(10, samples.Count))];
                samples.Add($"{baseSample} (variation {samples.Count})");
            }

            return samples.Take(count).ToList();
        }

        private void SimulateProviderFailure(string provider)
        {
            _logger.LogWarning("Simulating failure for provider: {Provider}", provider);
            // In real implementation, this would affect the mock clients
        }

        private void RestoreProvider(string provider)
        {
            _logger.LogInformation("Restoring provider: {Provider}", provider);
            // In real implementation, this would restore the mock clients
        }

        private IAudioTranscriptionClient CreateMockTranscriptionClient()
        {
            var mock = new Mock<IAudioTranscriptionClient>();
            
            mock.Setup(x => x.TranscribeAudioAsync(
                    It.IsAny<AudioTranscriptionRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (AudioTranscriptionRequest request, string apiKey, CancellationToken ct) =>
                {
                    // Simulate processing time
                    await Task.Delay(_random.Next(50, 200), ct);
                    
                    return new AudioTranscriptionResponse
                    {
                        Text = "This is a transcribed text from the audio.",
                        Language = request.Language ?? "en",
                        Duration = _random.Next(5, 30),
                        Segments = new List<TranscriptionSegment>()
                    };
                });

            mock.Setup(x => x.SupportsTranscriptionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
                
            mock.Setup(x => x.GetSupportedFormatsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "mp3", "mp4", "wav", "m4a", "webm" });
                
            mock.Setup(x => x.GetSupportedLanguagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "en", "es", "fr", "de", "it", "pt", "zh", "ja" });

            return mock.Object;
        }

        private ITextToSpeechClient CreateMockTtsClient()
        {
            var mock = new Mock<ITextToSpeechClient>();
            
            mock.Setup(x => x.CreateSpeechAsync(
                    It.IsAny<TextToSpeechRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (TextToSpeechRequest request, string apiKey, CancellationToken ct) =>
                {
                    // Simulate processing time
                    await Task.Delay(_random.Next(50, 300), ct);
                    
                    var audioSize = request.Input.Length * 100;
                    return new TextToSpeechResponse
                    {
                        AudioData = new byte[audioSize],
                        Format = request.ResponseFormat?.ToString() ?? "mp3",
                        Duration = request.Input.Length * 0.1
                    };
                });

            mock.Setup(x => x.ListVoicesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<VoiceInfo>
                {
                    new() { VoiceId = "alloy", Name = "Alloy", Provider = "openai" },
                    new() { VoiceId = "echo", Name = "Echo", Provider = "openai" }
                });
            
            mock.Setup(x => x.GetSupportedFormatsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "mp3", "wav", "opus", "aac" });
            
            mock.Setup(x => x.SupportsTextToSpeechAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
                
            // Add StreamSpeechAsync mock
            mock.Setup(x => x.StreamSpeechAsync(
                    It.IsAny<TextToSpeechRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns((TextToSpeechRequest request, string apiKey, CancellationToken ct) =>
                {
                    async IAsyncEnumerable<AudioChunk> Generate()
                    {
                        var totalSize = request.Input.Length * 100;
                        var chunkSize = 1024;
                        var chunks = (int)Math.Ceiling((double)totalSize / chunkSize);
                        
                        for (int i = 0; i < chunks; i++)
                        {
                            if (ct.IsCancellationRequested) yield break;
                            
                            await Task.Delay(10, ct);
                            var remaining = Math.Min(chunkSize, totalSize - (i * chunkSize));
                            yield return new AudioChunk
                            {
                                Data = new byte[remaining],
                                ChunkIndex = i,
                                IsFinal = i == chunks - 1
                            };
                        }
                    }
                    return Generate();
                });

            return mock.Object;
        }

        private IRealtimeAudioClient CreateMockRealtimeClient()
        {
            var mock = new Mock<IRealtimeAudioClient>();
            
            mock.Setup(x => x.CreateSessionAsync(
                    It.IsAny<RealtimeSessionConfig>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (RealtimeSessionConfig config, string apiKey, CancellationToken ct) =>
                {
                    await Task.Delay(50, ct);
                    return new RealtimeSession
                    {
                        Id = Guid.NewGuid().ToString(),
                        Provider = "openai",
                        Config = config,
                        CreatedAt = DateTime.UtcNow,
                        State = SessionState.Connected
                    };
                });

            mock.Setup(x => x.SupportsRealtimeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
                
            mock.Setup(x => x.GetCapabilitiesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RealtimeCapabilities
                {
                    SupportedModels = new List<string> { "gpt-4-realtime" },
                    SupportedVoices = new List<string> { "alloy", "echo", "fable", "onyx", "nova", "shimmer" },
                    MaxSessionDuration = TimeSpan.FromHours(2),
                    SupportsTurnDetection = true,
                    SupportsInterruption = true,
                    SupportsFunctionCalling = true
                });
                
            mock.Setup(x => x.StreamAudioAsync(It.IsAny<RealtimeSession>(), It.IsAny<CancellationToken>()))
                .Returns((RealtimeSession session, CancellationToken ct) =>
                {
                    var mockStream = new Mock<IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse>>();
                    mockStream.Setup(s => s.IsConnected).Returns(true);
                    mockStream.Setup(s => s.SendAsync(It.IsAny<RealtimeAudioFrame>(), It.IsAny<CancellationToken>()))
                        .Returns(ValueTask.CompletedTask);
                    mockStream.Setup(s => s.CompleteAsync())
                        .Returns(ValueTask.CompletedTask);
                    mockStream.Setup(s => s.ReceiveAsync(It.IsAny<CancellationToken>()))
                        .Returns(AsyncEnumerable.Empty<RealtimeResponse>());
                    return mockStream.Object;
                });
                
            mock.Setup(x => x.UpdateSessionAsync(
                    It.IsAny<RealtimeSession>(),
                    It.IsAny<RealtimeSessionUpdate>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
                
            mock.Setup(x => x.CloseSessionAsync(It.IsAny<RealtimeSession>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            return mock.Object;
        }

        private ILLMClient CreateMockLLMClient()
        {
            var mock = new Mock<ILLMClient>();
            
            mock.Setup(x => x.CompleteAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<Message>>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<double>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (string model, List<Message> messages, string apiKey, int maxTokens, double temp, string vk, CancellationToken ct) =>
                {
                    await Task.Delay(_random.Next(100, 500), ct);
                    return new ChatCompletionResponse
                    {
                        Id = Guid.NewGuid().ToString(),
                        Choices = new List<Choice>
                        {
                            new()
                            {
                                Message = new Message
                                {
                                    Role = "assistant",
                                    Content = "This is a response from the LLM."
                                },
                                FinishReason = "stop"
                            }
                        }
                    };
                });

            return mock.Object;
        }
    }
}
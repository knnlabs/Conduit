using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.LoadTesting
{
    /// <summary>
    /// Performance benchmarks for audio services using BenchmarkDotNet.
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net80)]
    [RPlotExporter]
    public class AudioPerformanceBenchmarks
    {
        private IServiceProvider _serviceProvider = null!;
        private byte[] _smallAudioData = null!;
        private byte[] _mediumAudioData = null!;
        private byte[] _largeAudioData = null!;
        private string _shortText = null!;
        private string _mediumText = null!;
        private string _longText = null!;

        [GlobalSetup]
        public void Setup()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // Prepare test data
            _smallAudioData = new byte[10 * 1024]; // 10KB
            _mediumAudioData = new byte[100 * 1024]; // 100KB
            _largeAudioData = new byte[1024 * 1024]; // 1MB

            _shortText = "Hello world!";
            _mediumText = string.Join(" ", Enumerable.Repeat("This is a test sentence.", 20));
            _longText = string.Join(" ", Enumerable.Repeat("This is a longer test paragraph with more content.", 100));

            // Warm up services
            WarmUpServices().GetAwaiter().GetResult();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            (_serviceProvider as IDisposable)?.Dispose();
        }

        [Benchmark]
        public async Task TranscribeSmallAudio()
        {
            var service = _serviceProvider.GetRequiredService<IAudioTranscriptionClient>();
            var request = new AudioTranscriptionRequest
            {
                AudioData = _smallAudioData,
                Language = "en"
            };

            await service.TranscribeAudioAsync(request);
        }

        [Benchmark]
        public async Task TranscribeMediumAudio()
        {
            var service = _serviceProvider.GetRequiredService<IAudioTranscriptionClient>();
            var request = new AudioTranscriptionRequest
            {
                AudioData = _mediumAudioData,
                Language = "en"
            };

            await service.TranscribeAudioAsync(request);
        }

        [Benchmark]
        public async Task TranscribeLargeAudio()
        {
            var service = _serviceProvider.GetRequiredService<IAudioTranscriptionClient>();
            var request = new AudioTranscriptionRequest
            {
                AudioData = _largeAudioData,
                Language = "en"
            };

            await service.TranscribeAudioAsync(request);
        }

        [Benchmark]
        public async Task TextToSpeechShort()
        {
            var service = _serviceProvider.GetRequiredService<ITextToSpeechClient>();
            var request = new TextToSpeechRequest
            {
                Input = _shortText,
                Voice = "alloy",
                Model = "tts-1"
            };

            await service.CreateSpeechAsync(request);
        }

        [Benchmark]
        public async Task TextToSpeechMedium()
        {
            var service = _serviceProvider.GetRequiredService<ITextToSpeechClient>();
            var request = new TextToSpeechRequest
            {
                Input = _mediumText,
                Voice = "alloy",
                Model = "tts-1"
            };

            await service.CreateSpeechAsync(request);
        }

        [Benchmark]
        public async Task TextToSpeechLong()
        {
            var service = _serviceProvider.GetRequiredService<ITextToSpeechClient>();
            var request = new TextToSpeechRequest
            {
                Input = _longText,
                Voice = "alloy",
                Model = "tts-1"
            };

            await service.CreateSpeechAsync(request);
        }

        [Benchmark]
        public async Task CachedTranscription()
        {
            var service = _serviceProvider.GetRequiredService<MonitoringAudioService>();
            var request = new AudioTranscriptionRequest
            {
                AudioData = _smallAudioData, // Same data for cache hits
                Language = "en"
            };

            // First call populates cache
            await service.TranscribeAudioAsync(request);
            
            // Second call should hit cache
            await service.TranscribeAudioAsync(request);
        }

        [Benchmark]
        public async Task AudioRouting()
        {
            var router = _serviceProvider.GetRequiredService<IAudioRouter>();
            var request = new AudioTranscriptionRequest
            {
                AudioData = _mediumAudioData,
                Language = "en",
                ResponseFormat = TranscriptionFormat.Text
            };

            var client = await router.GetTranscriptionClientAsync(request, "benchmark-key");
        }

        [Benchmark]
        public async Task HybridConversation()
        {
            var service = _serviceProvider.GetRequiredService<IHybridAudioService>();
            var request = new HybridAudioRequest
            {
                AudioData = _smallAudioData,
                AudioFormat = "mp3",
                Language = "en",
                SystemPrompt = "You are a helpful assistant.",
                VoiceId = "alloy",
                EnableStreaming = true
            };

            var response = await service.ProcessAudioAsync(request, "benchmark-key");
        }

        [Benchmark]
        public async Task MetricsCollection()
        {
            var metrics = _serviceProvider.GetRequiredService<IAudioMetricsCollector>();
            
            var transcriptionMetric = new TranscriptionMetric
            {
                Provider = "openai",
                VirtualKey = "benchmark",
                Success = true,
                DurationMs = 150,
                AudioDurationSeconds = 5,
                FileSizeBytes = _mediumAudioData.Length,
                WordCount = 50
            };

            await metrics.RecordTranscriptionMetricAsync(transcriptionMetric);
            
            var snapshot = await metrics.GetCurrentSnapshotAsync();
        }

        [Benchmark]
        public async Task ConnectionPoolAcquisition()
        {
            var pool = _serviceProvider.GetRequiredService<IAudioConnectionPool>();
            
            using var connection = await pool.GetConnectionAsync("openai");
            // Simulate some work
            await Task.Delay(1);
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Add logging (minimal for benchmarks)
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Warning);
            });

            // Add core services
            services.AddMemoryCache();
            services.AddHttpClient();
            
            // Add mock clients for consistent benchmarking
            services.AddSingleton<IAudioTranscriptionClient>(CreateMockTranscriptionClient());
            services.AddSingleton<ITextToSpeechClient>(CreateMockTtsClient());
            services.AddSingleton<IRealtimeAudioClient>(CreateMockRealtimeClient());
            services.AddSingleton<ILLMClient>(CreateMockLLMClient());

            // Add all audio services
            services.AddScoped<IAudioRouter, AdvancedAudioRouter>();
            services.AddScoped<IAdvancedAudioRouter, AdvancedAudioRouter>();
            services.AddScoped<ISimpleAudioRouter, BasicAudioRouter>();
            services.AddScoped<IAudioProcessingService, AudioProcessingService>();
            services.AddScoped<IHybridAudioService, HybridAudioService>();
            services.AddSingleton<IAudioConnectionPool, AudioConnectionPool>();
            services.AddScoped<IAudioStreamCache, AudioStreamCache>();
            services.AddScoped<IAudioCdnService, AudioCdnService>();
            services.AddScoped<PerformanceOptimizedAudioService>();
            services.AddSingleton<IAudioMetricsCollector, AudioMetricsCollector>();
            services.AddSingleton<IAudioAlertingService, AudioAlertingService>();
            services.AddSingleton<IAudioTracingService, AudioTracingService>();
            services.AddScoped<MonitoringAudioService>();

            // Configure options for performance
            services.Configure<AudioConnectionPoolOptions>(options =>
            {
                options.MaxConnectionsPerProvider = 100;
                options.EnableHealthChecks = false; // Disable for benchmarks
            });

            services.Configure<AudioCacheOptions>(options =>
            {
                options.EnableCaching = true;
                options.MaxCacheSizeMb = 100;
            });

            services.Configure<AudioTracingOptions>(options =>
            {
                options.SamplingRate = 0; // Disable tracing for benchmarks
            });
        }

        private async Task WarmUpServices()
        {
            // Warm up connection pool
            var pool = _serviceProvider.GetRequiredService<IAudioConnectionPool>();
            await pool.WarmUpAsync(new[] { "openai", "elevenlabs" });

            // Warm up caches
            var cache = _serviceProvider.GetRequiredService<IAudioStreamCache>();
            await cache.WarmUpAsync();

            // Do a few operations to JIT compile
            var transcriptionService = _serviceProvider.GetRequiredService<IAudioTranscriptionClient>();
            for (int i = 0; i < 3; i++)
            {
                await transcriptionService.TranscribeAudioAsync(new AudioTranscriptionRequest
                {
                    AudioData = _smallAudioData,
                    Language = "en"
                });
            }
        }

        private IAudioTranscriptionClient CreateMockTranscriptionClient()
        {
            var mock = new Mock<IAudioTranscriptionClient>();
            
            mock.Setup(x => x.TranscribeAudioAsync(
                    It.IsAny<AudioTranscriptionRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((AudioTranscriptionRequest request, string apiKey, CancellationToken ct) =>
                {
                    // Minimal delay for consistent benchmarking
                    return new AudioTranscriptionResponse
                    {
                        Text = "Transcribed text",
                        Language = request.Language ?? "en",
                        Duration = request.AudioData.Length / 16000.0
                    };
                });

            return mock.Object;
        }

        private ITextToSpeechClient CreateMockTtsClient()
        {
            var mock = new Mock<ITextToSpeechClient>();
            
            mock.Setup(x => x.CreateSpeechAsync(
                    It.IsAny<TextToSpeechRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((TextToSpeechRequest request, string apiKey, CancellationToken ct) =>
                {
                    return new TextToSpeechResponse
                    {
                        AudioData = new byte[request.Input.Length * 100],
                        Format = request.ResponseFormat ?? "mp3",
                        Duration = request.Input.Length * 0.1
                    };
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
                .ReturnsAsync(new RealtimeSession
                {
                    SessionId = Guid.NewGuid().ToString(),
                    Model = "gpt-4-realtime"
                });

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
                .ReturnsAsync(new ChatCompletionResponse
                {
                    Id = "test",
                    Choices = new List<Choice>
                    {
                        new()
                        {
                            Message = new Message
                            {
                                Role = "assistant",
                                Content = "Response"
                            }
                        }
                    }
                });

            return mock.Object;
        }
    }

    /// <summary>
    /// Test class to run benchmarks.
    /// </summary>
    public class AudioBenchmarkTests
    {
        private readonly ITestOutputHelper _output;

        public AudioBenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact(Skip = "Run manually - takes significant time")]
        public void RunPerformanceBenchmarks()
        {
            var summary = BenchmarkRunner.Run<AudioPerformanceBenchmarks>(
                ManualConfig
                    .Create(DefaultConfig.Instance)
                    .WithOptions(ConfigOptions.DisableOptimizationsValidator));

            _output.WriteLine("Benchmark completed. Check BenchmarkDotNet reports for details.");
        }

        [Fact]
        public async Task QuickPerformanceCheck()
        {
            var benchmark = new AudioPerformanceBenchmarks();
            benchmark.Setup();

            try
            {
                var stopwatch = Stopwatch.StartNew();
                
                // Run each benchmark once for quick check
                await benchmark.TranscribeSmallAudio();
                var transcribeSmallTime = stopwatch.ElapsedMilliseconds;
                
                stopwatch.Restart();
                await benchmark.TextToSpeechShort();
                var ttsShortTime = stopwatch.ElapsedMilliseconds;
                
                stopwatch.Restart();
                await benchmark.CachedTranscription();
                var cachedTime = stopwatch.ElapsedMilliseconds;
                
                stopwatch.Restart();
                await benchmark.AudioRouting();
                var routingTime = stopwatch.ElapsedMilliseconds;
                
                _output.WriteLine("Quick Performance Check:");
                _output.WriteLine($"Transcribe Small Audio: {transcribeSmallTime}ms");
                _output.WriteLine($"TTS Short Text: {ttsShortTime}ms");
                _output.WriteLine($"Cached Transcription: {cachedTime}ms");
                _output.WriteLine($"Audio Routing: {routingTime}ms");
                
                // Basic assertions
                Assert.True(transcribeSmallTime < 100, "Small audio transcription should be fast");
                Assert.True(ttsShortTime < 100, "Short TTS should be fast");
                Assert.True(cachedTime < 50, "Cached operations should be very fast");
                Assert.True(routingTime < 10, "Routing decisions should be very fast");
            }
            finally
            {
                benchmark.Cleanup();
            }
        }
    }
}
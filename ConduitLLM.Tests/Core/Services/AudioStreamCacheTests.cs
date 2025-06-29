using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Core.Services;
using ConduitLLM.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Core.Services
{
    [Trait("Category", "Unit")]
    [Trait("Phase", "2")]
    [Trait("Component", "Core")]
    public class AudioStreamCacheTests : TestBase
    {
        private readonly Mock<ILogger<AudioStreamCache>> _loggerMock;
        private readonly Mock<IMemoryCache> _memoryCacheMock;
        private readonly Mock<ICacheService> _distributedCacheMock;
        private readonly Mock<IOptions<AudioCacheOptions>> _optionsMock;
        private readonly AudioCacheOptions _options;
        private readonly AudioStreamCache _cache;
        private readonly Fixture _fixture;

        public AudioStreamCacheTests(ITestOutputHelper output) : base(output)
        {
            _loggerMock = CreateLogger<AudioStreamCache>();
            _memoryCacheMock = new Mock<IMemoryCache>().SetupWorkingCache();
            _distributedCacheMock = MockBuilders.BuildCacheService()
                .WithGetBehavior()
                .WithSetBehavior()
                .Build();
            
            _options = new AudioCacheOptions
            {
                DefaultTranscriptionTtl = TimeSpan.FromMinutes(30),
                DefaultTtsTtl = TimeSpan.FromMinutes(60),
                MemoryCacheTtl = TimeSpan.FromMinutes(5),
                MaxMemoryCacheSizeBytes = 1024 * 1024 * 100, // 100MB
                StreamingChunkSizeBytes = 64 * 1024 // 64KB
            };
            
            _optionsMock = new Mock<IOptions<AudioCacheOptions>>();
            _optionsMock.Setup(x => x.Value).Returns(_options);
            
            _cache = new AudioStreamCache(
                _loggerMock.Object,
                _memoryCacheMock.Object,
                _distributedCacheMock.Object,
                _optionsMock.Object);
                
            _fixture = new Fixture();
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new AudioStreamCache(null!, _memoryCacheMock.Object, _distributedCacheMock.Object, _optionsMock.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        }

        [Fact]
        public void Constructor_WithNullMemoryCache_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new AudioStreamCache(_loggerMock.Object, null!, _distributedCacheMock.Object, _optionsMock.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("memoryCache");
        }

        [Fact]
        public void Constructor_WithNullDistributedCache_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new AudioStreamCache(_loggerMock.Object, _memoryCacheMock.Object, null!, _optionsMock.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("distributedCache");
        }

        [Fact]
        public void Constructor_WithNullOptions_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new AudioStreamCache(_loggerMock.Object, _memoryCacheMock.Object, _distributedCacheMock.Object, null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("options");
        }

        [Fact]
        public void Constructor_WithNullOptionsValue_ThrowsArgumentNullException()
        {
            // Arrange
            var badOptionsMock = new Mock<IOptions<AudioCacheOptions>>();
            badOptionsMock.Setup(x => x.Value).Returns((AudioCacheOptions)null!);

            // Act & Assert
            var act = () => new AudioStreamCache(_loggerMock.Object, _memoryCacheMock.Object, _distributedCacheMock.Object, badOptionsMock.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("options");
        }

        [Fact]
        public async Task CacheTranscriptionAsync_StoresInBothCaches()
        {
            // Arrange
            var request = CreateTranscriptionRequest();
            var response = CreateTranscriptionResponse();
            var ttl = TimeSpan.FromMinutes(15);

            // Act
            await _cache.CacheTranscriptionAsync(request, response, ttl);

            // Assert
            _memoryCacheMock.Verify(x => x.CreateEntry(It.IsAny<object>()), Times.Once);
            _distributedCacheMock.Verify(x => x.Set(
                It.IsAny<string>(),
                It.Is<AudioTranscriptionResponse>(r => r == response),
                ttl,
                It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Fact]
        public async Task CacheTranscriptionAsync_UsesDefaultTtlWhenNotSpecified()
        {
            // Arrange
            var request = CreateTranscriptionRequest();
            var response = CreateTranscriptionResponse();

            // Act
            await _cache.CacheTranscriptionAsync(request, response);

            // Assert
            _memoryCacheMock.Verify(x => x.CreateEntry(It.IsAny<object>()), Times.Once);
            _distributedCacheMock.Verify(x => x.Set(
                It.IsAny<string>(),
                It.IsAny<AudioTranscriptionResponse>(),
                _options.DefaultTranscriptionTtl,
                It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Fact]
        public async Task CacheTranscriptionAsync_LogsDebugMessage()
        {
            // Arrange
            var request = CreateTranscriptionRequest();
            var response = CreateTranscriptionResponse();

            // Act
            await _cache.CacheTranscriptionAsync(request, response);

            // Assert
            _loggerMock.VerifyLog(LogLevel.Debug, "Cached transcription with key");
        }

        [Fact]
        public async Task GetCachedTranscriptionAsync_WithMemoryCacheHit_ReturnsFromMemory()
        {
            // Arrange
            var request = CreateTranscriptionRequest();
            var expectedResponse = CreateTranscriptionResponse();
            SetupMemoryCacheHit(expectedResponse);

            // Act
            var result = await _cache.GetCachedTranscriptionAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeSameAs(expectedResponse);
            _distributedCacheMock.Verify(x => x.Get<AudioTranscriptionResponse>(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetCachedTranscriptionAsync_WithMemoryCacheMissButDistributedHit_ReturnsFromDistributed()
        {
            // Arrange
            var request = CreateTranscriptionRequest();
            var expectedResponse = CreateTranscriptionResponse();
            SetupMemoryCacheMiss();
            SetupDistributedCacheHit(expectedResponse);

            // Act
            var result = await _cache.GetCachedTranscriptionAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedResponse);
            _memoryCacheMock.Verify(x => x.CreateEntry(It.IsAny<object>()), Times.Once); // Should populate memory cache
        }

        [Fact]
        public async Task GetCachedTranscriptionAsync_WithBothCacheMiss_ReturnsNull()
        {
            // Arrange
            var request = CreateTranscriptionRequest();
            SetupMemoryCacheMiss();
            SetupDistributedCacheMiss();

            // Act
            var result = await _cache.GetCachedTranscriptionAsync(request);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetCachedTranscriptionAsync_LogsAppropriateMessages()
        {
            // Arrange
            var request = CreateTranscriptionRequest();
            SetupMemoryCacheMiss();
            SetupDistributedCacheMiss();

            // Act
            await _cache.GetCachedTranscriptionAsync(request);

            // Assert
            _loggerMock.VerifyLog(LogLevel.Debug, "Transcription cache miss");
        }

        [Fact]
        public async Task CacheTtsAudioAsync_StoresInBothCaches()
        {
            // Arrange
            var request = CreateTtsRequest();
            var response = CreateTtsResponse();
            var ttl = TimeSpan.FromMinutes(45);

            // Act
            await _cache.CacheTtsAudioAsync(request, response, ttl);

            // Assert
            _memoryCacheMock.Verify(x => x.CreateEntry(It.IsAny<object>()), Times.Once);
            // The implementation stores TtsCacheEntry, not TextToSpeechResponse directly
            _distributedCacheMock.Verify(x => x.Set(
                It.IsAny<string>(),
                It.IsAny<object>(), // Use object since TtsCacheEntry is internal
                ttl,
                It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Fact]
        public async Task GetStatisticsAsync_ReturnsAccurateStatistics()
        {
            // Arrange
            // Set up some cache hits and misses
            var request1 = CreateTranscriptionRequest();
            var request2 = CreateTranscriptionRequest();
            var response = CreateTranscriptionResponse();
            
            SetupMemoryCacheHit(response);
            await _cache.GetCachedTranscriptionAsync(request1); // Hit
            
            SetupMemoryCacheMiss();
            SetupDistributedCacheMiss();
            await _cache.GetCachedTranscriptionAsync(request2); // Miss

            // Act
            var stats = await _cache.GetStatisticsAsync();

            // Assert
            stats.Should().NotBeNull();
            stats.TranscriptionHits.Should().Be(1);
            stats.TranscriptionMisses.Should().Be(1);
            stats.TranscriptionHitRate.Should().Be(0.5);
        }

        [Fact]
        public async Task StreamCachedAudioAsync_YieldsAudioChunks()
        {
            // Arrange
            // First cache the audio using the public API to ensure proper type is stored
            var request = CreateTtsRequest();
            var response = new TextToSpeechResponse 
            { 
                AudioData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
                Duration = 1.0,
                Format = "mp3"
            };
            
            await _cache.CacheTtsAudioAsync(request, response);
            var cacheKey = GenerateTtsCacheKey(request);

            // Act
            var chunks = new List<AudioChunk>();
            await foreach (var chunk in _cache.StreamCachedAudioAsync(cacheKey))
            {
                chunks.Add(chunk);
            }

            // Assert
            chunks.Should().NotBeEmpty();
            var reassembled = chunks.SelectMany(c => c.Data).ToArray();
            reassembled.Should().BeEquivalentTo(response.AudioData);
        }

        [Fact]
        public async Task StreamCachedAudioAsync_WithNonExistentKey_YieldsEmpty()
        {
            // Arrange
            var cacheKey = "non-existent-key";
            _distributedCacheMock.Setup(x => x.Get<TextToSpeechResponse>(cacheKey))
                .Returns((TextToSpeechResponse?)null);

            // Act
            var chunks = new List<AudioChunk>();
            await foreach (var chunk in _cache.StreamCachedAudioAsync(cacheKey))
            {
                chunks.Add(chunk);
            }

            // Assert
            chunks.Should().BeEmpty();
        }

        [Fact]
        public async Task ClearExpiredAsync_ClearsExpiredEntries()
        {
            // Act
            var result = await _cache.ClearExpiredAsync();

            // Assert
            result.Should().BeGreaterOrEqualTo(0);
        }

        [Fact]
        public async Task PreloadContentAsync_PreloadsSpecifiedContent()
        {
            // Arrange
            var content = new PreloadContent
            {
                CommonPhrases = new List<PreloadTtsItem>
                {
                    new PreloadTtsItem
                    {
                        Text = "Hello, world!",
                        Voice = "alloy",
                        Language = "en-US",
                        Ttl = TimeSpan.FromHours(24)
                    }
                }
            };

            // Act
            await _cache.PreloadContentAsync(content);

            // Assert
            _loggerMock.VerifyLog(LogLevel.Information, "Preloading");
        }

        private AudioTranscriptionRequest CreateTranscriptionRequest()
        {
            return new AudioTranscriptionRequest
            {
                AudioData = _fixture.Create<byte[]>(),
                FileName = "test.mp3",
                Language = "en-US"
            };
        }

        private AudioTranscriptionResponse CreateTranscriptionResponse()
        {
            return new AudioTranscriptionResponse
            {
                Text = _fixture.Create<string>(),
                Language = "en-US",
                Duration = 30.0
            };
        }

        private TextToSpeechRequest CreateTtsRequest()
        {
            return new TextToSpeechRequest
            {
                Input = _fixture.Create<string>(),
                Voice = "alloy",
                Language = "en-US"
            };
        }

        private TextToSpeechResponse CreateTtsResponse()
        {
            return new TextToSpeechResponse
            {
                AudioData = _fixture.Create<byte[]>(),
                Duration = 10.0,
                Format = "mp3"
            };
        }

        private void SetupMemoryCacheHit<T>(T value)
        {
            object outValue = value;
            _memoryCacheMock.Setup(x => x.TryGetValue(It.IsAny<object>(), out outValue))
                .Returns(true);
        }

        private void SetupMemoryCacheMiss()
        {
            object outValue = null;
            _memoryCacheMock.Setup(x => x.TryGetValue(It.IsAny<object>(), out outValue))
                .Returns(false);
        }

        private void SetupDistributedCacheHit<T>(T value) where T : class
        {
            _distributedCacheMock.Setup(x => x.Get<T>(It.IsAny<string>()))
                .Returns(value);
        }

        private void SetupDistributedCacheMiss()
        {
            _distributedCacheMock.Setup(x => x.Get<AudioTranscriptionResponse>(It.IsAny<string>()))
                .Returns((AudioTranscriptionResponse?)null);
            _distributedCacheMock.Setup(x => x.Get<TextToSpeechResponse>(It.IsAny<string>()))
                .Returns((TextToSpeechResponse?)null);
        }

        private string GenerateTtsCacheKey(TextToSpeechRequest request)
        {
            // Replicate the cache key generation logic from AudioStreamCache
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var textHash = Convert.ToBase64String(
                sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(request.Input)));

            var keyParts = new[]
            {
                "tts",
                request.Model ?? "default",
                request.Voice,
                request.Language ?? "auto",
                request.Speed?.ToString() ?? "1.0",
                request.ResponseFormat?.ToString() ?? "mp3",
                textHash
            };

            return string.Join(":", keyParts);
        }
    }
}
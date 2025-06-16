using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Unit tests for DatabaseModelCapabilityService
    /// </summary>
    public class DatabaseModelCapabilityServiceTests
    {
        private readonly Mock<IModelProviderMappingRepository> _mockRepository;
        private readonly Mock<ILogger<DatabaseModelCapabilityService>> _mockLogger;
        private readonly IMemoryCache _cache;
        private readonly DatabaseModelCapabilityService _service;

        public DatabaseModelCapabilityServiceTests()
        {
            _mockRepository = new Mock<IModelProviderMappingRepository>();
            _mockLogger = new Mock<ILogger<DatabaseModelCapabilityService>>();
            _cache = new MemoryCache(new MemoryCacheOptions());
            _service = new DatabaseModelCapabilityService(_mockLogger.Object, _mockRepository.Object, _cache);
        }

        [Fact]
        public async Task SupportsVisionAsync_ReturnsTrueForVisionModel()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                ModelAlias = "gpt-4-vision",
                SupportsVision = true,
                ProviderCredential = new ProviderCredential { ProviderName = "openai" }
            };

            _mockRepository.Setup(r => r.GetByModelNameAsync("gpt-4-vision", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mapping);

            // Act
            var result = await _service.SupportsVisionAsync("gpt-4-vision");

            // Assert
            Assert.True(result);
            _mockRepository.Verify(r => r.GetByModelNameAsync("gpt-4-vision", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SupportsVisionAsync_ReturnsFalseForNonVisionModel()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                ModelAlias = "gpt-3.5-turbo",
                SupportsVision = false,
                ProviderCredential = new ProviderCredential { ProviderName = "openai" }
            };

            _mockRepository.Setup(r => r.GetByModelNameAsync("gpt-3.5-turbo", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mapping);

            // Act
            var result = await _service.SupportsVisionAsync("gpt-3.5-turbo");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task SupportsVisionAsync_ReturnsFalseWhenModelNotFound()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetByModelNameAsync("unknown-model", It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelProviderMapping?)null);

            _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ModelProviderMapping>());

            // Act
            var result = await _service.SupportsVisionAsync("unknown-model");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task SupportsVisionAsync_UsesCacheOnSecondCall()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                ModelAlias = "gpt-4-vision",
                SupportsVision = true,
                ProviderCredential = new ProviderCredential { ProviderName = "openai" }
            };

            _mockRepository.Setup(r => r.GetByModelNameAsync("gpt-4-vision", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mapping);

            // Act
            var result1 = await _service.SupportsVisionAsync("gpt-4-vision");
            var result2 = await _service.SupportsVisionAsync("gpt-4-vision");

            // Assert
            Assert.True(result1);
            Assert.True(result2);
            _mockRepository.Verify(r => r.GetByModelNameAsync("gpt-4-vision", It.IsAny<CancellationToken>()), Times.Once); // Only called once due to cache
        }

        [Fact]
        public async Task SupportsAudioTranscriptionAsync_ReturnsTrueForAudioModel()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                ModelAlias = "whisper-1",
                SupportsAudioTranscription = true,
                ProviderCredential = new ProviderCredential { ProviderName = "openai" }
            };

            _mockRepository.Setup(r => r.GetByModelNameAsync("whisper-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mapping);

            // Act
            var result = await _service.SupportsAudioTranscriptionAsync("whisper-1");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task SupportsTextToSpeechAsync_ReturnsTrueForTTSModel()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                ModelAlias = "tts-1",
                SupportsTextToSpeech = true,
                ProviderCredential = new ProviderCredential { ProviderName = "openai" }
            };

            _mockRepository.Setup(r => r.GetByModelNameAsync("tts-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mapping);

            // Act
            var result = await _service.SupportsTextToSpeechAsync("tts-1");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task SupportsRealtimeAudioAsync_ReturnsTrueForRealtimeModel()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                ModelAlias = "gpt-4o-realtime-preview",
                SupportsRealtimeAudio = true,
                ProviderCredential = new ProviderCredential { ProviderName = "openai" }
            };

            _mockRepository.Setup(r => r.GetByModelNameAsync("gpt-4o-realtime-preview", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mapping);

            // Act
            var result = await _service.SupportsRealtimeAudioAsync("gpt-4o-realtime-preview");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task GetTokenizerTypeAsync_ReturnsCorrectTokenizer()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                ModelAlias = "gpt-4",
                TokenizerType = "cl100k_base",
                ProviderCredential = new ProviderCredential { ProviderName = "openai" }
            };

            _mockRepository.Setup(r => r.GetByModelNameAsync("gpt-4", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mapping);

            // Act
            var result = await _service.GetTokenizerTypeAsync("gpt-4");

            // Assert
            Assert.Equal("cl100k_base", result);
        }

        [Fact]
        public async Task GetTokenizerTypeAsync_ReturnsDefaultWhenNotSet()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                ModelAlias = "some-model",
                TokenizerType = null,
                ProviderCredential = new ProviderCredential { ProviderName = "provider" }
            };

            _mockRepository.Setup(r => r.GetByModelNameAsync("some-model", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mapping);

            // Act
            var result = await _service.GetTokenizerTypeAsync("some-model");

            // Assert
            Assert.Equal("cl100k_base", result); // Default tokenizer
        }

        [Fact]
        public async Task GetDefaultModelAsync_ReturnsDefaultForProviderAndCapability()
        {
            // Arrange
            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMapping
                {
                    ModelAlias = "gpt-4o",
                    IsDefault = true,
                    DefaultCapabilityType = "chat",
                    ProviderCredential = new ProviderCredential { ProviderName = "openai" }
                },
                new ModelProviderMapping
                {
                    ModelAlias = "whisper-1",
                    IsDefault = true,
                    DefaultCapabilityType = "transcription",
                    ProviderCredential = new ProviderCredential { ProviderName = "openai" }
                }
            };

            _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mappings);

            // Act
            var chatDefault = await _service.GetDefaultModelAsync("openai", "chat");
            var transcriptionDefault = await _service.GetDefaultModelAsync("openai", "transcription");

            // Assert
            Assert.Equal("gpt-4o", chatDefault);
            Assert.Equal("whisper-1", transcriptionDefault);
        }

        [Fact]
        public async Task GetDefaultModelAsync_ReturnsNullWhenNoDefault()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ModelProviderMapping>());

            // Act
            var result = await _service.GetDefaultModelAsync("unknown-provider", "chat");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetSupportedVoicesAsync_ParsesJsonArray()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                ModelAlias = "tts-1",
                SupportedVoices = "[\"alloy\", \"echo\", \"fable\", \"onyx\", \"nova\", \"shimmer\"]",
                ProviderCredential = new ProviderCredential { ProviderName = "openai" }
            };

            _mockRepository.Setup(r => r.GetByModelNameAsync("tts-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mapping);

            // Act
            var result = await _service.GetSupportedVoicesAsync("tts-1");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(6, result.Count);
            Assert.Contains("alloy", result);
            Assert.Contains("shimmer", result);
        }

        [Fact]
        public async Task GetSupportedVoicesAsync_ReturnsEmptyListWhenNull()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                ModelAlias = "model-without-voices",
                SupportedVoices = null,
                ProviderCredential = new ProviderCredential { ProviderName = "provider" }
            };

            _mockRepository.Setup(r => r.GetByModelNameAsync("model-without-voices", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mapping);

            // Act
            var result = await _service.GetSupportedVoicesAsync("model-without-voices");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetSupportedLanguagesAsync_ParsesJsonArray()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                ModelAlias = "whisper-1",
                SupportedLanguages = "[\"en\", \"es\", \"fr\", \"de\", \"it\"]",
                ProviderCredential = new ProviderCredential { ProviderName = "openai" }
            };

            _mockRepository.Setup(r => r.GetByModelNameAsync("whisper-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mapping);

            // Act
            var result = await _service.GetSupportedLanguagesAsync("whisper-1");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(5, result.Count);
            Assert.Contains("en", result);
            Assert.Contains("it", result);
        }

        [Fact]
        public async Task GetSupportedFormatsAsync_ParsesJsonArray()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                ModelAlias = "tts-1",
                SupportedFormats = "[\"mp3\", \"opus\", \"aac\", \"flac\", \"wav\", \"pcm\"]",
                ProviderCredential = new ProviderCredential { ProviderName = "openai" }
            };

            _mockRepository.Setup(r => r.GetByModelNameAsync("tts-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mapping);

            // Act
            var result = await _service.GetSupportedFormatsAsync("tts-1");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(6, result.Count);
            Assert.Contains("mp3", result);
            Assert.Contains("pcm", result);
        }

        [Fact]
        public async Task GetMappingByModelNameAsync_ChecksProviderModelNameWhenAliasNotFound()
        {
            // Arrange
            var allMappings = new List<ModelProviderMapping>
            {
                new ModelProviderMapping
                {
                    ModelAlias = "custom-alias",
                    ProviderModelName = "actual-model-name",
                    SupportsVision = true,
                    ProviderCredential = new ProviderCredential { ProviderName = "provider" }
                }
            };

            _mockRepository.Setup(r => r.GetByModelNameAsync("actual-model-name", It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelProviderMapping?)null);

            _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(allMappings);

            // Act
            var result = await _service.SupportsVisionAsync("actual-model-name");

            // Assert
            Assert.True(result);
            _mockRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandlesConcurrentCacheAccess()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                ModelAlias = "gpt-4",
                SupportsVision = true,
                ProviderCredential = new ProviderCredential { ProviderName = "openai" }
            };

            _mockRepository.Setup(r => r.GetByModelNameAsync("gpt-4", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mapping);

            // Act - Simulate concurrent access
            var tasks = Enumerable.Range(0, 10).Select(_ => _service.SupportsVisionAsync("gpt-4")).ToArray();
            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.All(results, r => Assert.True(r));
            // Repository should only be called once or twice (due to race conditions), not 10 times
            _mockRepository.Verify(r => r.GetByModelNameAsync("gpt-4", It.IsAny<CancellationToken>()), Times.AtMost(2));
        }

        private void Dispose()
        {
            _cache?.Dispose();
        }
    }
}

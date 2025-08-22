using Moq;

namespace ConduitLLM.Tests.Providers
{
    public partial class OpenAIClientTests
    {
        #region Capability Tests

        [Fact]
        public async Task SupportsTranscriptionAsync_WithCapabilityService_UsesService()
        {
            // Arrange
            var client = CreateOpenAIClient();
            _capabilityServiceMock.Setup(x => x.SupportsAudioTranscriptionAsync("gpt-4"))
                .ReturnsAsync(true);

            // Act
            var result = await client.SupportsTranscriptionAsync();

            // Assert
            Assert.True(result);
            _capabilityServiceMock.Verify(x => x.SupportsAudioTranscriptionAsync("gpt-4"), Times.Once);
        }

        [Fact]
        public async Task SupportsTranscriptionAsync_WithCapabilityServiceError_FallsBackToDefault()
        {
            // Arrange
            var client = CreateOpenAIClient();
            _capabilityServiceMock.Setup(x => x.SupportsAudioTranscriptionAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await client.SupportsTranscriptionAsync();

            // Assert
            Assert.True(result); // Falls back to true for OpenAI
        }

        [Fact]
        public async Task GetSupportedFormatsAsync_ReturnsWhisperFormats()
        {
            // Arrange
            var client = CreateOpenAIClient();
            _capabilityServiceMock.Setup(x => x.GetSupportedFormatsAsync("gpt-4"))
                .ReturnsAsync(new List<string> { "mp3", "wav" });

            // Act
            var formats = await client.GetSupportedFormatsAsync();

            // Assert
            Assert.NotNull(formats);
            Assert.Contains("mp3", formats);
            Assert.Contains("wav", formats);
        }

        [Fact]
        public async Task GetSupportedLanguagesAsync_ReturnsWhisperLanguages()
        {
            // Arrange
            var client = CreateOpenAIClient();
            _capabilityServiceMock.Setup(x => x.GetSupportedLanguagesAsync("gpt-4"))
                .ThrowsAsync(new Exception("Service error")); // Force fallback to default

            // Act
            var languages = await client.GetSupportedLanguagesAsync();

            // Assert
            Assert.NotNull(languages);
            Assert.Contains("en", languages);
            Assert.Contains("es", languages);
            Assert.Contains("fr", languages);
            Assert.Contains("de", languages);
            Assert.Contains("zh", languages);
            Assert.Contains("ja", languages);
            // And many more...
        }

        [Fact]
        public async Task SupportsTextToSpeechAsync_WithCapabilityService_UsesService()
        {
            // Arrange
            var client = CreateOpenAIClient();
            _capabilityServiceMock.Setup(x => x.SupportsTextToSpeechAsync("gpt-4"))
                .ReturnsAsync(true);

            // Act
            var result = await client.SupportsTextToSpeechAsync();

            // Assert
            Assert.True(result);
            _capabilityServiceMock.Verify(x => x.SupportsTextToSpeechAsync("gpt-4"), Times.Once);
        }

        #endregion

        #region Realtime Audio Tests

        [Fact]
        public async Task SupportsRealtimeAsync_WithSupportedModel_ReturnsTrue()
        {
            // Arrange
            var client = CreateOpenAIClient();
            _capabilityServiceMock.Setup(x => x.SupportsRealtimeAudioAsync("gpt-4o-realtime-preview"))
                .ReturnsAsync(true);

            // Act
            var result = await client.SupportsRealtimeAsync("gpt-4o-realtime-preview");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task SupportsRealtimeAsync_WithUnsupportedModel_ReturnsFalse()
        {
            // Arrange
            var client = CreateOpenAIClient();
            _capabilityServiceMock.Setup(x => x.SupportsRealtimeAudioAsync("gpt-4"))
                .ReturnsAsync(false);

            // Act
            var result = await client.SupportsRealtimeAsync("gpt-4");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetRealtimeCapabilitiesAsync_ReturnsExpectedCapabilities()
        {
            // Arrange
            var client = CreateOpenAIClient();

            // Act
            var capabilities = await client.GetRealtimeCapabilitiesAsync();

            // Assert
            Assert.NotNull(capabilities);
            Assert.NotEmpty(capabilities.SupportedInputFormats);
            Assert.NotEmpty(capabilities.SupportedOutputFormats);
            Assert.NotEmpty(capabilities.AvailableVoices);
            Assert.NotEmpty(capabilities.SupportedLanguages);
            Assert.True(capabilities.SupportsFunctionCalling);
            Assert.True(capabilities.SupportsInterruptions);
        }

        #endregion

        #region Provider Capabilities Tests

        [Fact]
        public async Task GetCapabilitiesAsync_ForChatModel_ReturnsCorrectCapabilities()
        {
            // Arrange
            var client = CreateOpenAIClient("gpt-4");

            // Act
            var capabilities = await client.GetCapabilitiesAsync();

            // Assert
            Assert.NotNull(capabilities);
            Assert.Equal("OpenAI", capabilities.Provider);
            Assert.Equal("gpt-4", capabilities.ModelId);
            
            // Chat parameters
            Assert.True(capabilities.ChatParameters.Temperature);
            Assert.True(capabilities.ChatParameters.MaxTokens);
            Assert.True(capabilities.ChatParameters.TopP);
            Assert.False(capabilities.ChatParameters.TopK); // OpenAI doesn't support top-k
            Assert.True(capabilities.ChatParameters.Stop);
            Assert.True(capabilities.ChatParameters.Tools);
            
            // Features
            Assert.True(capabilities.Features.Streaming);
            Assert.False(capabilities.Features.Embeddings);
            Assert.False(capabilities.Features.ImageGeneration);
            Assert.True(capabilities.Features.FunctionCalling);
        }

        [Fact]
        public async Task GetCapabilitiesAsync_ForVisionModel_EnablesVisionInput()
        {
            // Arrange
            var client = CreateOpenAIClient("gpt-4o");

            // Act
            var capabilities = await client.GetCapabilitiesAsync();

            // Assert
            Assert.True(capabilities.Features.VisionInput);
        }

        [Fact]
        public async Task GetCapabilitiesAsync_ForDalleModel_EnablesImageGeneration()
        {
            // Arrange
            var client = CreateOpenAIClient("dall-e-3");

            // Act
            var capabilities = await client.GetCapabilitiesAsync();

            // Assert
            Assert.True(capabilities.Features.ImageGeneration);
            Assert.False(capabilities.Features.Streaming);
            Assert.False(capabilities.ChatParameters.Tools);
        }

        [Fact]
        public async Task GetCapabilitiesAsync_ForEmbeddingModel_EnablesEmbeddings()
        {
            // Arrange
            var client = CreateOpenAIClient("text-embedding-ada-002");

            // Act
            var capabilities = await client.GetCapabilitiesAsync();

            // Assert
            Assert.True(capabilities.Features.Embeddings);
            Assert.False(capabilities.Features.Streaming);
            Assert.False(capabilities.ChatParameters.Tools);
        }

        #endregion
    }
}
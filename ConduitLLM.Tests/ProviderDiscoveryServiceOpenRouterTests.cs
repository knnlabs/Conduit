using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Core.Services;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Configuration;
using MassTransit;

namespace ConduitLLM.Tests
{
    public class ProviderDiscoveryServiceOpenRouterTests
    {
        private readonly Mock<ILLMClientFactory> _clientFactoryMock;
        private readonly Mock<ConduitLLM.Configuration.IProviderCredentialService> _credentialServiceMock;
        private readonly Mock<ConduitLLM.Configuration.IModelProviderMappingService> _mappingServiceMock;
        private readonly Mock<ILogger<ProviderDiscoveryService>> _loggerMock;
        private readonly IMemoryCache _cache;
        private readonly ProviderDiscoveryService _discoveryService;

        public ProviderDiscoveryServiceOpenRouterTests()
        {
            _clientFactoryMock = new Mock<ILLMClientFactory>();
            _credentialServiceMock = new Mock<ConduitLLM.Configuration.IProviderCredentialService>();
            _mappingServiceMock = new Mock<ConduitLLM.Configuration.IModelProviderMappingService>();
            _loggerMock = new Mock<ILogger<ProviderDiscoveryService>>();
            _cache = new MemoryCache(new MemoryCacheOptions());

            _discoveryService = new ProviderDiscoveryService(
                _clientFactoryMock.Object,
                _credentialServiceMock.Object,
                _mappingServiceMock.Object,
                _loggerMock.Object,
                _cache
            );
        }

        [Theory]
        [InlineData("anthropic/claude-3-opus", true, true, true, false)] // Chat + Stream + ToolUse
        [InlineData("anthropic/claude-3-sonnet", true, true, true, false)] // Chat + Stream + ToolUse
        [InlineData("openai/gpt-4-turbo", true, true, true, true)] // Chat + Stream + FunctionCalling + ToolUse
        [InlineData("openai/gpt-4-vision-preview", true, true, true, true)] // Chat + Stream + Vision + FunctionCalling
        [InlineData("google/gemini-pro", true, true, true, false)] // Chat + Stream + Vision
        [InlineData("google/gemini-pro-vision", true, true, true, false)] // Chat + Stream + Vision
        [InlineData("meta-llama/llama-3-70b-instruct", true, true, false, false)] // Chat + Stream only
        [InlineData("mistralai/mistral-large", true, true, false, false)] // Chat + Stream only
        public async Task TestOpenRouterModelCapabilityDetection(
            string modelId, 
            bool expectedChat, 
            bool expectedStream, 
            bool expectedVision,
            bool expectedFunctionCalling)
        {
            // Arrange
            var mockClient = new Mock<ILLMClient>();
            mockClient.Setup(c => c.ListModelsAsync(It.IsAny<string>(), default))
                .ReturnsAsync(new List<string> { modelId });

            _clientFactoryMock.Setup(f => f.GetClientByProvider("openrouter"))
                .Returns(mockClient.Object);

            var credentials = new List<ConduitLLM.Configuration.Entities.ProviderCredential>
            {
                new() { Id = 1, ProviderName = "openrouter", ApiKey = "test-key", IsEnabled = true }
            };
            _credentialServiceMock.Setup(s => s.GetAllCredentialsAsync())
                .ReturnsAsync(credentials);

            _mappingServiceMock.Setup(s => s.GetAllMappingsAsync())
                .ReturnsAsync(new List<ModelProviderMapping>());

            // Act
            var models = await _discoveryService.DiscoverModelsAsync();

            // Assert
            Assert.Contains(modelId, models.Keys);
            var model = models[modelId];
            
            Assert.Equal("openrouter", model.Provider);
            Assert.Equal(expectedChat, model.Capabilities.Chat);
            Assert.Equal(expectedStream, model.Capabilities.ChatStream);
            Assert.Equal(expectedVision, model.Capabilities.Vision);
            Assert.Equal(expectedFunctionCalling, model.Capabilities.FunctionCalling);
            
            // OpenRouter models should never have image generation
            Assert.False(model.Capabilities.ImageGeneration);
        }

        [Fact]
        public async Task TestOpenRouterFallbackModels_WhenApiUnavailable()
        {
            // Arrange
            var mockClient = new Mock<ILLMClient>();
            mockClient.Setup(c => c.ListModelsAsync(It.IsAny<string>(), default))
                .ThrowsAsync(new Exception("API error"));

            _clientFactoryMock.Setup(f => f.GetClientByProvider("openrouter"))
                .Returns(mockClient.Object);

            var credentials = new List<ConduitLLM.Configuration.Entities.ProviderCredential>
            {
                new() { Id = 1, ProviderName = "openrouter", ApiKey = "test-key", IsEnabled = true }
            };
            _credentialServiceMock.Setup(s => s.GetAllCredentialsAsync())
                .ReturnsAsync(credentials);

            _mappingServiceMock.Setup(s => s.GetAllMappingsAsync())
                .ReturnsAsync(new List<ModelProviderMapping>());

            // Act
            var models = await _discoveryService.DiscoverModelsAsync();

            // Assert
            // Should have fallback models for OpenRouter
            Assert.Contains("anthropic/claude-3-opus", models.Keys);
            Assert.Contains("openai/gpt-4-turbo", models.Keys);
            Assert.Contains("google/gemini-pro", models.Keys);
            
            // Verify capabilities are properly inferred
            var claudeModel = models["anthropic/claude-3-opus"];
            Assert.True(claudeModel.Capabilities.Chat);
            Assert.True(claudeModel.Capabilities.ChatStream);
            Assert.True(claudeModel.Capabilities.ToolUse);
            
            var gptModel = models["openai/gpt-4-turbo"];
            Assert.True(gptModel.Capabilities.Chat);
            Assert.True(gptModel.Capabilities.ChatStream);
            Assert.True(gptModel.Capabilities.FunctionCalling);
        }

        [Fact]
        public async Task TestOpenRouterModelCapability_UnknownModel_UsesDefaults()
        {
            // Arrange
            var unknownModelId = "unknown-provider/unknown-model";
            
            var mockClient = new Mock<ILLMClient>();
            mockClient.Setup(c => c.ListModelsAsync(It.IsAny<string>(), default))
                .ReturnsAsync(new List<string> { unknownModelId });

            _clientFactoryMock.Setup(f => f.GetClientByProvider("openrouter"))
                .Returns(mockClient.Object);

            var credentials = new List<ConduitLLM.Configuration.Entities.ProviderCredential>
            {
                new() { Id = 1, ProviderName = "openrouter", ApiKey = "test-key", IsEnabled = true }
            };
            _credentialServiceMock.Setup(s => s.GetAllCredentialsAsync())
                .ReturnsAsync(credentials);

            _mappingServiceMock.Setup(s => s.GetAllMappingsAsync())
                .ReturnsAsync(new List<ModelProviderMapping>());

            // Act
            var models = await _discoveryService.DiscoverModelsAsync();

            // Assert
            Assert.Contains(unknownModelId, models.Keys);
            var model = models[unknownModelId];
            
            // Should use default OpenRouter capabilities
            Assert.True(model.Capabilities.Chat);
            Assert.True(model.Capabilities.ChatStream);
            Assert.False(model.Capabilities.Vision);
            Assert.False(model.Capabilities.ImageGeneration);
            Assert.False(model.Capabilities.VideoGeneration);
        }
    }
}
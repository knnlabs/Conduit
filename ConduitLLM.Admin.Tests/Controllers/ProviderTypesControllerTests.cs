using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Admin.Controllers;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Admin.Tests.Controllers
{
    /// <summary>
    /// Unit tests for the ProviderTypesController using the Provider Registry.
    /// </summary>
    public class ProviderTypesControllerTests
    {
        private readonly Mock<IProviderMetadataRegistry> _mockRegistry;
        private readonly Mock<ILogger<ProviderTypesController>> _mockLogger;
        private readonly ProviderTypesController _controller;

        public ProviderTypesControllerTests()
        {
            _mockRegistry = new Mock<IProviderMetadataRegistry>();
            _mockLogger = new Mock<ILogger<ProviderTypesController>>();
            _controller = new ProviderTypesController(_mockRegistry.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullRegistry_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ProviderTypesController(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ProviderTypesController(_mockRegistry.Object, null!));
        }

        #endregion

        #region GetProviderTypes Tests

        [Fact]
        public void GetProviderTypes_WithRegisteredProviders_ReturnsCorrectInfo()
        {
            // Arrange
            var mockMetadata = new Mock<IProviderMetadata>();
            mockMetadata.Setup(m => m.DisplayName).Returns("Test Provider");
            mockMetadata.Setup(m => m.DefaultBaseUrl).Returns("https://test.api.com");

            _mockRegistry.Setup(r => r.TryGetMetadata(It.IsAny<ProviderType>(), out It.Ref<IProviderMetadata?>.IsAny))
                .Returns((ProviderType pt, out IProviderMetadata? metadata) =>
                {
                    if (pt == ProviderType.OpenAI)
                    {
                        metadata = mockMetadata.Object;
                        return true;
                    }
                    metadata = null;
                    return false;
                });

            // Act
            var result = _controller.GetProviderTypes() as OkObjectResult;

            // Assert
            Assert.NotNull(result);
            var providerTypes = result.Value as IEnumerable<ProviderTypeInfo>;
            Assert.NotNull(providerTypes);
            
            var openAIInfo = providerTypes.FirstOrDefault(p => p.Name == "OpenAI");
            Assert.NotNull(openAIInfo);
            Assert.Equal(1, openAIInfo.Value);
            Assert.Equal("Test Provider", openAIInfo.DisplayName);
            Assert.True(openAIInfo.IsRegistered);
            Assert.Equal("https://test.api.com", openAIInfo.DefaultBaseUrl);
        }

        [Fact]
        public void GetProviderTypes_WithUnregisteredProviders_MarksAsNotRegistered()
        {
            // Arrange
            _mockRegistry.Setup(r => r.TryGetMetadata(It.IsAny<ProviderType>(), out It.Ref<IProviderMetadata?>.IsAny))
                .Returns(false);

            // Act
            var result = _controller.GetProviderTypes() as OkObjectResult;

            // Assert
            Assert.NotNull(result);
            var providerTypes = result.Value as IEnumerable<ProviderTypeInfo>;
            Assert.NotNull(providerTypes);
            
            var anyProvider = providerTypes.First();
            Assert.False(anyProvider.IsRegistered);
            Assert.Null(anyProvider.DefaultBaseUrl);
            Assert.Equal(anyProvider.Name, anyProvider.DisplayName);
        }

        [Fact]
        public void GetProviderTypes_WhenExceptionOccurs_Returns500()
        {
            // Arrange
            _mockRegistry.Setup(r => r.TryGetMetadata(It.IsAny<ProviderType>(), out It.Ref<IProviderMetadata?>.IsAny))
                .Throws(new Exception("Test exception"));

            // Act
            var result = _controller.GetProviderTypes() as ObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(500, result.StatusCode);
            Assert.Equal("An error occurred while retrieving provider types", result.Value);
        }

        #endregion

        #region GetProviderCapabilities Tests

        [Fact]
        public void GetProviderCapabilities_WhenProviderExists_ReturnsCapabilities()
        {
            // Arrange
            var capabilities = new ProviderCapabilities
            {
                Provider = "OpenAI",
                Features = new FeatureSupport { Streaming = true }
            };
            
            var mockMetadata = new Mock<IProviderMetadata>();
            mockMetadata.Setup(m => m.Capabilities).Returns(capabilities);

            _mockRegistry.Setup(r => r.TryGetMetadata(ProviderType.OpenAI, out It.Ref<IProviderMetadata?>.IsAny))
                .Returns((ProviderType pt, out IProviderMetadata? metadata) =>
                {
                    metadata = mockMetadata.Object;
                    return true;
                });

            // Act
            var result = _controller.GetProviderCapabilities(ProviderType.OpenAI) as OkObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Same(capabilities, result.Value);
        }

        [Fact]
        public void GetProviderCapabilities_WhenProviderNotFound_Returns404()
        {
            // Arrange
            _mockRegistry.Setup(r => r.TryGetMetadata(It.IsAny<ProviderType>(), out It.Ref<IProviderMetadata?>.IsAny))
                .Returns(false);

            // Act
            var result = _controller.GetProviderCapabilities(ProviderType.OpenAI) as NotFoundObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(404, result.StatusCode);
        }

        [Fact]
        public void GetProviderCapabilities_WhenExceptionOccurs_Returns500()
        {
            // Arrange
            _mockRegistry.Setup(r => r.TryGetMetadata(It.IsAny<ProviderType>(), out It.Ref<IProviderMetadata?>.IsAny))
                .Throws(new Exception("Test exception"));

            // Act
            var result = _controller.GetProviderCapabilities(ProviderType.OpenAI) as ObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(500, result.StatusCode);
        }

        #endregion

        #region GetAuthRequirements Tests

        [Fact]
        public void GetAuthRequirements_WhenProviderExists_ReturnsAuthRequirements()
        {
            // Arrange
            var authRequirements = new AuthenticationRequirements
            {
                RequiresApiKey = true,
                ApiKeyHeaderName = "Authorization"
            };
            
            var mockMetadata = new Mock<IProviderMetadata>();
            mockMetadata.Setup(m => m.AuthRequirements).Returns(authRequirements);

            _mockRegistry.Setup(r => r.TryGetMetadata(ProviderType.OpenAI, out It.Ref<IProviderMetadata?>.IsAny))
                .Returns((ProviderType pt, out IProviderMetadata? metadata) =>
                {
                    metadata = mockMetadata.Object;
                    return true;
                });

            // Act
            var result = _controller.GetAuthRequirements(ProviderType.OpenAI) as OkObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Same(authRequirements, result.Value);
        }

        [Fact]
        public void GetAuthRequirements_WhenProviderNotFound_Returns404()
        {
            // Arrange
            _mockRegistry.Setup(r => r.TryGetMetadata(It.IsAny<ProviderType>(), out It.Ref<IProviderMetadata?>.IsAny))
                .Returns(false);

            // Act
            var result = _controller.GetAuthRequirements(ProviderType.OpenAI) as NotFoundObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(404, result.StatusCode);
        }

        #endregion

        #region GetConfigurationHints Tests

        [Fact]
        public void GetConfigurationHints_WhenProviderExists_ReturnsHints()
        {
            // Arrange
            var hints = new ProviderConfigurationHints
            {
                DocumentationUrl = "https://docs.example.com",
                RequiresSpecialSetup = true
            };
            
            var mockMetadata = new Mock<IProviderMetadata>();
            mockMetadata.Setup(m => m.ConfigurationHints).Returns(hints);

            _mockRegistry.Setup(r => r.TryGetMetadata(ProviderType.OpenAI, out It.Ref<IProviderMetadata?>.IsAny))
                .Returns((ProviderType pt, out IProviderMetadata? metadata) =>
                {
                    metadata = mockMetadata.Object;
                    return true;
                });

            // Act
            var result = _controller.GetConfigurationHints(ProviderType.OpenAI) as OkObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Same(hints, result.Value);
        }

        #endregion

        #region GetProvidersByFeature Tests

        [Fact]
        public void GetProvidersByFeature_WithValidFeature_ReturnsProviders()
        {
            // Arrange
            var mockMetadata1 = CreateMockMetadata(ProviderType.OpenAI, "OpenAI", "https://api.openai.com");
            var mockMetadata2 = CreateMockMetadata(ProviderType.Anthropic, "Anthropic", "https://api.anthropic.com");
            
            _mockRegistry.Setup(r => r.GetProvidersByFeature(It.IsAny<Func<FeatureSupport, bool>>()))
                .Returns(new[] { mockMetadata1.Object, mockMetadata2.Object });

            // Act
            var result = _controller.GetProvidersByFeature("streaming") as OkObjectResult;

            // Assert
            Assert.NotNull(result);
            var providers = result.Value as IEnumerable<ProviderTypeInfo>;
            Assert.NotNull(providers);
            Assert.Equal(2, providers.Count());
        }

        [Fact]
        public void GetProvidersByFeature_WithInvalidFeature_Returns400()
        {
            // Act
            var result = _controller.GetProvidersByFeature("invalid-feature") as BadRequestObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public void GetProvidersByFeature_RecognizesAllFeatures()
        {
            // Arrange
            var features = new[] { "streaming", "embeddings", "imagegeneration", 
                                  "visioninput", "functioncalling", "audiotranscription", "texttospeech" };
            
            _mockRegistry.Setup(r => r.GetProvidersByFeature(It.IsAny<Func<FeatureSupport, bool>>()))
                .Returns(Array.Empty<IProviderMetadata>());

            // Act & Assert
            foreach (var feature in features)
            {
                var result = _controller.GetProvidersByFeature(feature) as OkObjectResult;
                Assert.NotNull(result);
            }
        }

        #endregion

        #region GetRegistryDiagnostics Tests

        [Fact]
        public void GetRegistryDiagnostics_ReturnsExpectedDiagnostics()
        {
            // Arrange
            var diagnostics = new ProviderRegistryDiagnostics
            {
                TotalProviders = 21,
                RegisteredProviders = new List<string> { "OpenAI", "Anthropic" },
                ProvidersByCapability = new Dictionary<string, List<string>>
                {
                    ["Streaming"] = new List<string> { "OpenAI", "Anthropic" }
                }
            };

            _mockRegistry.Setup(r => r.GetDiagnostics()).Returns(diagnostics);

            // Act
            var result = _controller.GetRegistryDiagnostics() as OkObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Same(diagnostics, result.Value);
        }

        [Fact]
        public void GetRegistryDiagnostics_WhenExceptionOccurs_Returns500()
        {
            // Arrange
            _mockRegistry.Setup(r => r.GetDiagnostics()).Throws(new Exception("Test exception"));

            // Act
            var result = _controller.GetRegistryDiagnostics() as ObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(500, result.StatusCode);
        }

        #endregion

        #region Helper Methods

        private Mock<IProviderMetadata> CreateMockMetadata(ProviderType providerType, string displayName, string baseUrl)
        {
            var mock = new Mock<IProviderMetadata>();
            mock.Setup(m => m.ProviderType).Returns(providerType);
            mock.Setup(m => m.DisplayName).Returns(displayName);
            mock.Setup(m => m.DefaultBaseUrl).Returns(baseUrl);
            return mock;
        }

        #endregion
    }
}
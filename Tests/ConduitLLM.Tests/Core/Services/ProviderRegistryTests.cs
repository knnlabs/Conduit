using Microsoft.Extensions.Logging;
using Moq;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Unit tests for the ProviderMetadataRegistry implementation.
    /// </summary>
    public class ProviderRegistryTests
    {
        private readonly Mock<ILogger<ProviderMetadataRegistry>> _mockLogger;
        private readonly ProviderMetadataRegistry _registry;

        public ProviderRegistryTests()
        {
            _mockLogger = new Mock<ILogger<ProviderMetadataRegistry>>();
            _registry = new ProviderMetadataRegistry(_mockLogger.Object);
        }

        #region GetMetadata Tests

        [Fact]
        public void GetMetadata_WhenProviderExists_ReturnsCorrectMetadata()
        {
            // Act
            var metadata = _registry.GetMetadata(ProviderType.OpenAI);

            // Assert
            Assert.NotNull(metadata);
            Assert.Equal(ProviderType.OpenAI, metadata.ProviderType);
            Assert.Equal("OpenAI", metadata.DisplayName);
            Assert.NotNull(metadata.AuthRequirements);
        }

        [Fact]
        public void GetMetadata_WhenProviderDoesNotExist_ThrowsProviderNotFoundException()
        {
            // Arrange
            var nonExistentProvider = (ProviderType)999;

            // Act & Assert
            var exception = Assert.Throws<ProviderNotFoundException>(() => 
                _registry.GetMetadata(nonExistentProvider));
            
            Assert.Equal(nonExistentProvider, exception.ProviderType);
        }

        #endregion

        #region TryGetMetadata Tests

        [Fact]
        public void TryGetMetadata_WhenProviderExists_ReturnsTrueAndSetsMetadata()
        {
            // Act
            var result = _registry.TryGetMetadata(ProviderType.OpenAI, out var metadata);

            // Assert
            Assert.True(result);
            Assert.NotNull(metadata);
            Assert.Equal(ProviderType.OpenAI, metadata!.ProviderType);
            Assert.Equal("OpenAI", metadata.DisplayName);
        }

        [Fact]
        public void TryGetMetadata_WhenProviderDoesNotExist_ReturnsFalseAndNullMetadata()
        {
            // Arrange
            var nonExistentProvider = (ProviderType)999;

            // Act
            var result = _registry.TryGetMetadata(nonExistentProvider, out var metadata);

            // Assert
            Assert.False(result);
            Assert.Null(metadata);
        }

        #endregion

        #region GetAllMetadata Tests

        [Fact]
        public void GetAllMetadata_ReturnsAllRegisteredProviders()
        {
            // Act
            var allMetadata = _registry.GetAllMetadata().ToList();

            // Assert
            Assert.NotEmpty(allMetadata);
            // Should have metadata for most provider types
            Assert.True(allMetadata.Count >= 8); // Expecting at least 8 providers (we have 10 defined)
            
            // Verify they are ordered by enum value
            for (int i = 1; i < allMetadata.Count; i++)
            {
                Assert.True((int)allMetadata[i].ProviderType >= (int)allMetadata[i - 1].ProviderType);
            }
        }

        [Fact]
        public void GetAllMetadata_ReturnsUniqueProviders()
        {
            // Act
            var allMetadata = _registry.GetAllMetadata().ToList();

            // Assert
            var uniqueProviderTypes = allMetadata.Select(m => m.ProviderType).Distinct().Count();
            Assert.Equal(allMetadata.Count, uniqueProviderTypes);
        }

        #endregion

        #region IsRegistered Tests

        [Fact]
        public void IsRegistered_WhenProviderExists_ReturnsTrue()
        {
            // Act & Assert
            Assert.True(_registry.IsRegistered(ProviderType.OpenAI));
            Assert.True(_registry.IsRegistered(ProviderType.Groq));
            Assert.True(_registry.IsRegistered(ProviderType.Replicate));
        }

        [Fact]
        public void IsRegistered_WhenProviderDoesNotExist_ReturnsFalse()
        {
            // Arrange
            var nonExistentProvider = (ProviderType)999;

            // Act & Assert
            Assert.False(_registry.IsRegistered(nonExistentProvider));
        }

        #endregion

        #region GetDiagnostics Tests

        [Fact]
        public void GetDiagnostics_ReturnsValidDiagnosticInfo()
        {
            // Act
            var diagnostics = _registry.GetDiagnostics();

            // Assert
            Assert.NotNull(diagnostics);
            Assert.True(diagnostics.TotalProviders > 0);
            Assert.NotEmpty(diagnostics.RegisteredProviders);
            Assert.True(diagnostics.GeneratedAt <= DateTime.UtcNow);
            Assert.True(diagnostics.GeneratedAt > DateTime.UtcNow.AddMinutes(-1));
        }

        #endregion

        #region Provider-Specific Metadata Tests

        [Fact]
        public void OpenAIMetadata_HasCorrectConfiguration()
        {
            // Act
            var metadata = _registry.GetMetadata(ProviderType.OpenAI);

            // Assert
            Assert.Equal("https://api.openai.com/v1", metadata.DefaultBaseUrl);
            Assert.True(metadata.AuthRequirements.RequiresApiKey);
            Assert.Equal("Authorization", metadata.AuthRequirements.ApiKeyHeaderName);
        }

        // Test removed - Anthropic provider no longer supported, replaced with OpenAI

        // Test removed - AzureOpenAI provider no longer supported, replaced with OpenAI

        [Fact]
        public void OpenAICompatibleMetadata_HasCorrectConfiguration()
        {
            // Act
            var metadata = _registry.GetMetadata(ProviderType.OpenAICompatible);

            // Assert
            Assert.NotNull(metadata.DefaultBaseUrl);
            Assert.True(metadata.AuthRequirements.RequiresApiKey);
            Assert.NotNull(metadata.ConfigurationHints);
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void OpenAIProvider_ValidateConfiguration_WithValidApiKey_ReturnsSuccess()
        {
            // Arrange
            var metadata = _registry.GetMetadata(ProviderType.OpenAI);
            var config = new Dictionary<string, object>
            {
                ["apiKey"] = "sk-1234567890abcdef"
            };

            // Act
            var result = metadata.ValidateConfiguration(config);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void OpenAIProvider_ValidateConfiguration_WithInvalidApiKey_ReturnsError()
        {
            // Arrange
            var metadata = _registry.GetMetadata(ProviderType.OpenAI);
            var config = new Dictionary<string, object>
            {
                ["apiKey"] = "invalid-key-format"
            };

            // Act
            var result = metadata.ValidateConfiguration(config);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Field == "apiKey");
        }

        // Test removed - AzureOpenAI provider no longer supported, replaced with OpenAI

        #endregion
    }
}

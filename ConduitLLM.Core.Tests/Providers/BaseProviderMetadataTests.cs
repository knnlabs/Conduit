using System;
using System.Collections.Generic;
using Xunit;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Providers;

namespace ConduitLLM.Core.Tests.Providers
{
    /// <summary>
    /// Unit tests for the BaseProviderMetadata class.
    /// </summary>
    public class BaseProviderMetadataTests
    {
        /// <summary>
        /// Test implementation of BaseProviderMetadata for testing purposes.
        /// </summary>
        private class TestProviderMetadata : BaseProviderMetadata
        {
            public override ProviderType ProviderType => ProviderType.OpenAI;
            public override string DisplayName => "Test Provider";
            public override string DefaultBaseUrl => "https://test.api.com/v1";

            public TestProviderMetadata(bool customizeCapabilities = false)
            {
                if (customizeCapabilities)
                {
                    Capabilities = new ProviderCapabilities
                    {
                        Provider = ProviderType.ToString(),
                        ChatParameters = new ChatParameterSupport
                        {
                            Temperature = true,
                            MaxTokens = true,
                            Tools = true
                        },
                        Features = new FeatureSupport
                        {
                            Streaming = true,
                            Embeddings = true
                        }
                    };

                    AuthRequirements = new AuthenticationRequirements
                    {
                        RequiresApiKey = true,
                        CustomFields = new List<AuthField>
                        {
                            new AuthField
                            {
                                Name = "region",
                                DisplayName = "Region",
                                Required = true,
                                Type = AuthFieldType.Text,
                                ValidationPattern = @"^[a-z]{2}-[a-z]+-\d$"
                            },
                            new AuthField
                            {
                                Name = "optionalField",
                                DisplayName = "Optional Field",
                                Required = false,
                                Type = AuthFieldType.Text
                            }
                        }
                    };
                }
            }
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_InitializesDefaultValues()
        {
            // Act
            var metadata = new TestProviderMetadata();

            // Assert
            Assert.NotNull(metadata.Capabilities);
            Assert.NotNull(metadata.AuthRequirements);
            Assert.NotNull(metadata.ConfigurationHints);
            
            // Verify default capabilities
            Assert.True(metadata.Capabilities.ChatParameters.Temperature);
            Assert.True(metadata.Capabilities.ChatParameters.MaxTokens);
            Assert.True(metadata.Capabilities.Features.Streaming);
            Assert.False(metadata.Capabilities.Features.ImageGeneration);
            
            // Verify default auth requirements
            Assert.True(metadata.AuthRequirements.RequiresApiKey);
            Assert.False(metadata.AuthRequirements.SupportsOAuth);
            Assert.Equal("Authorization", metadata.AuthRequirements.ApiKeyHeaderName);
        }

        #endregion

        #region ValidateConfiguration Tests

        [Fact]
        public void ValidateConfiguration_WithValidApiKey_ReturnsSuccess()
        {
            // Arrange
            var metadata = new TestProviderMetadata();
            var config = new Dictionary<string, object>
            {
                ["apiKey"] = "valid-api-key-123"
            };

            // Act
            var result = metadata.ValidateConfiguration(config);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ValidateConfiguration_WithMissingApiKey_ReturnsError()
        {
            // Arrange
            var metadata = new TestProviderMetadata();
            var config = new Dictionary<string, object>();

            // Act
            var result = metadata.ValidateConfiguration(config);

            // Assert
            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.Equal("apiKey", result.Errors[0].Field);
            Assert.Equal("API key is required", result.Errors[0].Message);
        }

        [Fact]
        public void ValidateConfiguration_WithEmptyApiKey_ReturnsError()
        {
            // Arrange
            var metadata = new TestProviderMetadata();
            var config = new Dictionary<string, object>
            {
                ["apiKey"] = ""
            };

            // Act
            var result = metadata.ValidateConfiguration(config);

            // Assert
            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.Equal("apiKey", result.Errors[0].Field);
        }

        [Fact]
        public void ValidateConfiguration_WithCustomRequiredFields_ValidatesCorrectly()
        {
            // Arrange
            var metadata = new TestProviderMetadata(customizeCapabilities: true);
            var config = new Dictionary<string, object>
            {
                ["apiKey"] = "valid-key",
                ["region"] = "us-east-1"
            };

            // Act
            var result = metadata.ValidateConfiguration(config);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ValidateConfiguration_WithMissingCustomRequiredField_ReturnsError()
        {
            // Arrange
            var metadata = new TestProviderMetadata(customizeCapabilities: true);
            var config = new Dictionary<string, object>
            {
                ["apiKey"] = "valid-key"
                // Missing required "region" field
            };

            // Act
            var result = metadata.ValidateConfiguration(config);

            // Assert
            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.Equal("region", result.Errors[0].Field);
            Assert.Equal("Region is required", result.Errors[0].Message);
        }

        [Fact]
        public void ValidateConfiguration_WithInvalidPatternForCustomField_ReturnsError()
        {
            // Arrange
            var metadata = new TestProviderMetadata(customizeCapabilities: true);
            var config = new Dictionary<string, object>
            {
                ["apiKey"] = "valid-key",
                ["region"] = "invalid-region-format"
            };

            // Act
            var result = metadata.ValidateConfiguration(config);

            // Assert
            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.Equal("region", result.Errors[0].Field);
            Assert.Contains("format is invalid", result.Errors[0].Message);
        }

        [Fact]
        public void ValidateConfiguration_WithValidBaseUrl_ReturnsSuccess()
        {
            // Arrange
            var metadata = new TestProviderMetadata();
            var config = new Dictionary<string, object>
            {
                ["apiKey"] = "valid-key",
                ["baseUrl"] = "https://custom.api.com/v1"
            };

            // Act
            var result = metadata.ValidateConfiguration(config);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ValidateConfiguration_WithInvalidBaseUrl_ReturnsError()
        {
            // Arrange
            var metadata = new TestProviderMetadata();
            var config = new Dictionary<string, object>
            {
                ["apiKey"] = "valid-key",
                ["baseUrl"] = "not-a-valid-url"
            };

            // Act
            var result = metadata.ValidateConfiguration(config);

            // Assert
            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.Equal("baseUrl", result.Errors[0].Field);
            Assert.Equal("Base URL must be a valid HTTP(S) URL", result.Errors[0].Message);
        }

        [Fact]
        public void ValidateConfiguration_WithOptionalFields_DoesNotRequireThem()
        {
            // Arrange
            var metadata = new TestProviderMetadata(customizeCapabilities: true);
            var config = new Dictionary<string, object>
            {
                ["apiKey"] = "valid-key",
                ["region"] = "us-east-1"
                // optionalField is not provided
            };

            // Act
            var result = metadata.ValidateConfiguration(config);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        #endregion

        #region Helper Method Tests

        [Fact]
        public void CreateApiKeyField_CreatesCorrectField()
        {
            // Act
            var field = BaseProviderMetadata.CreateApiKeyField("Custom API Key", "Get your key from example.com");

            // Assert
            Assert.Equal("apiKey", field.Name);
            Assert.Equal("Custom API Key", field.DisplayName);
            Assert.True(field.Required);
            Assert.Equal(AuthFieldType.Password, field.Type);
            Assert.Equal("Get your key from example.com", field.HelpText);
        }

        [Fact]
        public void CreateUrlField_CreatesCorrectField()
        {
            // Act
            var field = BaseProviderMetadata.CreateUrlField("endpoint", "API Endpoint", true, "The API endpoint URL");

            // Assert
            Assert.Equal("endpoint", field.Name);
            Assert.Equal("API Endpoint", field.DisplayName);
            Assert.True(field.Required);
            Assert.Equal(AuthFieldType.Url, field.Type);
            Assert.NotNull(field.ValidationPattern);
            Assert.Equal("The API endpoint URL", field.HelpText);
        }

        #endregion

        #region Default Method Tests

        [Fact]
        public void CreateDefaultCapabilities_ReturnsReasonableDefaults()
        {
            // Arrange
            var metadata = new TestProviderMetadata();

            // Act - access protected method through public property
            var capabilities = metadata.Capabilities;

            // Assert
            Assert.NotNull(capabilities);
            Assert.True(capabilities.ChatParameters.Temperature);
            Assert.True(capabilities.ChatParameters.MaxTokens);
            Assert.False(capabilities.ChatParameters.TopP);
            Assert.True(capabilities.ChatParameters.Stop);
            Assert.True(capabilities.Features.Streaming);
            Assert.False(capabilities.Features.ImageGeneration);
        }

        [Fact]
        public void CreateDefaultAuthRequirements_ReturnsReasonableDefaults()
        {
            // Arrange
            var metadata = new TestProviderMetadata();

            // Act - access protected method through public property
            var authRequirements = metadata.AuthRequirements;

            // Assert
            Assert.NotNull(authRequirements);
            Assert.True(authRequirements.RequiresApiKey);
            Assert.False(authRequirements.SupportsOAuth);
            Assert.Empty(authRequirements.CustomFields);
            Assert.Equal("Authorization", authRequirements.ApiKeyHeaderName);
        }

        [Fact]
        public void CreateDefaultConfigurationHints_ReturnsEmptyHints()
        {
            // Arrange
            var metadata = new TestProviderMetadata();

            // Act - access protected method through public property
            var hints = metadata.ConfigurationHints;

            // Assert
            Assert.NotNull(hints);
            Assert.False(hints.RequiresSpecialSetup);
            Assert.Empty(hints.ExampleValues);
            Assert.Empty(hints.Tips);
            Assert.Null(hints.DocumentationUrl);
            Assert.Null(hints.SetupInstructions);
        }

        #endregion
    }
}
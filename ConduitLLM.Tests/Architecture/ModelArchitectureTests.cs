using System;
using System.Linq;
using System.Reflection;
using Xunit;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Architecture
{
    /// <summary>
    /// Architecture tests to ensure the Model capability pattern is correctly implemented
    /// and to prevent regressions in the architectural design
    /// </summary>
    public class ModelArchitectureTests
    {
        [Fact]
        public void ModelProviderMapping_CapabilityProperties_ShouldBeReadOnly()
        {
            // Arrange
            var mappingType = typeof(ModelProviderMapping);
            var capabilityProperties = mappingType.GetProperties()
                .Where(p => p.Name.StartsWith("Supports") || 
                           p.Name == "MaxContextTokens" ||
                           p.Name == "TokenizerType")
                .ToList();

            // Act & Assert
            foreach (var property in capabilityProperties)
            {
                // Skip properties that have legitimate setters
                if (property.Name == "MaxContextTokensOverride")
                    continue;

                Assert.False(property.CanWrite, 
                    $"Property {property.Name} should be read-only as it derives from Model.Capabilities");
            }
        }

        [Fact]
        public void ModelProviderMapping_ShouldHaveModelIdRequired()
        {
            // Arrange
            var mappingType = typeof(ModelProviderMapping);
            var modelIdProperty = mappingType.GetProperty("ModelId");

            // Assert
            Assert.NotNull(modelIdProperty);
            Assert.Equal(typeof(int), modelIdProperty.PropertyType);
            // ModelId should not be nullable
            Assert.False(Nullable.GetUnderlyingType(modelIdProperty.PropertyType) != null,
                "ModelId should not be nullable");
        }

        [Fact]
        public void Model_ShouldHaveCapabilitiesRelationship()
        {
            // Arrange
            var modelType = typeof(Model);
            
            // Act
            var capabilitiesProperty = modelType.GetProperty("Capabilities");
            var capabilitiesIdProperty = modelType.GetProperty("ModelCapabilitiesId");

            // Assert
            Assert.NotNull(capabilitiesProperty);
            Assert.Equal(typeof(ModelCapabilities), capabilitiesProperty.PropertyType);
            
            Assert.NotNull(capabilitiesIdProperty);
            Assert.Equal(typeof(int), capabilitiesIdProperty.PropertyType);
        }

        [Fact]
        public void ModelCapabilities_ShouldHaveAllExpectedProperties()
        {
            // Arrange
            var capabilitiesType = typeof(ModelCapabilities);
            var expectedProperties = new[]
            {
                "SupportsChat",
                "SupportsVision",
                "SupportsAudioTranscription",
                "SupportsTextToSpeech",
                "SupportsRealtimeAudio",
                "SupportsImageGeneration",
                "SupportsVideoGeneration",
                "SupportsEmbeddings",
                "SupportsFunctionCalling",
                "SupportsStreaming",
                "MaxTokens",
                "TokenizerType"
            };

            // Act & Assert
            foreach (var propertyName in expectedProperties)
            {
                var property = capabilitiesType.GetProperty(propertyName);
                Assert.NotNull(property);
                Assert.True(property.CanRead && property.CanWrite,
                    $"Property {propertyName} should be read-write in ModelCapabilities");
            }
        }

        [Fact]
        public void ModelProviderMapping_MaxContextTokens_ShouldRespectOverride()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                ModelId = 1,
                Model = new Model
                {
                    Id = 1,
                    Name = "test-model",
                    ModelType = ModelType.Text,
                    Capabilities = new ModelCapabilities
                    {
                        MaxTokens = 4096
                    }
                }
            };

            // Act & Assert - Without override
            Assert.Equal(4096, mapping.MaxContextTokens);

            // Act & Assert - With override
            mapping.MaxContextTokensOverride = 8192;
            Assert.Equal(8192, mapping.MaxContextTokens);

            // Act & Assert - Remove override
            mapping.MaxContextTokensOverride = null;
            Assert.Equal(4096, mapping.MaxContextTokens);
        }

        [Fact]
        public void Model_ShouldHaveRequiredRelationships()
        {
            // Arrange
            var modelType = typeof(Model);

            // Act
            var seriesProperty = modelType.GetProperty("Series");
            var seriesIdProperty = modelType.GetProperty("ModelSeriesId");
            var identifiersProperty = modelType.GetProperty("Identifiers");
            var mappingsProperty = modelType.GetProperty("ProviderMappings");

            // Assert - Model Series relationship
            Assert.NotNull(seriesProperty);
            Assert.Equal(typeof(ModelSeries), seriesProperty.PropertyType);
            Assert.NotNull(seriesIdProperty);

            // Assert - Collections
            Assert.NotNull(identifiersProperty);
            Assert.True(identifiersProperty.PropertyType.IsGenericType);
            
            Assert.NotNull(mappingsProperty);
            Assert.True(mappingsProperty.PropertyType.IsGenericType);
        }

        [Fact]
        public void ModelProviderMapping_ShouldDeriveAllCapabilitiesFromModel()
        {
            // Arrange
            var model = new Model
            {
                Id = 1,
                Name = "test-model",
                ModelType = ModelType.Text,
                Capabilities = new ModelCapabilities
                {
                    SupportsChat = true,
                    SupportsVision = true,
                    SupportsEmbeddings = false,
                    SupportsFunctionCalling = true,
                    SupportsStreaming = true,
                    SupportsAudioTranscription = true,
                    SupportsTextToSpeech = false,
                    SupportsRealtimeAudio = false,
                    SupportsImageGeneration = false,
                    SupportsVideoGeneration = false,
                    MaxTokens = 8192,
                    TokenizerType = TokenizerType.Cl100KBase
                }
            };

            var mapping = new ModelProviderMapping
            {
                ModelId = 1,
                Model = model
            };

            // Act & Assert
            Assert.Equal(model.Capabilities.SupportsChat, mapping.SupportsChat);
            Assert.Equal(model.Capabilities.SupportsVision, mapping.SupportsVision);
            Assert.Equal(model.Capabilities.SupportsEmbeddings, mapping.SupportsEmbeddings);
            Assert.Equal(model.Capabilities.SupportsFunctionCalling, mapping.SupportsFunctionCalling);
            Assert.Equal(model.Capabilities.SupportsStreaming, mapping.SupportsStreaming);
            Assert.Equal(model.Capabilities.SupportsAudioTranscription, mapping.SupportsAudioTranscription);
            Assert.Equal(model.Capabilities.SupportsTextToSpeech, mapping.SupportsTextToSpeech);
            Assert.Equal(model.Capabilities.SupportsRealtimeAudio, mapping.SupportsRealtimeAudio);
            Assert.Equal(model.Capabilities.SupportsImageGeneration, mapping.SupportsImageGeneration);
            Assert.Equal(model.Capabilities.SupportsVideoGeneration, mapping.SupportsVideoGeneration);
            Assert.Equal(model.Capabilities.MaxTokens, mapping.MaxContextTokens);
            Assert.Equal(model.Capabilities.TokenizerType, mapping.TokenizerType);
        }

        [Fact]
        public void ModelProviderMapping_WithNullModel_ShouldReturnDefaultCapabilities()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                ModelId = 1,
                Model = null // Simulating lazy loading not yet loaded
            };

            // Act & Assert - Should return false/default values when Model is null
            Assert.False(mapping.SupportsChat);
            Assert.False(mapping.SupportsVision);
            Assert.False(mapping.SupportsEmbeddings);
            // MaxContextTokens has a default of 4096 when Model is null
            Assert.Equal(4096, mapping.MaxContextTokens);
            Assert.Null(mapping.TokenizerType);
        }

        [Fact]
        public void Model_IsActive_ShouldDefaultToTrue()
        {
            // Arrange & Act
            var model = new Model();

            // Assert
            Assert.True(model.IsActive);
        }

        [Fact]
        public void Model_TimestampProperties_ShouldExist()
        {
            // Arrange
            var modelType = typeof(Model);

            // Act
            var createdAtProperty = modelType.GetProperty("CreatedAt");
            var updatedAtProperty = modelType.GetProperty("UpdatedAt");

            // Assert
            Assert.NotNull(createdAtProperty);
            Assert.Equal(typeof(DateTime), createdAtProperty.PropertyType);
            
            Assert.NotNull(updatedAtProperty);
            Assert.Equal(typeof(DateTime), updatedAtProperty.PropertyType);
        }
    }
}
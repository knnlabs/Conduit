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
                    continue; // This property has been removed

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
        public void Model_ShouldHaveCapabilityProperties()
        {
            // Arrange
            var modelType = typeof(Model);
            
            // Act & Assert - Check that capability properties exist directly on Model
            var supportsChatProperty = modelType.GetProperty("SupportsChat");
            var supportsVisionProperty = modelType.GetProperty("SupportsVision");
            var maxInputTokensProperty = modelType.GetProperty("MaxInputTokens");
            var maxOutputTokensProperty = modelType.GetProperty("MaxOutputTokens");

            Assert.NotNull(supportsChatProperty);
            Assert.NotNull(supportsVisionProperty);
            Assert.NotNull(maxInputTokensProperty);
            Assert.NotNull(maxOutputTokensProperty);
        }

        [Fact]
        public void Model_ShouldHaveAllExpectedCapabilityProperties()
        {
            // Arrange
            var modelType = typeof(Model);
            var expectedProperties = new[]
            {
                "SupportsChat",
                "SupportsVision",
                "SupportsImageGeneration",
                "SupportsVideoGeneration",
                "SupportsEmbeddings",
                "SupportsFunctionCalling",
                "SupportsStreaming",
                "MaxInputTokens",
                "MaxOutputTokens",
                "TokenizerType"
            };

            // Act & Assert
            foreach (var propertyName in expectedProperties)
            {
                var property = modelType.GetProperty(propertyName);
                Assert.NotNull(property);
                Assert.True(property.CanRead && property.CanWrite,
                    $"Property {propertyName} should be read-write in Model");
            }
        }

        [Fact]
        public void ModelProviderMapping_ShouldUseModelTokenLimits()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                ModelId = 1,
                Model = new Model
                {
                    Id = 1,
                    Name = "test-model",
                    MaxInputTokens = 4096,
                    MaxOutputTokens = 2048
                }
            };

            // Act & Assert
            Assert.Equal(4096, mapping.Model.MaxInputTokens);
            Assert.Equal(2048, mapping.Model.MaxOutputTokens);
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
                SupportsChat = true,
                SupportsVision = true,
                SupportsEmbeddings = false,
                SupportsFunctionCalling = true,
                SupportsStreaming = true,
                SupportsImageGeneration = false,
                SupportsVideoGeneration = false,
                MaxInputTokens = 8192,
                MaxOutputTokens = 4096,
                TokenizerType = TokenizerType.Cl100KBase
            };

            var mapping = new ModelProviderMapping
            {
                ModelId = 1,
                Model = model
            };

            // Act & Assert
            Assert.Equal(model.SupportsChat, mapping.SupportsChat);
            Assert.Equal(model.SupportsVision, mapping.SupportsVision);
            Assert.Equal(model.SupportsEmbeddings, mapping.SupportsEmbeddings);
            Assert.Equal(model.SupportsFunctionCalling, mapping.SupportsFunctionCalling);
            Assert.Equal(model.SupportsStreaming, mapping.SupportsStreaming);
            Assert.Equal(model.SupportsImageGeneration, mapping.SupportsImageGeneration);
            Assert.Equal(model.SupportsVideoGeneration, mapping.SupportsVideoGeneration);
            Assert.Equal(model.MaxInputTokens, mapping.Model.MaxInputTokens);
            Assert.Equal(model.TokenizerType, mapping.TokenizerType);
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
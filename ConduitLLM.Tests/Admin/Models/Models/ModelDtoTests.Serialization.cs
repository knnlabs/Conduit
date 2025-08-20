using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using ConduitLLM.Admin.Models.Models;
using ConduitLLM.Admin.Models.ModelCapabilities;
using ConduitLLM.Configuration.Entities;
using FluentAssertions;
using Xunit;

namespace ConduitLLM.Tests.Admin.Models.Models
{
    /// <summary>
    /// Tests for ModelDto serialization and deserialization behavior.
    /// These tests ensure API contract stability and catch breaking changes.
    /// </summary>
    public partial class ModelDtoTests
    {
        [Fact]
        public void ModelDto_Should_Serialize_And_Deserialize_Correctly()
        {
            // Arrange
            var dto = new ModelDto
            {
                Id = 42,
                Name = "gpt-4-turbo",
                ModelSeriesId = 1,
                ModelCapabilitiesId = 5,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 20, 14, 45, 0, DateTimeKind.Utc),
                Capabilities = new ModelCapabilitiesDto
                {
                    Id = 5,
                    SupportsChat = true,
                    SupportsVision = true,
                    MaxTokens = 128000,
                    MinTokens = 1,
                    TokenizerType = TokenizerType.Cl100KBase
                }
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<ModelDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Id.Should().Be(dto.Id);
            deserialized.Name.Should().Be(dto.Name);
            deserialized.ModelSeriesId.Should().Be(dto.ModelSeriesId);
            deserialized.ModelCapabilitiesId.Should().Be(dto.ModelCapabilitiesId);
            deserialized.IsActive.Should().Be(dto.IsActive);
            deserialized.CreatedAt.Should().Be(dto.CreatedAt);
            deserialized.UpdatedAt.Should().Be(dto.UpdatedAt);
            deserialized.Capabilities.Should().NotBeNull();
            deserialized.Capabilities!.SupportsChat.Should().BeTrue();
        }

        [Fact]
        public void ModelDto_Should_Handle_Null_Capabilities()
        {
            // Arrange
            var dto = new ModelDto
            {
                Id = 1,
                Name = "test-model",
                ModelSeriesId = 1,
                ModelCapabilitiesId = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Capabilities = null
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<ModelDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Capabilities.Should().BeNull();
        }

        [Fact]
        public void ModelDto_Should_Preserve_Property_Names_In_Json()
        {
            // Arrange
            var dto = new ModelDto
            {
                Id = 1,
                Name = "test",
                ModelSeriesId = 2,
                ModelCapabilitiesId = 3,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Act
            var json = JsonSerializer.Serialize(dto);

            // Assert - ensure JSON property names match expectations for API compatibility
            json.Should().Contain("\"Id\":");
            json.Should().Contain("\"Name\":");
            json.Should().Contain("\"ModelSeriesId\":");
            json.Should().Contain("\"ModelCapabilitiesId\":");
            json.Should().Contain("\"IsActive\":");
            json.Should().Contain("\"CreatedAt\":");
            json.Should().Contain("\"UpdatedAt\":");
        }

        [Fact]
        public void ModelDto_Should_Handle_Empty_Name()
        {
            // Arrange & Act
            var dto = new ModelDto { Name = string.Empty };

            // Assert
            dto.Name.Should().BeEmpty();
            
            // Serialization should work
            var json = JsonSerializer.Serialize(dto);
            json.Should().Contain("\"Name\":\"\"");
        }

        [Fact]
        public void ModelDto_Should_Handle_Unicode_In_Name()
        {
            // Arrange
            var dto = new ModelDto
            {
                Id = 1,
                Name = "模型-🤖-測試",
                ModelSeriesId = 1,
                ModelCapabilitiesId = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<ModelDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Name.Should().Be("模型-🤖-測試");
        }

        [Theory]
        [InlineData("gpt-4")]
        [InlineData("claude-3-opus-20240229")]
        [InlineData("llama-3.1-405b-instruct")]
        [InlineData("gemini-1.5-pro-002")]
        [InlineData("very-long-model-name-with-many-hyphens-and-numbers-123456789")]
        public void ModelDto_Should_Handle_Various_Model_Names(string modelName)
        {
            // Arrange
            var dto = new ModelDto
            {
                Id = 1,
                Name = modelName,
                ModelSeriesId = 1,
                ModelCapabilitiesId = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<ModelDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Name.Should().Be(modelName);
        }

        [Fact]
        public void ModelDto_Should_Be_Case_Sensitive_For_Json_Properties()
        {
            // Arrange
            var json = @"{
                ""id"": 1,
                ""name"": ""test"",
                ""modelSeriesId"": 2,
                ""modelCapabilitiesId"": 3,
                ""isActive"": true,
                ""createdAt"": ""2024-01-01T00:00:00Z"",
                ""updatedAt"": ""2024-01-01T00:00:00Z""
            }";

            // Act - deserialize with wrong casing (camelCase instead of PascalCase)
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = false };
            var deserialized = JsonSerializer.Deserialize<ModelDto>(json, options);

            // Assert - properties should be default values due to case mismatch
            deserialized.Should().NotBeNull();
            deserialized!.Id.Should().Be(0); // Default value
            deserialized.Name.Should().Be(string.Empty); // Default value
        }

        [Fact]
        public void ModelDto_Should_Handle_Extra_Json_Properties_Gracefully()
        {
            // Arrange - JSON with extra properties that don't exist in DTO
            var json = @"{
                ""Id"": 1,
                ""Name"": ""test"",
                ""ModelSeriesId"": 2,
                ""ModelCapabilitiesId"": 3,
                ""IsActive"": true,
                ""CreatedAt"": ""2024-01-01T00:00:00Z"",
                ""UpdatedAt"": ""2024-01-01T00:00:00Z"",
                ""ExtraProperty"": ""should be ignored"",
                ""AnotherExtra"": 12345
            }";

            // Act
            var deserialized = JsonSerializer.Deserialize<ModelDto>(json);

            // Assert - should deserialize known properties and ignore extras
            deserialized.Should().NotBeNull();
            deserialized!.Id.Should().Be(1);
            deserialized.Name.Should().Be("test");
        }
    }
}
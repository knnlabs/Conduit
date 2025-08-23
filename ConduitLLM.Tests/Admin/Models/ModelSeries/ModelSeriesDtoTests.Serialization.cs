using System.Text.Json;

using ConduitLLM.Admin.Models.ModelSeries;

using FluentAssertions;

namespace ConduitLLM.Tests.Admin.Models.ModelSeries
{
    /// <summary>
    /// Tests for ModelSeriesDto serialization and deserialization behavior.
    /// These tests ensure API contract stability and catch breaking changes.
    /// </summary>
    public partial class ModelSeriesDtoTests
    {
        [Fact]
        public void ModelSeriesDto_Should_Serialize_And_Deserialize_Correctly()
        {
            // Arrange
            var dto = new ModelSeriesDto
            {
                Id = 5,
                AuthorId = 2,
                AuthorName = "OpenAI",
                Name = "GPT-4",
                Description = "Fourth generation GPT models",
                TokenizerType = TokenizerType.Cl100KBase,
                Parameters = "{\"contextLength\":128000,\"architecture\":\"transformer\"}"
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<ModelSeriesDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Id.Should().Be(dto.Id);
            deserialized.AuthorId.Should().Be(dto.AuthorId);
            deserialized.AuthorName.Should().Be(dto.AuthorName);
            deserialized.Name.Should().Be(dto.Name);
            deserialized.Description.Should().Be(dto.Description);
            deserialized.TokenizerType.Should().Be(dto.TokenizerType);
            deserialized.Parameters.Should().Be(dto.Parameters);
        }

        [Fact]
        public void ModelSeriesDto_Should_Handle_Null_AuthorName()
        {
            // Arrange
            var dto = new ModelSeriesDto
            {
                Id = 1,
                AuthorId = 1,
                AuthorName = null, // Author not loaded
                Name = "Test Series",
                Description = "Test",
                TokenizerType = TokenizerType.O200KBase,
                Parameters = "{}"
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<ModelSeriesDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.AuthorName.Should().BeNull();
            deserialized.AuthorId.Should().Be(1); // ID still present
        }

        [Fact]
        public void CreateModelSeriesDto_Should_Serialize_Without_Id()
        {
            // Arrange
            var dto = new CreateModelSeriesDto
            {
                AuthorId = 3,
                Name = "Claude",
                Description = "Anthropic's Claude series",
                TokenizerType = TokenizerType.Claude,
                Parameters = "{\"safetyLevel\":\"high\"}"
            };

            // Act
            var json = JsonSerializer.Serialize(dto);

            // Assert
            json.Should().NotContain("\"Id\"");
            json.Should().Contain("\"AuthorId\":3");
            json.Should().Contain("\"Name\":\"Claude\"");
            json.Should().Contain("\"Description\"");
            json.Should().Contain("\"TokenizerType\"");
            json.Should().Contain("\"Parameters\"");
        }

        [Fact]
        public void UpdateModelSeriesDto_Should_Handle_Partial_Updates()
        {
            // Arrange
            var dto = new UpdateModelSeriesDto
            {
                Id = 10,
                Name = "Updated Name",
                Description = null, // Don't update
                TokenizerType = null, // Don't update
                Parameters = "{\"new\":\"config\"}"
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<UpdateModelSeriesDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Id.Should().Be(10);
            deserialized.Name.Should().Be("Updated Name");
            deserialized.Description.Should().BeNull();
            deserialized.TokenizerType.Should().BeNull();
            deserialized.Parameters.Should().Be("{\"new\":\"config\"}");
        }

        [Fact]
        public void SeriesSimpleModelDto_Should_Serialize_Minimal_Fields()
        {
            // Arrange
            var dto = new SeriesSimpleModelDto
            {
                Id = 42,
                Name = "gpt-4-turbo",
                Version = "2024-04-09",
                IsActive = true
            };

            // Act
            var json = JsonSerializer.Serialize(dto);

            // Assert
            json.Should().Contain("\"Id\":42");
            json.Should().Contain("\"Name\":\"gpt-4-turbo\"");
            json.Should().Contain("\"Version\":\"2024-04-09\"");
            json.Should().Contain("\"IsActive\":true");
            // Should not contain other fields
            json.Should().NotContain("ModelSeriesId");
            json.Should().NotContain("Capabilities");
        }

        [Fact]
        public void ModelSeriesDto_Should_Preserve_Complex_JSON_Parameters()
        {
            // Arrange
            var complexParams = @"{
                ""contextLength"": 128000,
                ""architecture"": {
                    ""type"": ""transformer"",
                    ""layers"": 96,
                    ""heads"": 64
                },
                ""training"": {
                    ""datasetSize"": ""13T tokens"",
                    ""hardware"": [""A100"", ""H100""]
                }
            }";

            var dto = new ModelSeriesDto
            {
                Id = 1,
                AuthorId = 1,
                Name = "Test",
                TokenizerType = TokenizerType.Cl100KBase,
                Parameters = complexParams
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<ModelSeriesDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Parameters.Should().Be(complexParams);
        }

        [Theory]
        [InlineData(TokenizerType.Cl100KBase)]
        [InlineData(TokenizerType.P50KBase)]
        [InlineData(TokenizerType.P50KEdit)]
        [InlineData(TokenizerType.R50KBase)]
        [InlineData(TokenizerType.Claude)]
        [InlineData(TokenizerType.O200KBase)]
        [InlineData(TokenizerType.LLaMA)]
        [InlineData(TokenizerType.BPE)]
        public void ModelSeriesDto_Should_Serialize_All_TokenizerTypes(TokenizerType tokenizerType)
        {
            // Arrange
            var dto = new ModelSeriesDto
            {
                Id = 1,
                AuthorId = 1,
                Name = "Test",
                TokenizerType = tokenizerType,
                Parameters = "{}"
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<ModelSeriesDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.TokenizerType.Should().Be(tokenizerType);
        }

        [Fact]
        public void ModelSeriesDto_Should_Handle_Unicode_In_Description()
        {
            // Arrange
            var dto = new ModelSeriesDto
            {
                Id = 1,
                AuthorId = 1,
                Name = "Test",
                Description = "支持中文 🚀 Multi-language ñ é ü",
                TokenizerType = TokenizerType.BPE,
                Parameters = "{}"
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<ModelSeriesDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Description.Should().Be("支持中文 🚀 Multi-language ñ é ü");
        }

        [Fact]
        public void ModelSeriesDto_Should_Handle_Empty_Parameters()
        {
            // Arrange
            var dto = new ModelSeriesDto
            {
                Id = 1,
                AuthorId = 1,
                Name = "Test",
                TokenizerType = TokenizerType.BPE,
                Parameters = "" // Empty but not null
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<ModelSeriesDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Parameters.Should().BeEmpty();
        }
    }
}
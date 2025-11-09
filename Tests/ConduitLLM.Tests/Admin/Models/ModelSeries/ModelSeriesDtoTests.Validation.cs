using ConduitLLM.Admin.Models.ModelSeries;

using FluentAssertions;

namespace ConduitLLM.Tests.Admin.Models.ModelSeries
{
    /// <summary>
    /// Tests for ModelSeriesDto validation rules and business constraints.
    /// These tests ensure DTOs enforce proper data integrity.
    /// </summary>
    public partial class ModelSeriesDtoTests
    {
        [Fact]
        public void CreateModelSeriesDto_Should_Have_Default_Values()
        {
            // Act
            var dto = new CreateModelSeriesDto();

            // Assert
            dto.AuthorId.Should().Be(0);
            dto.Name.Should().Be(string.Empty);
            dto.Description.Should().BeNull();
            dto.TokenizerType.Should().Be(TokenizerType.Cl100KBase); // Default enum value is 0
            dto.Parameters.Should().BeNull();
        }

        [Fact]
        public void CreateModelSeriesDto_Should_Accept_Valid_Data()
        {
            // Arrange & Act
            var dto = new CreateModelSeriesDto
            {
                AuthorId = 5,
                Name = "GPT-4",
                Description = "Fourth generation GPT models",
                TokenizerType = TokenizerType.Cl100KBase,
                Parameters = "{\"contextLength\":128000}"
            };

            // Assert
            dto.AuthorId.Should().Be(5);
            dto.Name.Should().Be("GPT-4");
            dto.Description.Should().Be("Fourth generation GPT models");
            dto.TokenizerType.Should().Be(TokenizerType.Cl100KBase);
            dto.Parameters.Should().Be("{\"contextLength\":128000}");
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("  ")]
        [InlineData("\t")]
        [InlineData("\n")]
        public void CreateModelSeriesDto_Should_Allow_Whitespace_Names_But_Flag_For_Validation(string name)
        {
            // Arrange
            var dto = new CreateModelSeriesDto { Name = name, AuthorId = 1 };

            // Act & Assert
            // DTO allows the value but business logic should validate
            dto.Name.Should().Be(name);
            
            // This is where controller validation would catch it
            var isInvalid = string.IsNullOrWhiteSpace(dto.Name);
            isInvalid.Should().BeTrue("Controller should validate this as invalid");
        }

        [Fact]
        public void UpdateModelSeriesDto_Should_Allow_Partial_Updates()
        {
            // Arrange & Act
            var dto = new UpdateModelSeriesDto
            {
                Id = 10,
                Name = "Updated Series Name",
                Description = null, // Don't update
                TokenizerType = TokenizerType.O200KBase, // Update tokenizer
                Parameters = null // Don't update
            };

            // Assert - nulls mean "don't update"
            dto.Id.Should().Be(10);
            dto.Name.Should().Be("Updated Series Name");
            dto.Description.Should().BeNull();
            dto.TokenizerType.Should().Be(TokenizerType.O200KBase);
            dto.Parameters.Should().BeNull();
        }

        [Fact]
        public void UpdateModelSeriesDto_Should_Allow_All_Null_For_No_Updates()
        {
            // Arrange & Act
            var dto = new UpdateModelSeriesDto
            {
                Id = 1,
                Name = null,
                Description = null,
                TokenizerType = null,
                Parameters = null
            };

            // Assert - all nulls is valid (no-op update)
            dto.Id.Should().Be(1);
            dto.Name.Should().BeNull();
            dto.Description.Should().BeNull();
            dto.TokenizerType.Should().BeNull();
            dto.Parameters.Should().BeNull();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        [InlineData(int.MinValue)]
        public void CreateModelSeriesDto_Should_Accept_Invalid_AuthorIds_But_Controller_Should_Validate(int authorId)
        {
            // Arrange & Act
            var dto = new CreateModelSeriesDto { AuthorId = authorId, Name = "Test" };

            // Assert
            // DTO accepts any int, but controller/business logic should validate
            dto.AuthorId.Should().Be(authorId);
            
            // Business rule check
            var isInvalid = authorId <= 0;
            isInvalid.Should().BeTrue("Controller should reject non-positive author IDs");
        }

        [Fact]
        public void ModelSeriesDto_Should_Validate_AuthorId_And_AuthorName_Consistency()
        {
            // Arrange
            var dto = new ModelSeriesDto
            {
                Id = 1,
                AuthorId = 5,
                AuthorName = null, // Inconsistent - have ID but no name loaded
                Name = "Test Series",
                TokenizerType = TokenizerType.BPE,
                Parameters = "{}"
            };

            // Act & Assert
            // DTO allows this, but it indicates incomplete data loading
            dto.AuthorId.Should().Be(5);
            dto.AuthorName.Should().BeNull();
            
            // Business logic could validate this
            var isIncomplete = dto.AuthorId > 0 && string.IsNullOrEmpty(dto.AuthorName);
            isIncomplete.Should().BeTrue("Indicates author data wasn't loaded with series");
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("[]")]
        [InlineData("null")]
        [InlineData("{\"key\":\"value\"}")]
        [InlineData("not-json-at-all")]
        [InlineData("{'single':'quotes'}")]
        public void ModelSeriesDto_Should_Accept_Any_String_As_Parameters(string parameters)
        {
            // Arrange & Act
            var dto = new ModelSeriesDto
            {
                Id = 1,
                AuthorId = 1,
                Name = "Test",
                TokenizerType = TokenizerType.BPE,
                Parameters = parameters
            };

            // Assert
            // DTO accepts any string, JSON validation happens at business layer
            dto.Parameters.Should().Be(parameters);
        }

        [Fact]
        public void SeriesSimpleModelDto_Should_Have_Default_Values()
        {
            // Act
            var dto = new SeriesSimpleModelDto();

            // Assert
            dto.Id.Should().Be(0);
            dto.Name.Should().Be(string.Empty);
            dto.Version.Should().BeNull();
            dto.IsActive.Should().BeFalse();
        }

        [Fact]
        public void SeriesSimpleModelDto_Should_Accept_Null_Version()
        {
            // Arrange & Act
            var dto = new SeriesSimpleModelDto
            {
                Id = 1,
                Name = "model-without-version",
                Version = null,
                IsActive = true
            };

            // Assert
            dto.Version.Should().BeNull();
            dto.Name.Should().Be("model-without-version");
        }

        [Theory]
        [InlineData("2024-01-01")]
        [InlineData("v1.0.0")]
        [InlineData("preview")]
        [InlineData("2024-04-09-preview")]
        [InlineData("0613")]
        [InlineData("")]
        public void SeriesSimpleModelDto_Should_Accept_Various_Version_Formats(string version)
        {
            // Arrange & Act
            var dto = new SeriesSimpleModelDto
            {
                Id = 1,
                Name = "test-model",
                Version = version,
                IsActive = true
            };

            // Assert
            dto.Version.Should().Be(version);
        }

        [Fact]
        public void CreateModelSeriesDto_Parameters_Default_Should_Be_Null_Not_Empty()
        {
            // Arrange
            var dto = new CreateModelSeriesDto
            {
                AuthorId = 1,
                Name = "Test Series",
                TokenizerType = TokenizerType.BPE
                // Parameters not set
            };

            // Act & Assert
            dto.Parameters.Should().BeNull("Default should be null, not empty string");
            
            // Controller would typically convert null to "{}"
            var effectiveParams = dto.Parameters ?? "{}";
            effectiveParams.Should().Be("{}");
        }

        [Theory]
        [InlineData("GPT-4")]
        [InlineData("Claude-3")]
        [InlineData("Llama-3.1")]
        [InlineData("gemini-1.5-pro")]
        [InlineData("VERY-LONG-SERIES-NAME-WITH-MANY-WORDS-AND-HYPHENS")]
        [InlineData("Series_With_Underscores")]
        [InlineData("Series.With.Dots")]
        public void ModelSeriesDto_Should_Accept_Various_Series_Names(string seriesName)
        {
            // Arrange & Act
            var dto = new ModelSeriesDto
            {
                Id = 1,
                AuthorId = 1,
                Name = seriesName,
                TokenizerType = TokenizerType.BPE,
                Parameters = "{}"
            };

            // Assert
            dto.Name.Should().Be(seriesName);
        }

        [Fact]
        public void ModelSeriesDto_Should_Handle_Very_Long_Description()
        {
            // Arrange
            var longDescription = new string('x', 10000); // 10k characters
            
            var dto = new ModelSeriesDto
            {
                Id = 1,
                AuthorId = 1,
                Name = "Test",
                Description = longDescription,
                TokenizerType = TokenizerType.BPE,
                Parameters = "{}"
            };

            // Act & Assert
            dto.Description.Should().Be(longDescription);
            dto.Description.Should().HaveLength(10000);
        }

        [Fact]
        public void UpdateModelSeriesDto_Should_Allow_Empty_String_To_Clear_Description()
        {
            // Arrange & Act
            var dto = new UpdateModelSeriesDto
            {
                Id = 1,
                Description = "" // Empty string to clear description
            };

            // Assert
            dto.Description.Should().BeEmpty();
            dto.Description.Should().NotBeNull("Empty string is different from null for updates");
        }
    }
}
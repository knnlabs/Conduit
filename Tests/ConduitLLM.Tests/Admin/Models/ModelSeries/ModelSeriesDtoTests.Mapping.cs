using ConduitLLM.Admin.Models.ModelSeries;
using ConduitLLM.Configuration.Entities;

using FluentAssertions;

namespace ConduitLLM.Tests.Admin.Models.ModelSeries
{
    /// <summary>
    /// Tests for entity-to-DTO and DTO-to-entity mapping logic.
    /// These tests ensure data is correctly transformed between layers.
    /// </summary>
    public partial class ModelSeriesDtoTests
    {
        [Fact]
        public void Should_Map_Entity_To_ModelSeriesDto_Correctly()
        {
            // Arrange
            var entity = new ConduitLLM.Configuration.Entities.ModelSeries
            {
                Id = 5,
                AuthorId = 2,
                Author = new ModelAuthor
                {
                    Id = 2,
                    Name = "OpenAI",
                    WebsiteUrl = "https://openai.com",
                    Description = "OpenAI research organization"
                },
                Name = "GPT-4",
                Description = "Fourth generation GPT models",
                TokenizerType = TokenizerType.Cl100KBase,
                Parameters = "{\"contextLength\":128000}"
            };

            // Act - simulate the mapping logic from controller
            var dto = MapEntityToDto(entity);

            // Assert
            dto.Should().NotBeNull();
            dto.Id.Should().Be(entity.Id);
            dto.AuthorId.Should().Be(entity.AuthorId);
            dto.AuthorName.Should().Be("OpenAI");
            dto.Name.Should().Be(entity.Name);
            dto.Description.Should().Be(entity.Description);
            dto.TokenizerType.Should().Be(entity.TokenizerType);
            dto.Parameters.Should().Be(entity.Parameters);
        }

        [Fact]
        public void Should_Map_Entity_With_Null_Author_To_Dto()
        {
            // Arrange
            var entity = new ConduitLLM.Configuration.Entities.ModelSeries
            {
                Id = 1,
                AuthorId = 1,
                Author = null, // Author not loaded
                Name = "Test Series",
                Description = "Test",
                TokenizerType = TokenizerType.BPE,
                Parameters = "{}"
            };

            // Act
            var dto = MapEntityToDto(entity);

            // Assert
            dto.Should().NotBeNull();
            dto.AuthorId.Should().Be(1);
            dto.AuthorName.Should().BeNull();
        }

        [Fact]
        public void Should_Map_CreateModelSeriesDto_To_Entity()
        {
            // Arrange
            var createDto = new CreateModelSeriesDto
            {
                AuthorId = 3,
                Name = "Claude",
                Description = "Anthropic's Claude series",
                TokenizerType = TokenizerType.Claude,
                Parameters = "{\"safetyLevel\":\"high\"}"
            };

            // Act - simulate controller logic
            var entity = new ConduitLLM.Configuration.Entities.ModelSeries
            {
                AuthorId = createDto.AuthorId,
                Name = createDto.Name,
                Description = createDto.Description,
                TokenizerType = createDto.TokenizerType,
                Parameters = createDto.Parameters ?? "{}"
            };

            // Assert
            entity.AuthorId.Should().Be(createDto.AuthorId);
            entity.Name.Should().Be(createDto.Name);
            entity.Description.Should().Be(createDto.Description);
            entity.TokenizerType.Should().Be(TokenizerType.Claude);
            entity.Parameters.Should().Be("{\"safetyLevel\":\"high\"}");
        }

        [Fact]
        public void Should_Apply_UpdateModelSeriesDto_To_Entity_With_Partial_Updates()
        {
            // Arrange
            var existingEntity = new ConduitLLM.Configuration.Entities.ModelSeries
            {
                Id = 10,
                AuthorId = 1,
                Name = "original-name",
                Description = "original description",
                TokenizerType = TokenizerType.P50KBase,
                Parameters = "{\"old\":\"params\"}"
            };

            var updateDto = new UpdateModelSeriesDto
            {
                Id = 10,
                Name = "updated-name",  // Update name
                Description = null,      // Don't update description
                TokenizerType = TokenizerType.Cl100KBase, // Update tokenizer
                Parameters = null        // Don't update parameters
            };

            // Act - simulate controller update logic
            if (!string.IsNullOrEmpty(updateDto.Name))
                existingEntity.Name = updateDto.Name;
            if (updateDto.Description != null)
                existingEntity.Description = updateDto.Description;
            if (updateDto.TokenizerType.HasValue)
                existingEntity.TokenizerType = updateDto.TokenizerType.Value;
            if (updateDto.Parameters != null)
                existingEntity.Parameters = updateDto.Parameters;

            // Assert
            existingEntity.Name.Should().Be("updated-name");
            existingEntity.Description.Should().Be("original description"); // Unchanged
            existingEntity.TokenizerType.Should().Be(TokenizerType.Cl100KBase); // Updated
            existingEntity.Parameters.Should().Be("{\"old\":\"params\"}"); // Unchanged
        }

        [Fact]
        public void Should_Map_Models_In_Series_To_SimpleModelDtos()
        {
            // Arrange
            var models = new List<Model>
            {
                new Model
                {
                    Id = 1,
                    Name = "gpt-4",
                    Version = "0613",
                    IsActive = true,
                    ModelSeriesId = 1
                },
                new Model
                {
                    Id = 2,
                    Name = "gpt-4-turbo",
                    Version = "2024-04-09",
                    IsActive = true,
                    ModelSeriesId = 1
                },
                new Model
                {
                    Id = 3,
                    Name = "gpt-4-turbo-preview",
                    Version = null,
                    IsActive = false,
                    ModelSeriesId = 1
                }
            };

            // Act - simulate controller logic
            var dtos = models.Select(m => new SeriesSimpleModelDto
            {
                Id = m.Id,
                Name = m.Name,
                Version = m.Version,
                IsActive = m.IsActive
            }).ToList();

            // Assert
            dtos.Should().HaveCount(3);
            
            dtos[0].Id.Should().Be(1);
            dtos[0].Name.Should().Be("gpt-4");
            dtos[0].Version.Should().Be("0613");
            dtos[0].IsActive.Should().BeTrue();
            
            dtos[1].Id.Should().Be(2);
            dtos[1].Name.Should().Be("gpt-4-turbo");
            dtos[1].Version.Should().Be("2024-04-09");
            dtos[1].IsActive.Should().BeTrue();
            
            dtos[2].Id.Should().Be(3);
            dtos[2].Name.Should().Be("gpt-4-turbo-preview");
            dtos[2].Version.Should().BeNull();
            dtos[2].IsActive.Should().BeFalse();
        }

        [Fact]
        public void Should_Handle_Name_Conflict_Detection_During_Update()
        {
            // Arrange
            var existingSeries1 = new ConduitLLM.Configuration.Entities.ModelSeries
            {
                Id = 1,
                AuthorId = 1,
                Name = "GPT-3.5"
            };

            var existingSeries2 = new ConduitLLM.Configuration.Entities.ModelSeries
            {
                Id = 2,
                AuthorId = 1,
                Name = "GPT-4"
            };

            var updateDto = new UpdateModelSeriesDto
            {
                Id = 1,
                Name = "GPT-4" // Trying to rename to existing name
            };

            // Act - simulate controller conflict check
            var wouldConflict = !string.IsNullOrEmpty(updateDto.Name) 
                && updateDto.Name != existingSeries1.Name
                && updateDto.Name == existingSeries2.Name
                && existingSeries1.AuthorId == existingSeries2.AuthorId;

            // Assert
            wouldConflict.Should().BeTrue("Should detect naming conflict within same author");
        }

        [Fact]
        public void Should_Apply_CreateDto_With_Null_Parameters_Using_Default()
        {
            // Arrange
            var createDto = new CreateModelSeriesDto
            {
                AuthorId = 1,
                Name = "Test Series",
                TokenizerType = TokenizerType.BPE,
                Parameters = null // Not provided
            };

            // Act - simulate controller logic
            var entity = new ConduitLLM.Configuration.Entities.ModelSeries
            {
                AuthorId = createDto.AuthorId,
                Name = createDto.Name,
                Description = createDto.Description,
                TokenizerType = createDto.TokenizerType,
                Parameters = createDto.Parameters ?? "{}" // Default to empty JSON
            };

            // Assert
            entity.Parameters.Should().Be("{}");
        }

        [Fact]
        public void Should_Clear_Description_When_UpdateDto_Has_Empty_String()
        {
            // Arrange
            var existingEntity = new ConduitLLM.Configuration.Entities.ModelSeries
            {
                Id = 1,
                AuthorId = 1,
                Name = "Test",
                Description = "Original description",
                TokenizerType = TokenizerType.BPE,
                Parameters = "{}"
            };

            var updateDto = new UpdateModelSeriesDto
            {
                Id = 1,
                Description = "" // Empty string means clear
            };

            // Act - simulate controller update logic
            if (updateDto.Description != null) // Empty string is not null
                existingEntity.Description = updateDto.Description;

            // Assert
            existingEntity.Description.Should().BeEmpty();
        }

        [Fact]
        public void Should_Not_Update_Description_When_UpdateDto_Has_Null()
        {
            // Arrange
            var existingEntity = new ConduitLLM.Configuration.Entities.ModelSeries
            {
                Id = 1,
                AuthorId = 1,
                Name = "Test",
                Description = "Original description",
                TokenizerType = TokenizerType.BPE,
                Parameters = "{}"
            };

            var updateDto = new UpdateModelSeriesDto
            {
                Id = 1,
                Description = null // Null means don't update
            };

            // Act - simulate controller update logic
            if (updateDto.Description != null)
                existingEntity.Description = updateDto.Description;

            // Assert
            existingEntity.Description.Should().Be("Original description");
        }

        // Helper method that mirrors controller mapping logic
        private static ModelSeriesDto MapEntityToDto(ConduitLLM.Configuration.Entities.ModelSeries entity)
        {
            return new ModelSeriesDto
            {
                Id = entity.Id,
                AuthorId = entity.AuthorId,
                AuthorName = entity.Author?.Name,
                Name = entity.Name,
                Description = entity.Description,
                TokenizerType = entity.TokenizerType,
                Parameters = entity.Parameters
            };
        }
    }
}
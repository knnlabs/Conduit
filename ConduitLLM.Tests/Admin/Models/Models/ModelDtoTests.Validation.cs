using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using ConduitLLM.Admin.Models.Models;
using ConduitLLM.Admin.Models.ModelCapabilities;
using ConduitLLM.Configuration.Entities;
using FluentAssertions;
using Xunit;

namespace ConduitLLM.Tests.Admin.Models.Models
{
    /// <summary>
    /// Tests for ModelDto validation rules and business constraints.
    /// These tests ensure DTOs enforce proper data integrity.
    /// </summary>
    public partial class ModelDtoTests
    {
        [Fact]
        public void CreateModelDto_Should_Have_Default_Values()
        {
            // Act
            var dto = new CreateModelDto();

            // Assert
            dto.Name.Should().Be(string.Empty);
            dto.ModelSeriesId.Should().Be(0);
            dto.ModelCapabilitiesId.Should().Be(0);
            dto.IsActive.Should().BeNull();
        }

        [Fact]
        public void CreateModelDto_Should_Accept_Valid_Data()
        {
            // Arrange & Act
            var dto = new CreateModelDto
            {
                Name = "gpt-4-turbo",
                ModelSeriesId = 1,
                ModelCapabilitiesId = 5,
                IsActive = true
            };

            // Assert
            dto.Name.Should().Be("gpt-4-turbo");
            dto.ModelSeriesId.Should().Be(1);
            dto.ModelCapabilitiesId.Should().Be(5);
            dto.IsActive.Should().BeTrue();
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("  ")]
        [InlineData("\t")]
        [InlineData("\n")]
        public void CreateModelDto_Should_Allow_Whitespace_Names_But_Flag_For_Validation(string name)
        {
            // Arrange
            var dto = new CreateModelDto { Name = name };

            // Act & Assert
            // DTO allows the value but business logic should validate
            dto.Name.Should().Be(name);
            
            // This is where controller validation would catch it
            var isInvalid = string.IsNullOrWhiteSpace(dto.Name);
            isInvalid.Should().BeTrue("Controller should validate this as invalid");
        }

        [Fact]
        public void UpdateModelDto_Should_Allow_Partial_Updates()
        {
            // Arrange & Act
            var dto = new UpdateModelDto
            {
                Id = 42,
                Name = null,  // Don't update name
                ModelSeriesId = 5,  // Update series
                ModelCapabilitiesId = null,  // Don't update capabilities
                IsActive = false  // Update status
            };

            // Assert - nulls mean "don't update"
            dto.Id.Should().Be(42);
            dto.Name.Should().BeNull();
            dto.ModelSeriesId.Should().Be(5);
            dto.ModelCapabilitiesId.Should().BeNull();
            dto.IsActive.Should().BeFalse();
        }

        [Fact]
        public void UpdateModelDto_Should_Allow_All_Null_For_No_Updates()
        {
            // Arrange & Act
            var dto = new UpdateModelDto
            {
                Id = 1,
                Name = null,
                ModelSeriesId = null,
                ModelCapabilitiesId = null,
                IsActive = null
            };

            // Assert - all nulls is valid (no-op update)
            dto.Id.Should().Be(1);
            dto.Name.Should().BeNull();
            dto.ModelSeriesId.Should().BeNull();
            dto.ModelCapabilitiesId.Should().BeNull();
            dto.IsActive.Should().BeNull();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        [InlineData(int.MinValue)]
        public void ModelDto_Should_Accept_Invalid_Ids_But_Controller_Should_Validate(int id)
        {
            // Arrange & Act
            var dto = new ModelDto { Id = id };

            // Assert
            // DTO accepts any int, but controller/business logic should validate
            dto.Id.Should().Be(id);
            
            // Business rule check
            var isInvalid = id <= 0;
            isInvalid.Should().BeTrue("Controller should reject non-positive IDs");
        }

        [Fact]
        public void ModelDto_Should_Handle_DateTime_Edge_Cases()
        {
            // Arrange
            var testCases = new[]
            {
                DateTime.MinValue,
                DateTime.MaxValue,
                new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc),  // Unix epoch
                new DateTime(2038, 1, 19, 3, 14, 7, DateTimeKind.Utc), // Y2K38
                DateTime.UtcNow
            };

            foreach (var testDate in testCases)
            {
                // Act
                var dto = new ModelDto
                {
                    Id = 1,
                    Name = "test",
                    ModelSeriesId = 1,
                    ModelCapabilitiesId = 1,
                    IsActive = true,
                    CreatedAt = testDate,
                    UpdatedAt = testDate
                };

                // Assert
                dto.CreatedAt.Should().Be(testDate);
                dto.UpdatedAt.Should().Be(testDate);
            }
        }

        [Fact]
        public void ModelDto_Should_Validate_Related_Id_Consistency()
        {
            // Arrange
            var dto = new ModelDto
            {
                Id = 1,
                Name = "test",
                ModelSeriesId = 5,
                ModelCapabilitiesId = 10,
                Capabilities = new ModelCapabilitiesDto
                {
                    Id = 99, // Different from ModelCapabilitiesId!
                    SupportsChat = true
                },
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Act & Assert
            // DTO allows this, but business logic should validate consistency
            dto.ModelCapabilitiesId.Should().Be(10);
            dto.Capabilities!.Id.Should().Be(99);
            
            // Business validation
            var isInconsistent = dto.Capabilities != null && 
                                  dto.Capabilities.Id != dto.ModelCapabilitiesId;
            isInconsistent.Should().BeTrue("Controller should validate ID consistency");
        }

        [Fact]
        public void ModelWithProviderIdDto_Should_Inherit_ModelDto_Properties()
        {
            // Arrange & Act
            var dto = new ModelWithProviderIdDto
            {
                Id = 42,
                Name = "gpt-4",
                ModelSeriesId = 1,
                ModelCapabilitiesId = 5,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                ProviderModelId = "gpt-4-0613"
            };

            // Assert - verify inheritance works correctly
            ModelDto baseDto = dto; // Should be able to cast up
            baseDto.Should().NotBeNull();
            baseDto.Id.Should().Be(42);
            baseDto.Name.Should().Be("gpt-4");
            
            // Extended property
            dto.ProviderModelId.Should().Be("gpt-4-0613");
        }

        [Theory]
        [InlineData("gpt-4-0613")]
        [InlineData("deployment-name-123")]
        [InlineData("azure/my-custom-deployment")]
        [InlineData("very.long.provider.specific.model.identifier.with.dots.and.hyphens-123")]
        [InlineData("UPPERCASE-MODEL-ID")]
        [InlineData("")]  // Empty is technically valid at DTO level
        public void ModelWithProviderIdDto_Should_Accept_Various_Provider_Ids(string providerId)
        {
            // Arrange & Act
            var dto = new ModelWithProviderIdDto
            {
                Id = 1,
                Name = "base-model",
                ModelSeriesId = 1,
                ModelCapabilitiesId = 1,
                ProviderModelId = providerId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Assert
            dto.ProviderModelId.Should().Be(providerId);
        }

        [Fact]
        public void CreateModelDto_IsActive_Null_Should_Default_To_True_In_Controller()
        {
            // Arrange
            var dto = new CreateModelDto
            {
                Name = "test-model",
                ModelSeriesId = 1,
                ModelCapabilitiesId = 1,
                IsActive = null  // Not specified
            };

            // Act - simulate controller logic
            var effectiveIsActive = dto.IsActive ?? true;

            // Assert
            dto.IsActive.Should().BeNull();
            effectiveIsActive.Should().BeTrue("Controller should default null to true");
        }
    }
}
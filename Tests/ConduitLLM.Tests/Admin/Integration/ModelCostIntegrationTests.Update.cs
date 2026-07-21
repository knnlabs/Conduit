using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Tests.Admin.Integration
{
    /// <summary>
    /// Update model cost tests for ModelCostIntegrationTests
    /// </summary>
    public partial class ModelCostIntegrationTests
    {
        #region Update Model Cost Tests

        [Fact]
        public async Task UpdateModelCost_ChangeMappings_ShouldUpdateCorrectly()
        {
            // Arrange
            await SetupTestDataAsync();
            
            // Create Models
            _dbContext.Models.AddRange(
                new Model { Id = 30, Name = "GPT-4", ModelSeriesId = 1 },
                new Model { Id = 31, Name = "GPT-3.5 Turbo", ModelSeriesId = 1 },
                new Model { Id = 32, Name = "Embedding Ada", ModelSeriesId = 1 }
            );
            await _dbContext.SaveChangesAsync();
            
            // Create ModelProviderTypeAssociations
            _dbContext.ModelProviderTypeAssociations.AddRange(
                new ModelProviderTypeAssociation { Id = 30, Identifier = "gpt-4", ModelId = 30, IsEnabled = true },
                new ModelProviderTypeAssociation { Id = 31, Identifier = "gpt-3.5-turbo", ModelId = 31, IsEnabled = true },
                new ModelProviderTypeAssociation { Id = 32, Identifier = "text-embedding-ada-002", ModelId = 32, IsEnabled = true }
            );
            await _dbContext.SaveChangesAsync();

            // Create initial cost with mappings
            var createDto = new CreateModelCostDto
            {
                CostName = "Test Pricing",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                ModelProviderTypeAssociationIds = new List<int> { 30, 31 }
            };
            
            var createResult = await _controller.CreateModelCost(createDto);
            var createdResult = createResult.Should().BeOfType<CreatedAtActionResult>().Subject;
            var createdCost = createdResult.Value.Should().BeOfType<ModelCostDto>().Subject;

            // Update with different mappings
            var updateDto = new UpdateModelCostDto
            {
                CostName = "Updated Pricing",
                InputCostPerMillionTokens = 15.00m,
                OutputCostPerMillionTokens = 25.00m,
                ModelProviderTypeAssociationIds = new List<int> { 31, 32 }
            };

            // Act
            var updateResult = await _controller.UpdateModelCost(createdCost.Id, updateDto);

            // Assert
            updateResult.Should().BeOfType<OkObjectResult>();

            // Verify updated mappings
            var updatedCost = await _modelCostRepository.GetByIdAsync(createdCost.Id);
            updatedCost.Should().NotBeNull();
            updatedCost!.CostName.Should().Be("Updated Pricing");
            updatedCost.ModelProviderTypeAssociations.Should().HaveCount(2);
            
            var actualAssociationIds = updatedCost.ModelProviderTypeAssociations.Select(a => a.Id).OrderBy(id => id).ToList();
            actualAssociationIds.Should().BeEquivalentTo(new List<int> { 31, 32 });
        }

        [Fact]
        public async Task UpdateModelCost_RemoveAllMappings_ShouldClearAssociations()
        {
            // Arrange
            await SetupTestDataAsync();
            var mappings = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                _modelMappingRepository.GetPaginatedAsync);
            var mappingIds = mappings.Select(m => m.Id).ToList();

            // Create cost with mappings
            var createDto = new CreateModelCostDto
            {
                CostName = "Test Cost",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                ModelProviderTypeAssociationIds = mappingIds
            };
            
            var createResult = await _controller.CreateModelCost(createDto);
            var createdResult = createResult.Should().BeOfType<CreatedAtActionResult>().Subject;
            var createdCost = createdResult.Value.Should().BeOfType<ModelCostDto>().Subject;

            // Update to remove all mappings
            var updateDto = new UpdateModelCostDto
            {
                CostName = createdCost.CostName,
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                ModelProviderTypeAssociationIds = new List<int>() // Empty list
            };

            // Act
            var updateResult = await _controller.UpdateModelCost(createdCost.Id, updateDto);

            // Assert
            updateResult.Should().BeOfType<OkObjectResult>();

            // Verify mappings removed
            using (var verifyContext = new ConduitDbContext(_dbContextOptions))
            {
                var dbMappings = verifyContext.ModelProviderTypeAssociations
                    .Where(m => m.ModelCostId == createdCost.Id)
                    .ToList();
                dbMappings.Should().BeEmpty();
            }
        }

        #endregion
    }
}

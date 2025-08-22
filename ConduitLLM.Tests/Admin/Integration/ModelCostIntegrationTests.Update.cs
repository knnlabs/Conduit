using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;

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
            var providerId = await SetupTestDataAsync();
            var allMappings = await _modelMappingRepository.GetAllAsync();
            var initialMappingIds = allMappings.Select(m => m.Id).Take(2).ToList();
            var newMappingIds = allMappings.Select(m => m.Id).Skip(1).Take(2).ToList();

            // Create initial cost with mappings
            var createDto = new CreateModelCostDto
            {
                CostName = "Test Pricing",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                ModelProviderMappingIds = initialMappingIds
            };
            
            var createResult = await _controller.CreateModelCost(createDto);
            var createdCost = (createResult as CreatedAtActionResult)?.Value as ModelCostDto;
            createdCost.Should().NotBeNull();

            // Update with different mappings
            var updateDto = new UpdateModelCostDto
            {
                Id = createdCost!.Id,
                CostName = "Updated Pricing",
                InputCostPerMillionTokens = 15.00m,
                OutputCostPerMillionTokens = 25.00m,
                ModelProviderMappingIds = newMappingIds
            };

            // Act
            var updateResult = await _controller.UpdateModelCost(createdCost.Id, updateDto);

            // Assert
            Assert.IsType<NoContentResult>(updateResult);

            // Verify updated mappings
            var updatedCost = await _modelCostRepository.GetByIdAsync(createdCost.Id);
            updatedCost.Should().NotBeNull();
            updatedCost!.CostName.Should().Be("Updated Pricing");
            updatedCost.ModelCostMappings.Should().HaveCount(2);
            
            var actualMappingIds = updatedCost.ModelCostMappings.Select(m => m.ModelProviderMappingId).ToList();
            actualMappingIds.Should().BeEquivalentTo(newMappingIds);
        }

        [Fact]
        public async Task UpdateModelCost_RemoveAllMappings_ShouldClearAssociations()
        {
            // Arrange
            var providerId = await SetupTestDataAsync();
            var mappings = await _modelMappingRepository.GetAllAsync();
            var mappingIds = mappings.Select(m => m.Id).ToList();

            // Create cost with mappings
            var createDto = new CreateModelCostDto
            {
                CostName = "Test Cost",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                ModelProviderMappingIds = mappingIds
            };
            
            var createResult = await _controller.CreateModelCost(createDto);
            var createdCost = (createResult as CreatedAtActionResult)?.Value as ModelCostDto;

            // Update to remove all mappings
            var updateDto = new UpdateModelCostDto
            {
                Id = createdCost!.Id,
                CostName = createdCost.CostName,
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                ModelProviderMappingIds = new List<int>() // Empty list
            };

            // Act
            var updateResult = await _controller.UpdateModelCost(createdCost.Id, updateDto);

            // Assert
            Assert.IsType<NoContentResult>(updateResult);

            // Verify mappings removed
            using (var verifyContext = new ConduitDbContext(_dbContextOptions))
            {
                var dbMappings = verifyContext.ModelCostMappings
                    .Where(m => m.ModelCostId == createdCost.Id)
                    .ToList();
                dbMappings.Should().BeEmpty();
            }
        }

        #endregion
    }
}
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Tests.Admin.Integration
{
    /// <summary>
    /// Edge case tests for ModelCostIntegrationTests
    /// </summary>
    public partial class ModelCostIntegrationTests
    {
        #region Edge Cases

        [Fact]
        public async Task CreateModelCost_WithInvalidMappingIds_ShouldStillCreate()
        {
            // This test verifies that invalid mapping IDs don't break the creation
            // The cost should be created but without the invalid mappings

            // Arrange
            await SetupTestDataAsync();
            
            var createDto = new CreateModelCostDto
            {
                CostName = "Cost with Invalid Mappings",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                ModelProviderTypeAssociationIds = new List<int> { 9999, 10000 } // Non-existent IDs
            };

            // Act
            var result = await _controller.CreateModelCost(createDto);

            // Assert
            var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            var createdCost = createdResult.Value.Should().BeOfType<ModelCostDto>().Subject;

            createdCost.CostName.Should().Be("Cost with Invalid Mappings");
            createdCost.AssociatedModelAliases.Should().BeEmpty(); // No valid mappings

            // Verify in database
            using (var verifyContext = new ConduitDbContext(_dbContextOptions))
            {
                var dbMappings = verifyContext.ModelProviderTypeAssociations
                    .Where(m => m.ModelCostId == createdCost.Id)
                    .ToList();
                dbMappings.Should().BeEmpty(); // No associations should be linked since IDs were invalid
            }
        }

        [Fact]
        public async Task UpdateModelCost_ConcurrentUpdates_LastWriteWins()
        {
            // This test simulates concurrent updates to verify data consistency
            
            // Arrange
            await SetupTestDataAsync();
            
            var createDto = new CreateModelCostDto
            {
                CostName = "Concurrent Test",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m
            };
            
            var createResult = await _controller.CreateModelCost(createDto);
            var createdResult = createResult.Should().BeOfType<CreatedAtActionResult>().Subject;
            var createdCost = createdResult.Value.Should().BeOfType<ModelCostDto>().Subject;

            // Prepare two concurrent updates
            var update1 = new UpdateModelCostDto
            {
                Id = createdCost.Id,
                CostName = "Update 1",
                InputCostPerMillionTokens = 15.00m,
                OutputCostPerMillionTokens = 25.00m
            };

            var update2 = new UpdateModelCostDto
            {
                Id = createdCost.Id,
                CostName = "Update 2",
                InputCostPerMillionTokens = 20.00m,
                OutputCostPerMillionTokens = 30.00m
            };

            // Act - Execute updates sequentially (simulating near-concurrent execution)
            await _controller.UpdateModelCost(createdCost.Id, update1);
            await _controller.UpdateModelCost(createdCost.Id, update2);

            // Assert - Last update should win
            var finalCost = await _modelCostRepository.GetByIdAsync(createdCost.Id);
            finalCost.Should().NotBeNull();
            finalCost!.CostName.Should().Be("Update 2");
            finalCost.InputCostPerMillionTokens.Should().Be(20.00m);
            finalCost.OutputCostPerMillionTokens.Should().Be(30.00m);
        }

        #endregion
    }
}
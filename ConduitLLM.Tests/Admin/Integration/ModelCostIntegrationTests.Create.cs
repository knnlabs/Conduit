using ConduitLLM.Configuration.DTOs;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Tests.Admin.Integration
{
    /// <summary>
    /// Create model cost tests for ModelCostIntegrationTests
    /// </summary>
    public partial class ModelCostIntegrationTests
    {
        #region Create Model Cost Tests

        [Fact]
        public async Task CreateModelCost_WithMappings_ShouldCreateAndAssociate()
        {
            // Arrange
            var providerId = await SetupTestDataAsync();
            var mappings = await _modelMappingRepository.GetAllAsync();
            var mappingIds = mappings.Select(m => m.Id).Take(2).ToList(); // Use first 2 mappings

            var createDto = new CreateModelCostDto
            {
                CostName = "GPT-4 Pricing",
                InputCostPerMillionTokens = 30.00m,
                OutputCostPerMillionTokens = 60.00m,
                ModelProviderMappingIds = mappingIds
            };

            // Act
            var result = await _controller.CreateModelCost(createDto);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var createdCost = Assert.IsType<ModelCostDto>(createdResult.Value);
            
            createdCost.CostName.Should().Be("GPT-4 Pricing");
            createdCost.AssociatedModelAliases.Should().HaveCount(2);
            createdCost.AssociatedModelAliases.Should().Contain(new[] { "gpt-4", "gpt-3.5-turbo" });

            // Verify in database
            var dbCost = await _modelCostRepository.GetByIdAsync(createdCost.Id);
            dbCost.Should().NotBeNull();
            dbCost!.ModelCostMappings.Should().HaveCount(2);
        }

        [Fact]
        public async Task CreateModelCost_DuplicateName_ShouldReturnBadRequest()
        {
            // Arrange
            await SetupTestDataAsync();
            
            // Create first cost
            var firstCost = new CreateModelCostDto
            {
                CostName = "Standard Pricing",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m
            };
            await _controller.CreateModelCost(firstCost);

            // Try to create duplicate
            var duplicateCost = new CreateModelCostDto
            {
                CostName = "Standard Pricing",
                InputCostPerMillionTokens = 15.00m,
                OutputCostPerMillionTokens = 25.00m
            };

            // Act
            var result = await _controller.CreateModelCost(duplicateCost);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            badRequestResult.Value.Should().Be("A model cost with name 'Standard Pricing' already exists");
        }

        #endregion
    }
}
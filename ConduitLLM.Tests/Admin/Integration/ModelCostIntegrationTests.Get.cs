using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ConduitLLM.Tests.Admin.Integration
{
    /// <summary>
    /// Get model cost tests for ModelCostIntegrationTests
    /// </summary>
    public partial class ModelCostIntegrationTests
    {
        #region Get Model Cost Tests

        [Fact]
        public async Task GetModelCostById_WithMappings_ShouldReturnAssociatedAliases()
        {
            // Arrange
            var providerId = await SetupTestDataAsync();
            var mappings = await _modelMappingRepository.GetAllAsync();
            var mappingIds = mappings.Select(m => m.Id).ToList();

            var createDto = new CreateModelCostDto
            {
                CostName = "Test Cost with Associations",
                InputCostPerMillionTokens = 25.00m,
                OutputCostPerMillionTokens = 50.00m,
                ModelProviderMappingIds = mappingIds
            };
            
            var createResult = await _controller.CreateModelCost(createDto);
            var createdCost = (createResult as CreatedAtActionResult)?.Value as ModelCostDto;

            // Act
            var getResult = await _controller.GetModelCostById(createdCost!.Id);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(getResult);
            var retrievedCost = Assert.IsType<ModelCostDto>(okResult.Value);
            
            retrievedCost.CostName.Should().Be("Test Cost with Associations");
            retrievedCost.AssociatedModelAliases.Should().HaveCount(3);
            retrievedCost.AssociatedModelAliases.Should().Contain(new[] { "gpt-4", "gpt-3.5-turbo", "text-embedding-ada-002" });
        }

        [Fact]
        public async Task GetAllModelCosts_ShouldReturnAllWithAssociations()
        {
            // Arrange
            var providerId = await SetupTestDataAsync();
            var mappings = await _modelMappingRepository.GetAllAsync();
            var mappingsList = mappings.OrderBy(m => m.ModelAlias).ToList(); // Order for predictability

            // Get specific mappings by alias
            var gpt4Mapping = mappingsList.First(m => m.ModelAlias == "gpt-4");
            var gpt35Mapping = mappingsList.First(m => m.ModelAlias == "gpt-3.5-turbo");
            var embeddingMapping = mappingsList.First(m => m.ModelAlias == "text-embedding-ada-002");

            // Create multiple costs with different mappings
            var cost1 = new CreateModelCostDto
            {
                CostName = "Cost 1",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                ModelProviderMappingIds = new List<int> { gpt4Mapping.Id }
            };

            var cost2 = new CreateModelCostDto
            {
                CostName = "Cost 2",
                InputCostPerMillionTokens = 15.00m,
                OutputCostPerMillionTokens = 30.00m,
                ModelProviderMappingIds = new List<int> { gpt35Mapping.Id, embeddingMapping.Id }
            };

            await _controller.CreateModelCost(cost1);
            await _controller.CreateModelCost(cost2);

            // Act
            var result = await _controller.GetAllModelCosts();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var costs = Assert.IsAssignableFrom<IEnumerable<ModelCostDto>>(okResult.Value);
            var costList = costs.ToList();

            costList.Should().HaveCount(2);
            
            var firstCost = costList.First(c => c.CostName == "Cost 1");
            firstCost.AssociatedModelAliases.Should().HaveCount(1);
            firstCost.AssociatedModelAliases.Should().Contain("gpt-4");

            var secondCost = costList.First(c => c.CostName == "Cost 2");
            secondCost.AssociatedModelAliases.Should().HaveCount(2);
            secondCost.AssociatedModelAliases.Should().Contain(new[] { "gpt-3.5-turbo", "text-embedding-ada-002" });
        }

        #endregion
    }
}
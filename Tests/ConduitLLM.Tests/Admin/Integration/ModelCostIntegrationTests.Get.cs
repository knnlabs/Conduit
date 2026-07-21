using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;

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
            
            // Create Models first
            _dbContext.Models.AddRange(
                new Model { Id = 10, Name = "GPT-4", ModelSeriesId = 1 },
                new Model { Id = 11, Name = "GPT-3.5 Turbo", ModelSeriesId = 1 },
                new Model { Id = 12, Name = "Embedding Ada", ModelSeriesId = 1 }
            );
            await _dbContext.SaveChangesAsync();
            
            // Create ModelProviderTypeAssociations
            _dbContext.ModelProviderTypeAssociations.AddRange(
                new ModelProviderTypeAssociation { Id = 10, Identifier = "gpt-4", ModelId = 10, IsEnabled = true },
                new ModelProviderTypeAssociation { Id = 11, Identifier = "gpt-3.5-turbo", ModelId = 11, IsEnabled = true },
                new ModelProviderTypeAssociation { Id = 12, Identifier = "text-embedding-ada-002", ModelId = 12, IsEnabled = true }
            );
            await _dbContext.SaveChangesAsync();

            var createDto = new CreateModelCostDto
            {
                CostName = "Test Cost with Associations",
                InputCostPerMillionTokens = 25.00m,
                OutputCostPerMillionTokens = 50.00m,
                ModelProviderTypeAssociationIds = new List<int> { 10, 11, 12 }
            };
            
            var createResult = await _controller.CreateModelCost(createDto);
            var createdResult = createResult.Should().BeOfType<CreatedAtActionResult>().Subject;
            var createdCost = createdResult.Value.Should().BeOfType<ModelCostDto>().Subject;

            // Act
            var getResult = await _controller.GetModelCostById(createdCost.Id);

            // Assert
            var okResult = getResult.Should().BeOfType<OkObjectResult>().Subject;
            var retrievedCost = okResult.Value.Should().BeOfType<ModelCostDto>().Subject;
            
            retrievedCost.CostName.Should().Be("Test Cost with Associations");
            retrievedCost.AssociatedModelAliases.Should().HaveCount(3);
            retrievedCost.AssociatedModelAliases.Should().Contain(new[] { "gpt-4", "gpt-3.5-turbo", "text-embedding-ada-002" });
        }

        [Fact]
        public async Task GetAllModelCosts_ShouldReturnAllWithAssociations()
        {
            // Arrange
            var providerId = await SetupTestDataAsync();
            
            // Create Models
            _dbContext.Models.AddRange(
                new Model { Id = 20, Name = "GPT-4", ModelSeriesId = 1 },
                new Model { Id = 21, Name = "GPT-3.5 Turbo", ModelSeriesId = 1 },
                new Model { Id = 22, Name = "Embedding Ada", ModelSeriesId = 1 }
            );
            await _dbContext.SaveChangesAsync();
            
            // Create ModelProviderTypeAssociations
            _dbContext.ModelProviderTypeAssociations.AddRange(
                new ModelProviderTypeAssociation { Id = 20, Identifier = "gpt-4", ModelId = 20, IsEnabled = true },
                new ModelProviderTypeAssociation { Id = 21, Identifier = "gpt-3.5-turbo", ModelId = 21, IsEnabled = true },
                new ModelProviderTypeAssociation { Id = 22, Identifier = "text-embedding-ada-002", ModelId = 22, IsEnabled = true }
            );
            await _dbContext.SaveChangesAsync();

            // Create multiple costs with different mappings
            var cost1 = new CreateModelCostDto
            {
                CostName = "Cost 1",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                ModelProviderTypeAssociationIds = new List<int> { 20 }
            };

            var cost2 = new CreateModelCostDto
            {
                CostName = "Cost 2",
                InputCostPerMillionTokens = 15.00m,
                OutputCostPerMillionTokens = 30.00m,
                ModelProviderTypeAssociationIds = new List<int> { 21, 22 }
            };

            await _controller.CreateModelCost(cost1);
            await _controller.CreateModelCost(cost2);

            // Act
            var result = await _controller.GetAllModelCosts();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var costs = okResult.Value.Should().BeOfType<PagedResult<ModelCostDto>>().Subject;
            var costList = costs.Items;

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

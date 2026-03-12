using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;

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
        public async Task CreateModelCost_WithAssociations_ShouldCreateAndAssociate()
        {
            // Arrange
            var providerId = await SetupTestDataAsync();
            
            // Create Models first
            _dbContext.Models.AddRange(
                new Model { Id = 1, Name = "GPT-4", ModelSeriesId = 1 },
                new Model { Id = 2, Name = "GPT-3.5 Turbo", ModelSeriesId = 1 }
            );
            await _dbContext.SaveChangesAsync();
            
            // Create some ModelProviderTypeAssociations for testing
            _dbContext.ModelProviderTypeAssociations.AddRange(
                new ModelProviderTypeAssociation { Id = 1, Identifier = "gpt-4", ModelId = 1, IsEnabled = true },
                new ModelProviderTypeAssociation { Id = 2, Identifier = "gpt-3.5-turbo", ModelId = 2, IsEnabled = true }
            );
            await _dbContext.SaveChangesAsync();

            var createDto = new CreateModelCostDto
            {
                CostName = "GPT-4 Pricing",
                InputCostPerMillionTokens = 30.00m,
                OutputCostPerMillionTokens = 60.00m,
                ModelProviderTypeAssociationIds = new List<int> { 1, 2 }
            };

            // Act
            var result = await _controller.CreateModelCost(createDto);

            // Assert
            var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            var createdCost = createdResult.Value.Should().BeOfType<ModelCostDto>().Subject;

            createdCost.CostName.Should().Be("GPT-4 Pricing");
            createdCost.AssociatedModelAliases.Should().HaveCount(2);
            createdCost.AssociatedModelAliases.Should().Contain(new[] { "gpt-4", "gpt-3.5-turbo" });

            // Verify in database
            var dbCost = await _modelCostRepository.GetByIdAsync(createdCost.Id);
            dbCost.Should().NotBeNull();
            dbCost!.ModelProviderTypeAssociations.Should().HaveCount(2);
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
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.error.Should().Be("The requested operation is not valid");
            errorResponse.Code.Should().Be("invalid_operation");
        }

        #endregion
    }
}
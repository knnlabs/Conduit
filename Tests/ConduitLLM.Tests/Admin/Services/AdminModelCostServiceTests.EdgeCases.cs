using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;

using AwesomeAssertions;

using Moq;

namespace ConduitLLM.Tests.Admin.Services
{
    public partial class AdminModelCostServiceTests
    {
        #region Edge Cases and Bug Prevention Tests

        [Fact]
        public async Task CreateModelCostAsync_WithEmptyMappingIds_ShouldNotCreateMappings()
        {
            // This test ensures we handle empty mapping lists correctly
            var createDto = new CreateModelCostDto
            {
                CostName = "Cost without mappings",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                ModelProviderTypeAssociationIds = new List<int>() // Empty list
            };

            var createdEntity = new ModelCost
            {
                CostName = createDto.CostName,
                ModelProviderTypeAssociations = new List<ModelProviderTypeAssociation>()
            };

            _mockModelCostRepository.Setup(x => x.GetByCostNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCost?)null);
            _mockModelCostRepository.Setup(x => x.CreateAsync(It.IsAny<ModelCost>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
            _mockModelCostRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(createdEntity);

            // Act
            var result = await _service.CreateModelCostAsync(createDto);

            // Assert
            result.Should().NotBeNull();
            result.AssociatedModelAliases.Should().BeEmpty();
            using (var verifyContext = CreateDbContext())
            {
                // No new associations should have their ModelCostId set to this cost
                verifyContext.ModelProviderTypeAssociations.Where(a => a.ModelCostId == 1).Should().BeEmpty();
            }
        }

        [Fact]
        public async Task UpdateModelCostAsync_RemoveAllMappings_ShouldClearMappings()
        {
            // This test ensures we can remove all mappings by passing an empty list
            var updateDto = new UpdateModelCostDto
            {
                CostName = "Updated Cost",
                InputCostPerMillionTokens = 15.00m,
                OutputCostPerMillionTokens = 25.00m,
                ModelProviderTypeAssociationIds = new List<int>() // Empty list to clear all mappings
            };

            var existingCost = new ModelCost
            {
                Id = 1,
                CostName = "Original Cost"
            };

            // Add existing associations
            using (var setupContext = CreateDbContext())
            {
                AddModels(setupContext, 1, 2);
                setupContext.ModelCosts.Add(new ModelCost { Id = 1, CostName = "Original Cost" });
                setupContext.ModelProviderTypeAssociations.AddRange(new[]
                {
                    new ModelProviderTypeAssociation { Id = 1, ModelCostId = 1, Identifier = "gpt-4", ModelId = 1, IsEnabled = true },
                    new ModelProviderTypeAssociation { Id = 2, ModelCostId = 1, Identifier = "gpt-3.5", ModelId = 2, IsEnabled = true }
                });
                await setupContext.SaveChangesAsync();
            }

            _mockModelCostRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingCost);
            _mockModelCostRepository.Setup(x => x.UpdateAsync(It.IsAny<ModelCost>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpdateModelCostAsync(1, updateDto);

            // Assert
            result.Should().NotBeNull();
            using (var verifyContext = CreateDbContext())
            {
                verifyContext.ModelProviderTypeAssociations.Where(a => a.ModelCostId == 1).Should().BeEmpty();
            }
        }

        [Fact]
        public async Task ToDto_WithInactiveMappings_ShouldOnlyReturnActiveModelAliases()
        {
            // This test verifies the bug fix where only active mappings should be included
            var modelCost = new ModelCost
            {
                Id = 1,
                CostName = "Test Cost",
                ModelProviderTypeAssociations = new List<ModelProviderTypeAssociation>
                {
                    new ModelProviderTypeAssociation 
                    { 
                        IsEnabled = true,
                        Identifier = "active-model",
                        ModelId = 1
                    },
                    new ModelProviderTypeAssociation 
                    { 
                        IsEnabled = false, // Disabled association
                        Identifier = "inactive-model",
                        ModelId = 2
                    }
                }
            };

            _mockModelCostRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.GetModelCostByIdAsync(1);

            // Assert
            result.Should().NotBeNull();
            result!.AssociatedModelAliases.Should().HaveCount(1);
            result.AssociatedModelAliases.Should().Contain("active-model");
            result.AssociatedModelAliases.Should().NotContain("inactive-model");
        }

        #endregion
    }
}

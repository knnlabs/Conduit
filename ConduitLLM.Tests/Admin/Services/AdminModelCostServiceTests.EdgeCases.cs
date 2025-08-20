using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using FluentAssertions;
using Moq;
using Xunit;

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
                ModelProviderMappingIds = new List<int>() // Empty list
            };

            var createdEntity = new ModelCost
            {
                Id = 1,
                CostName = createDto.CostName,
                ModelCostMappings = new List<ModelCostMapping>()
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
                verifyContext.ModelCostMappings.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task UpdateModelCostAsync_RemoveAllMappings_ShouldClearMappings()
        {
            // This test ensures we can remove all mappings by passing an empty list
            var updateDto = new UpdateModelCostDto
            {
                Id = 1,
                CostName = "Updated Cost",
                InputCostPerMillionTokens = 15.00m,
                OutputCostPerMillionTokens = 25.00m,
                ModelProviderMappingIds = new List<int>() // Empty list to clear all mappings
            };

            var existingCost = new ModelCost
            {
                Id = 1,
                CostName = "Original Cost"
            };

            // Add existing mappings
            using (var setupContext = CreateDbContext())
            {
                setupContext.ModelCostMappings.AddRange(new[]
                {
                    new ModelCostMapping { Id = 1, ModelCostId = 1, ModelProviderMappingId = 1 },
                    new ModelCostMapping { Id = 2, ModelCostId = 1, ModelProviderMappingId = 2 }
                });
                await setupContext.SaveChangesAsync();
            }

            _mockModelCostRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingCost);
            _mockModelCostRepository.Setup(x => x.UpdateAsync(It.IsAny<ModelCost>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpdateModelCostAsync(updateDto);

            // Assert
            result.Should().BeTrue();
            using (var verifyContext = CreateDbContext())
            {
                verifyContext.ModelCostMappings.Where(m => m.ModelCostId == 1).Should().BeEmpty();
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
                ModelCostMappings = new List<ModelCostMapping>
                {
                    new ModelCostMapping 
                    { 
                        IsActive = true,
                        ModelProviderMapping = new ModelProviderMapping { ModelAlias = "active-model", ModelId = 1 }
                    },
                    new ModelCostMapping 
                    { 
                        IsActive = false, // Inactive mapping
                        ModelProviderMapping = new ModelProviderMapping { ModelAlias = "inactive-model", ModelId = 1 }
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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Admin.Services
{
    public partial class AdminModelCostServiceTests
    {
        #region GetModelCostByIdAsync Tests

        [Fact]
        public async Task GetModelCostByIdAsync_WithValidId_ShouldReturnModelCost()
        {
            // Arrange
            var modelCost = new ModelCost
            {
                Id = 1,
                CostName = "Test Cost",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                ModelCostMappings = new List<ModelCostMapping>
                {
                    new ModelCostMapping 
                    { 
                        ModelProviderMappingId = 1, 
                        IsActive = true,
                        ModelProviderMapping = new ModelProviderMapping { ModelAlias = "gpt-4", ModelId = 1 }
                    }
                }
            };

            _mockModelCostRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.GetModelCostByIdAsync(1);

            // Assert
            result.Should().NotBeNull();
            result!.CostName.Should().Be("Test Cost");
            result.AssociatedModelAliases.Should().Contain("gpt-4");
        }

        [Fact]
        public async Task GetModelCostByIdAsync_WithNonExistentId_ShouldReturnNull()
        {
            // Arrange
            _mockModelCostRepository.Setup(x => x.GetByIdAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCost?)null);

            // Act
            var result = await _service.GetModelCostByIdAsync(999);

            // Assert
            result.Should().BeNull();
        }

        #endregion
    }
}
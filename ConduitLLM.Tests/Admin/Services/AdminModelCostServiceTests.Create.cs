using System;
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
        #region CreateModelCostAsync Tests

        [Fact]
        public async Task CreateModelCostAsync_WithValidData_ShouldCreateModelCost()
        {
            // Arrange
            var createDto = new CreateModelCostDto
            {
                CostName = "Test Model Cost",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                ModelProviderMappingIds = new List<int>()
            };

            var createdEntity = new ModelCost
            {
                Id = 1,
                CostName = createDto.CostName,
                InputCostPerMillionTokens = createDto.InputCostPerMillionTokens,
                OutputCostPerMillionTokens = createDto.OutputCostPerMillionTokens,
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
            result.CostName.Should().Be("Test Model Cost");
            result.InputCostPerMillionTokens.Should().Be(10.00m);
            result.OutputCostPerMillionTokens.Should().Be(20.00m);
        }

        [Fact]
        public async Task CreateModelCostAsync_WithModelProviderMappingIds_ShouldCreateMappings()
        {
            // Arrange
            var mappingIds = new List<int> { 1, 2, 3 };
            var createDto = new CreateModelCostDto
            {
                CostName = "Test Model Cost with Mappings",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                ModelProviderMappingIds = mappingIds
            };

            var createdEntity = new ModelCost
            {
                Id = 1,
                CostName = createDto.CostName,
                InputCostPerMillionTokens = createDto.InputCostPerMillionTokens,
                OutputCostPerMillionTokens = createDto.OutputCostPerMillionTokens,
                ModelCostMappings = new List<ModelCostMapping>
                {
                    new ModelCostMapping 
                    { 
                        ModelCostId = 1, 
                        ModelProviderMappingId = 1, 
                        IsActive = true,
                        ModelProviderMapping = new ModelProviderMapping { Id = 1, ModelAlias = "model1", ModelId = 1 }
                    },
                    new ModelCostMapping 
                    { 
                        ModelCostId = 1, 
                        ModelProviderMappingId = 2, 
                        IsActive = true,
                        ModelProviderMapping = new ModelProviderMapping { Id = 2, ModelAlias = "model2", ModelId = 1 }
                    },
                    new ModelCostMapping 
                    { 
                        ModelCostId = 1, 
                        ModelProviderMappingId = 3, 
                        IsActive = true,
                        ModelProviderMapping = new ModelProviderMapping { Id = 3, ModelAlias = "model3", ModelId = 1 }
                    }
                }
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
            result.AssociatedModelAliases.Should().HaveCount(3);
            result.AssociatedModelAliases.Should().Contain(new[] { "model1", "model2", "model3" });
            
            // Verify mappings were added to DbContext
            using (var dbContext = CreateDbContext())
            {
                var mappings = dbContext.ModelCostMappings.ToList();
                mappings.Should().HaveCount(3);
                mappings.Should().AllSatisfy(m => 
                {
                    m.ModelCostId.Should().Be(1);
                    m.IsActive.Should().BeTrue();
                });
            }
        }

        [Fact]
        public async Task CreateModelCostAsync_WithDuplicateName_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var createDto = new CreateModelCostDto
            {
                CostName = "Existing Cost",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m
            };

            var existingCost = new ModelCost { Id = 1, CostName = "Existing Cost" };

            _mockModelCostRepository.Setup(x => x.GetByCostNameAsync("Existing Cost", It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingCost);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.CreateModelCostAsync(createDto));
        }

        [Fact]
        public async Task CreateModelCostAsync_WithNullDto_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await _service.CreateModelCostAsync(null!));
        }

        #endregion
    }
}
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;

using FluentAssertions;

using Moq;

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
                ModelProviderTypeAssociationIds = new List<int>()
            };

            var createdEntity = new ModelCost
            {
                Id = 1,
                CostName = createDto.CostName,
                InputCostPerMillionTokens = createDto.InputCostPerMillionTokens,
                OutputCostPerMillionTokens = createDto.OutputCostPerMillionTokens,
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
            result.CostName.Should().Be("Test Model Cost");
            result.InputCostPerMillionTokens.Should().Be(10.00m);
            result.OutputCostPerMillionTokens.Should().Be(20.00m);
        }

        [Fact]
        public async Task CreateModelCostAsync_WithModelProviderTypeAssociationIds_ShouldUpdateAssociations()
        {
            // Arrange
            var associationIds = new List<int> { 1, 2, 3 };
            var createDto = new CreateModelCostDto
            {
                CostName = "Test Model Cost with Associations",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m,
                ModelProviderTypeAssociationIds = associationIds
            };

            var createdEntity = new ModelCost
            {
                Id = 1,
                CostName = createDto.CostName,
                InputCostPerMillionTokens = createDto.InputCostPerMillionTokens,
                OutputCostPerMillionTokens = createDto.OutputCostPerMillionTokens,
                ModelProviderTypeAssociations = new List<ModelProviderTypeAssociation>
                {
                    new ModelProviderTypeAssociation 
                    { 
                        Id = 1,
                        ModelCostId = 1, 
                        Identifier = "gpt-4",
                        IsEnabled = true,
                        ModelId = 1
                    },
                    new ModelProviderTypeAssociation 
                    { 
                        Id = 2,
                        ModelCostId = 1, 
                        Identifier = "gpt-3.5-turbo",
                        IsEnabled = true,
                        ModelId = 2
                    },
                    new ModelProviderTypeAssociation 
                    { 
                        Id = 3,
                        ModelCostId = 1, 
                        Identifier = "claude-3-opus",
                        IsEnabled = true,
                        ModelId = 3
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
            result.AssociatedModelAliases.Should().Contain(new[] { "gpt-4", "gpt-3.5-turbo", "claude-3-opus" });
            
            // Verify the service was called with the correct DTO
            _mockModelCostRepository.Verify(x => x.CreateAsync(It.IsAny<ModelCost>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockModelCostRepository.Verify(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()), Times.Once);
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

        [Fact]
        public async Task CreateModelCostAsync_WithMalformedPricingConfiguration_ShouldRejectBeforeSave()
        {
            var createDto = new CreateModelCostDto
            {
                CostName = "Broken video pricing",
                PricingModel = ConduitLLM.Configuration.PricingModel.PerVideo,
                PricingConfiguration = "{not-json"
            };

            var act = () => _service.CreateModelCostAsync(createDto);

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Invalid PerVideo pricing configuration JSON*");
            _mockModelCostRepository.Verify(
                x => x.CreateAsync(It.IsAny<ModelCost>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion
    }
}

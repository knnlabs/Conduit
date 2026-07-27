using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;

using FluentAssertions;

using Moq;

namespace ConduitLLM.Tests.Admin.Services
{
    public partial class AdminModelCostServiceTests
    {
        #region UpdateModelCostAsync Tests

        [Fact]
        public async Task UpdateModelCostAsync_WithValidData_ShouldUpdateModelCost()
        {
            // Arrange
            var updateDto = new UpdateModelCostDto
            {
                CostName = "Updated Cost Name",
                InputCostPerMillionTokens = 15.00m,
                OutputCostPerMillionTokens = 25.00m,
                ReasoningCostPerMillionTokens = 35.00m,
                ModelProviderTypeAssociationIds = new List<int>()
            };

            var existingCost = new ModelCost
            {
                Id = 1,
                CostName = "Original Cost Name",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m
            };

            _mockModelCostRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingCost);
            _mockModelCostRepository.Setup(x => x.UpdateAsync(It.IsAny<ModelCost>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpdateModelCostAsync(1, updateDto);

            // Assert
            result.Should().NotBeNull();
            existingCost.ReasoningCostPerMillionTokens.Should().Be(35.00m);
            _mockModelCostRepository.Verify(x => x.UpdateAsync(It.IsAny<ModelCost>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateModelCostAsync_WithModelProviderTypeAssociationIds_ShouldUpdateAssociations()
        {
            // Arrange
            var newAssociationIds = new List<int> { 4, 5 };
            var updateDto = new UpdateModelCostDto
            {
                CostName = "Updated Cost",
                InputCostPerMillionTokens = 15.00m,
                OutputCostPerMillionTokens = 25.00m,
                ModelProviderTypeAssociationIds = newAssociationIds
            };

            var existingCost = new ModelCost
            {
                Id = 1,
                CostName = "Original Cost",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m
            };

            // Add existing associations to test context
            using (var setupContext = CreateDbContext())
            {
                AddModels(setupContext, 1, 2, 3, 4, 5);
                setupContext.ModelCosts.Add(new ModelCost { Id = 1, CostName = "Original Cost" });
                setupContext.ModelProviderTypeAssociations.AddRange(new[]
                {
                    new ModelProviderTypeAssociation { Id = 1, ModelCostId = 1, Identifier = "gpt-4", ModelId = 1, IsEnabled = true },
                    new ModelProviderTypeAssociation { Id = 2, ModelCostId = 1, Identifier = "gpt-3.5", ModelId = 2, IsEnabled = true },
                    new ModelProviderTypeAssociation { Id = 3, ModelCostId = 1, Identifier = "claude", ModelId = 3, IsEnabled = true },
                    new ModelProviderTypeAssociation { Id = 4, ModelCostId = null, Identifier = "llama-3", ModelId = 4, IsEnabled = true },
                    new ModelProviderTypeAssociation { Id = 5, ModelCostId = null, Identifier = "mistral", ModelId = 5, IsEnabled = true }
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
            
            // Verify old associations were cleared and new ones set
            using (var verifyContext = CreateDbContext())
            {
                var associations = verifyContext.ModelProviderTypeAssociations.Where(a => a.ModelCostId == 1).ToList();
                associations.Should().HaveCount(2);
                associations.Select(a => a.Id).Should().BeEquivalentTo(new[] { 4, 5 });
                associations.Should().AllSatisfy(a => a.IsEnabled.Should().BeTrue());
                
                // Verify old associations were cleared
                var clearedAssociations = verifyContext.ModelProviderTypeAssociations.Where(a => new[] { 1, 2, 3 }.Contains(a.Id)).ToList();
                clearedAssociations.Should().AllSatisfy(a => a.ModelCostId.Should().BeNull());
            }
        }

        [Fact]
        public async Task UpdateModelCostAsync_WithNonExistentId_ShouldReturnFalse()
        {
            // Arrange
            var updateDto = new UpdateModelCostDto
            {
                CostName = "Non-existent",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m
            };

            _mockModelCostRepository.Setup(x => x.GetByIdAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCost?)null);

            // Act
            var result = await _service.UpdateModelCostAsync(999, updateDto);

            // Assert
            result.Should().BeNull();
            _mockModelCostRepository.Verify(x => x.UpdateAsync(It.IsAny<ModelCost>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateModelCostAsync_WithDuplicateName_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var updateDto = new UpdateModelCostDto
            {
                CostName = "Existing Other Cost",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m
            };

            var existingCost = new ModelCost
            {
                Id = 1,
                CostName = "Original Cost",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m
            };

            var otherCost = new ModelCost
            {
                Id = 2,
                CostName = "Existing Other Cost"
            };

            _mockModelCostRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingCost);
            _mockModelCostRepository.Setup(x => x.GetByCostNameAsync("Existing Other Cost", It.IsAny<CancellationToken>()))
                .ReturnsAsync(otherCost);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.UpdateModelCostAsync(1, updateDto));
        }

        #endregion
    }
}

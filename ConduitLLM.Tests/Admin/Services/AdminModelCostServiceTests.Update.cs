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
        #region UpdateModelCostAsync Tests

        [Fact]
        public async Task UpdateModelCostAsync_WithValidData_ShouldUpdateModelCost()
        {
            // Arrange
            var updateDto = new UpdateModelCostDto
            {
                Id = 1,
                CostName = "Updated Cost Name",
                InputCostPerMillionTokens = 15.00m,
                OutputCostPerMillionTokens = 25.00m,
                ModelProviderMappingIds = new List<int>()
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
            var result = await _service.UpdateModelCostAsync(updateDto);

            // Assert
            result.Should().BeTrue();
            _mockModelCostRepository.Verify(x => x.UpdateAsync(It.IsAny<ModelCost>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateModelCostAsync_WithModelProviderMappingIds_ShouldUpdateMappings()
        {
            // Arrange
            var newMappingIds = new List<int> { 4, 5 };
            var updateDto = new UpdateModelCostDto
            {
                Id = 1,
                CostName = "Updated Cost",
                InputCostPerMillionTokens = 15.00m,
                OutputCostPerMillionTokens = 25.00m,
                ModelProviderMappingIds = newMappingIds
            };

            var existingCost = new ModelCost
            {
                Id = 1,
                CostName = "Original Cost",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m
            };

            // Add existing mappings to test context
            using (var setupContext = CreateDbContext())
            {
                setupContext.ModelCostMappings.AddRange(new[]
                {
                    new ModelCostMapping { Id = 1, ModelCostId = 1, ModelProviderMappingId = 1, IsActive = true },
                    new ModelCostMapping { Id = 2, ModelCostId = 1, ModelProviderMappingId = 2, IsActive = true },
                    new ModelCostMapping { Id = 3, ModelCostId = 1, ModelProviderMappingId = 3, IsActive = true }
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
            
            // Verify old mappings were removed and new ones added
            using (var verifyContext = CreateDbContext())
            {
                var mappings = verifyContext.ModelCostMappings.Where(m => m.ModelCostId == 1).ToList();
                mappings.Should().HaveCount(2);
                mappings.Select(m => m.ModelProviderMappingId).Should().BeEquivalentTo(new[] { 4, 5 });
                mappings.Should().AllSatisfy(m => m.IsActive.Should().BeTrue());
            }
        }

        [Fact]
        public async Task UpdateModelCostAsync_WithNonExistentId_ShouldReturnFalse()
        {
            // Arrange
            var updateDto = new UpdateModelCostDto
            {
                Id = 999,
                CostName = "Non-existent",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m
            };

            _mockModelCostRepository.Setup(x => x.GetByIdAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCost?)null);

            // Act
            var result = await _service.UpdateModelCostAsync(updateDto);

            // Assert
            result.Should().BeFalse();
            _mockModelCostRepository.Verify(x => x.UpdateAsync(It.IsAny<ModelCost>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateModelCostAsync_WithDuplicateName_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var updateDto = new UpdateModelCostDto
            {
                Id = 1,
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
                async () => await _service.UpdateModelCostAsync(updateDto));
        }

        #endregion
    }
}
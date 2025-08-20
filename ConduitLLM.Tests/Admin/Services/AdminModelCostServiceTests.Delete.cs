using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Admin.Services
{
    public partial class AdminModelCostServiceTests
    {
        #region DeleteModelCostAsync Tests

        [Fact]
        public async Task DeleteModelCostAsync_WithExistingId_ShouldReturnTrue()
        {
            // Arrange
            _mockModelCostRepository.Setup(x => x.DeleteAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeleteModelCostAsync(1);

            // Assert
            result.Should().BeTrue();
            _mockModelCostRepository.Verify(x => x.DeleteAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteModelCostAsync_WithNonExistentId_ShouldReturnFalse()
        {
            // Arrange
            _mockModelCostRepository.Setup(x => x.DeleteAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.DeleteModelCostAsync(999);

            // Assert
            result.Should().BeFalse();
        }

        #endregion
    }
}
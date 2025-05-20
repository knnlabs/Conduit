using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services.Adapters;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.WebUI.Adapters
{
    public class ModelCostServiceAdapterTests
    {
        private readonly Mock<IAdminApiClient> _adminApiClientMock;
        private readonly Mock<ILogger<ModelCostServiceAdapter>> _loggerMock;
        private readonly ModelCostServiceAdapter _adapter;

        public ModelCostServiceAdapterTests()
        {
            _adminApiClientMock = new Mock<IAdminApiClient>();
            _loggerMock = new Mock<ILogger<ModelCostServiceAdapter>>();
            _adapter = new ModelCostServiceAdapter(_adminApiClientMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task GetAllModelCostsAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var expectedCosts = new List<ConfigDTOs.ModelCostDto>
            {
                new ConfigDTOs.ModelCostDto { Id = 1, ModelIdPattern = "gpt-4*", InputTokenCost = 10m, OutputTokenCost = 30m },
                new ConfigDTOs.ModelCostDto { Id = 2, ModelIdPattern = "gpt-3.5*", InputTokenCost = 1.5m, OutputTokenCost = 2m }
            };

            _adminApiClientMock.Setup(c => c.GetAllModelCostsAsync())
                .ReturnsAsync(expectedCosts);

            // Act
            var result = await _adapter.GetAllModelCostsAsync();

            // Assert
            Assert.Same(expectedCosts, result);
            _adminApiClientMock.Verify(c => c.GetAllModelCostsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetModelCostByIdAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var expectedCost = new ConfigDTOs.ModelCostDto { Id = 1, ModelIdPattern = "gpt-4*", InputTokenCost = 10m, OutputTokenCost = 30m };

            _adminApiClientMock.Setup(c => c.GetModelCostByIdAsync(1))
                .ReturnsAsync(expectedCost);

            // Act
            var result = await _adapter.GetModelCostByIdAsync(1);

            // Assert
            Assert.Same(expectedCost, result);
            _adminApiClientMock.Verify(c => c.GetModelCostByIdAsync(1), Times.Once);
        }

        [Fact]
        public async Task GetModelCostByPatternAsync_FindsMatchingPattern()
        {
            // Arrange
            var modelCosts = new List<ConfigDTOs.ModelCostDto>
            {
                new ConfigDTOs.ModelCostDto { Id = 1, ModelIdPattern = "gpt-4*", InputTokenCost = 10m, OutputTokenCost = 30m },
                new ConfigDTOs.ModelCostDto { Id = 2, ModelIdPattern = "gpt-3.5*", InputTokenCost = 1.5m, OutputTokenCost = 2m }
            };

            _adminApiClientMock.Setup(c => c.GetAllModelCostsAsync())
                .ReturnsAsync(modelCosts);

            // Act
            var result = await _adapter.GetModelCostByPatternAsync("gpt-4*");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("gpt-4*", result.ModelIdPattern);
            _adminApiClientMock.Verify(c => c.GetAllModelCostsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetModelCostByPatternAsync_ReturnsNull_WhenNoMatchingPattern()
        {
            // Arrange
            var modelCosts = new List<ConfigDTOs.ModelCostDto>
            {
                new ConfigDTOs.ModelCostDto { Id = 1, ModelIdPattern = "gpt-4*", InputTokenCost = 10m, OutputTokenCost = 30m },
                new ConfigDTOs.ModelCostDto { Id = 2, ModelIdPattern = "gpt-3.5*", InputTokenCost = 1.5m, OutputTokenCost = 2m }
            };

            _adminApiClientMock.Setup(c => c.GetAllModelCostsAsync())
                .ReturnsAsync(modelCosts);

            // Act
            var result = await _adapter.GetModelCostByPatternAsync("claude*");

            // Assert
            Assert.Null(result);
            _adminApiClientMock.Verify(c => c.GetAllModelCostsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetModelCostByPatternAsync_HandlesExceptions()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.GetAllModelCostsAsync())
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _adapter.GetModelCostByPatternAsync("gpt-4*");

            // Assert
            Assert.Null(result);
            _adminApiClientMock.Verify(c => c.GetAllModelCostsAsync(), Times.Once);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error getting model cost by pattern")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task CreateModelCostAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var createDto = new ConfigDTOs.CreateModelCostDto { ModelIdPattern = "claude*", InputTokenCost = 5m, OutputTokenCost = 15m };
            var expectedCost = new ConfigDTOs.ModelCostDto { Id = 3, ModelIdPattern = "claude*", InputTokenCost = 5m, OutputTokenCost = 15m };

            _adminApiClientMock.Setup(c => c.CreateModelCostAsync(createDto))
                .ReturnsAsync(expectedCost);

            // Act
            var result = await _adapter.CreateModelCostAsync(createDto);

            // Assert
            Assert.Same(expectedCost, result);
            _adminApiClientMock.Verify(c => c.CreateModelCostAsync(createDto), Times.Once);
        }

        [Fact]
        public async Task UpdateModelCostAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var updateDto = new ConfigDTOs.UpdateModelCostDto { ModelIdPattern = "gpt-4*", InputTokenCost = 15m, OutputTokenCost = 45m };
            var expectedCost = new ConfigDTOs.ModelCostDto { Id = 1, ModelIdPattern = "gpt-4*", InputTokenCost = 15m, OutputTokenCost = 45m };

            _adminApiClientMock.Setup(c => c.UpdateModelCostAsync(1, updateDto))
                .ReturnsAsync(expectedCost);

            // Act
            var result = await _adapter.UpdateModelCostAsync(1, updateDto);

            // Assert
            Assert.Same(expectedCost, result);
            _adminApiClientMock.Verify(c => c.UpdateModelCostAsync(1, updateDto), Times.Once);
        }

        [Fact]
        public async Task DeleteModelCostAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.DeleteModelCostAsync(1))
                .ReturnsAsync(true);

            // Act
            var result = await _adapter.DeleteModelCostAsync(1);

            // Assert
            Assert.True(result);
            _adminApiClientMock.Verify(c => c.DeleteModelCostAsync(1), Times.Once);
        }

        [Fact]
        public async Task CalculateCostAsync_CalculatesCorrectly_WithExactMatch()
        {
            // Arrange
            var modelCosts = new List<ConfigDTOs.ModelCostDto>
            {
                new ConfigDTOs.ModelCostDto { Id = 1, ModelIdPattern = "gpt-4", InputTokenCost = 10m, OutputTokenCost = 30m },
                new ConfigDTOs.ModelCostDto { Id = 2, ModelIdPattern = "gpt-3.5-turbo", InputTokenCost = 1.5m, OutputTokenCost = 2m }
            };

            _adminApiClientMock.Setup(c => c.GetAllModelCostsAsync())
                .ReturnsAsync(modelCosts);

            // Act
            var result = await _adapter.CalculateCostAsync("gpt-4", 1000, 500);

            // Assert
            // (10 * 1000 / 1000) + (30 * 500 / 1000) = 10 + 15 = 25
            Assert.Equal(25m, result);
            _adminApiClientMock.Verify(c => c.GetAllModelCostsAsync(), Times.Once);
        }

        [Fact]
        public async Task CalculateCostAsync_CalculatesCorrectly_WithWildcardMatch()
        {
            // Arrange
            var modelCosts = new List<ConfigDTOs.ModelCostDto>
            {
                new ConfigDTOs.ModelCostDto { Id = 1, ModelIdPattern = "gpt-4*", InputTokenCost = 10m, OutputTokenCost = 30m },
                new ConfigDTOs.ModelCostDto { Id = 2, ModelIdPattern = "gpt-3.5*", InputTokenCost = 1.5m, OutputTokenCost = 2m }
            };

            _adminApiClientMock.Setup(c => c.GetAllModelCostsAsync())
                .ReturnsAsync(modelCosts);

            // Act
            var result = await _adapter.CalculateCostAsync("gpt-4-turbo", 1000, 500);

            // Assert
            // (10 * 1000 / 1000) + (30 * 500 / 1000) = 10 + 15 = 25
            Assert.Equal(25m, result);
            _adminApiClientMock.Verify(c => c.GetAllModelCostsAsync(), Times.Once);
        }

        [Fact]
        public async Task CalculateCostAsync_ReturnsBestMatch_WhenMultipleWildcardMatches()
        {
            // Arrange
            var modelCosts = new List<ConfigDTOs.ModelCostDto>
            {
                new ConfigDTOs.ModelCostDto { Id = 1, ModelIdPattern = "gpt*", InputTokenCost = 5m, OutputTokenCost = 10m },
                new ConfigDTOs.ModelCostDto { Id = 2, ModelIdPattern = "gpt-4*", InputTokenCost = 10m, OutputTokenCost = 30m }
            };

            _adminApiClientMock.Setup(c => c.GetAllModelCostsAsync())
                .ReturnsAsync(modelCosts);

            // Act
            var result = await _adapter.CalculateCostAsync("gpt-4-turbo", 1000, 500);

            // Assert
            // Should use the more specific pattern "gpt-4*"
            // (10 * 1000 / 1000) + (30 * 500 / 1000) = 10 + 15 = 25
            Assert.Equal(25m, result);
            _adminApiClientMock.Verify(c => c.GetAllModelCostsAsync(), Times.Once);
        }

        [Fact]
        public async Task CalculateCostAsync_ReturnsZero_WhenNoMatch()
        {
            // Arrange
            var modelCosts = new List<ConfigDTOs.ModelCostDto>
            {
                new ConfigDTOs.ModelCostDto { Id = 1, ModelIdPattern = "gpt-4*", InputTokenCost = 10m, OutputTokenCost = 30m },
                new ConfigDTOs.ModelCostDto { Id = 2, ModelIdPattern = "gpt-3.5*", InputTokenCost = 1.5m, OutputTokenCost = 2m }
            };

            _adminApiClientMock.Setup(c => c.GetAllModelCostsAsync())
                .ReturnsAsync(modelCosts);

            // Act
            var result = await _adapter.CalculateCostAsync("claude-3-opus", 1000, 500);

            // Assert
            Assert.Equal(0m, result);
            _adminApiClientMock.Verify(c => c.GetAllModelCostsAsync(), Times.Once);
        }

        [Fact]
        public async Task CalculateCostAsync_ReturnsZero_WhenNoCosts()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.GetAllModelCostsAsync())
                .ReturnsAsync(new List<ConfigDTOs.ModelCostDto>());

            // Act
            var result = await _adapter.CalculateCostAsync("gpt-4", 1000, 500);

            // Assert
            Assert.Equal(0m, result);
            _adminApiClientMock.Verify(c => c.GetAllModelCostsAsync(), Times.Once);
        }

        [Fact]
        public async Task CalculateCostAsync_HandlesExceptions()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.GetAllModelCostsAsync())
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _adapter.CalculateCostAsync("gpt-4", 1000, 500);

            // Assert
            Assert.Equal(0m, result);
            _adminApiClientMock.Verify(c => c.GetAllModelCostsAsync(), Times.Once);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error calculating cost for model")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }
}
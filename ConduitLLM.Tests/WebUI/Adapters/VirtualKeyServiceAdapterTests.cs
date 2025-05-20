using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services.Adapters;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.WebUI.Adapters
{
    public class VirtualKeyServiceAdapterTests
    {
        private readonly Mock<IAdminApiClient> _adminApiClientMock;
        private readonly Mock<ILogger<VirtualKeyServiceAdapter>> _loggerMock;
        private readonly VirtualKeyServiceAdapter _adapter;

        public VirtualKeyServiceAdapterTests()
        {
            _adminApiClientMock = new Mock<IAdminApiClient>();
            _loggerMock = new Mock<ILogger<VirtualKeyServiceAdapter>>();
            _adapter = new VirtualKeyServiceAdapter(_adminApiClientMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task ListVirtualKeysAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var expectedKeys = new List<ConfigDTOs.VirtualKey.VirtualKeyDto>
            {
                new ConfigDTOs.VirtualKey.VirtualKeyDto { Id = 1, Name = "Test Key 1" },
                new ConfigDTOs.VirtualKey.VirtualKeyDto { Id = 2, Name = "Test Key 2" }
            };

            _adminApiClientMock.Setup(c => c.GetAllVirtualKeysAsync())
                .ReturnsAsync(expectedKeys);

            // Act
            var result = await _adapter.ListVirtualKeysAsync();

            // Assert
            Assert.Equal(expectedKeys.Count, result.Count);
            _adminApiClientMock.Verify(c => c.GetAllVirtualKeysAsync(), Times.Once);
        }

        [Fact]
        public async Task GetVirtualKeyInfoAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var expectedKey = new ConfigDTOs.VirtualKey.VirtualKeyDto { Id = 1, Name = "Test Key" };

            _adminApiClientMock.Setup(c => c.GetVirtualKeyByIdAsync(1))
                .ReturnsAsync(expectedKey);

            // Act
            var result = await _adapter.GetVirtualKeyInfoAsync(1);

            // Assert
            Assert.Same(expectedKey, result);
            _adminApiClientMock.Verify(c => c.GetVirtualKeyByIdAsync(1), Times.Once);
        }

        [Fact]
        public async Task CreateVirtualKeyAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var createDto = new ConfigDTOs.VirtualKey.CreateVirtualKeyRequestDto { KeyName = "New Key" };
            var keyInfo = new ConfigDTOs.VirtualKey.VirtualKeyDto { Id = 1, Name = "New Key" };
            var expectedResponse = new ConfigDTOs.VirtualKey.CreateVirtualKeyResponseDto { 
                VirtualKey = "vk-123456", 
                KeyInfo = keyInfo 
            };

            _adminApiClientMock.Setup(c => c.CreateVirtualKeyAsync(createDto))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _adapter.GenerateVirtualKeyAsync(createDto);

            // Assert
            Assert.Same(expectedResponse, result);
            _adminApiClientMock.Verify(c => c.CreateVirtualKeyAsync(createDto), Times.Once);
        }

        [Fact]
        public async Task UpdateVirtualKeyAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var updateDto = new ConfigDTOs.VirtualKey.UpdateVirtualKeyRequestDto { KeyName = "Updated Key" };

            _adminApiClientMock.Setup(c => c.UpdateVirtualKeyAsync(1, updateDto))
                .ReturnsAsync(true);

            // Act
            var result = await _adapter.UpdateVirtualKeyAsync(1, updateDto);

            // Assert
            Assert.True(result);
            _adminApiClientMock.Verify(c => c.UpdateVirtualKeyAsync(1, updateDto), Times.Once);
        }

        [Fact]
        public async Task DeleteVirtualKeyAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.DeleteVirtualKeyAsync(1))
                .ReturnsAsync(true);

            // Act
            var result = await _adapter.DeleteVirtualKeyAsync(1);

            // Assert
            Assert.True(result);
            _adminApiClientMock.Verify(c => c.DeleteVirtualKeyAsync(1), Times.Once);
        }

        [Fact]
        public async Task GetVirtualKeyUsageStatisticsAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var expectedStats = new List<ConfigDTOs.VirtualKeyCostDataDto>
            {
                new ConfigDTOs.VirtualKeyCostDataDto { VirtualKeyId = 1, TotalCost = 10.5m }
            };

            _adminApiClientMock.Setup(c => c.GetVirtualKeyUsageStatisticsAsync(1))
                .ReturnsAsync(expectedStats);

            // Act
            var result = await _adapter.GetVirtualKeyUsageStatisticsAsync(1);

            // Assert
            Assert.Same(expectedStats, result);
            _adminApiClientMock.Verify(c => c.GetVirtualKeyUsageStatisticsAsync(1), Times.Once);
        }

        [Fact]
        public async Task ResetSpendAsync_ReturnsTrue()
        {
            // Act
            var result = await _adapter.ResetSpendAsync(1);

            // Assert 
            Assert.True(result);
        }
    }
}
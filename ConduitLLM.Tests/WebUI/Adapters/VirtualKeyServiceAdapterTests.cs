using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services.Adapters;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

using VirtualKeyDto = ConduitLLM.Configuration.DTOs.VirtualKey.VirtualKeyDto;
using CreateVirtualKeyRequestDto = ConduitLLM.Configuration.DTOs.VirtualKey.CreateVirtualKeyRequestDto;
using CreateVirtualKeyResponseDto = ConduitLLM.Configuration.DTOs.VirtualKey.CreateVirtualKeyResponseDto;
using UpdateVirtualKeyRequestDto = ConduitLLM.Configuration.DTOs.VirtualKey.UpdateVirtualKeyRequestDto;
using VirtualKeyCostDataDto = ConduitLLM.Configuration.DTOs.Costs.VirtualKeyCostDataDto;
using WebUIVirtualKeyCostDataDto = ConduitLLM.WebUI.DTOs.VirtualKeyCostDataDto;


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
            var expectedKeys = new List<VirtualKeyDto>
            {
                new VirtualKeyDto { Id = 1, Name = "Test Key 1" },
                new VirtualKeyDto { Id = 2, Name = "Test Key 2" }
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
            var expectedKey = new VirtualKeyDto { Id = 1, Name = "Test Key" };

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
            var createDto = new CreateVirtualKeyRequestDto { KeyName = "New Key" };
            var keyInfo = new VirtualKeyDto { Id = 1, Name = "New Key" };
            var expectedResponse = new CreateVirtualKeyResponseDto { 
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
            var updateDto = new UpdateVirtualKeyRequestDto { KeyName = "Updated Key" };

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
            var configStats = new List<VirtualKeyCostDataDto>
            {
                new VirtualKeyCostDataDto { VirtualKeyId = 1, Cost = 10.5m }
            };
            
            // Convert to WebUI DTOs
            var webStats = configStats.Select(c => new WebUIVirtualKeyCostDataDto
            {
                VirtualKeyId = c.VirtualKeyId,
                TotalCost = c.Cost
            }).ToList();

            _adminApiClientMock.Setup(c => c.GetVirtualKeyUsageStatisticsAsync(1))
                .Returns(Task.FromResult<IEnumerable<WebUIVirtualKeyCostDataDto>>(webStats));

            // Act
            var result = await _adapter.GetVirtualKeyUsageStatisticsAsync(1);

            // Assert
            Assert.NotNull(result);
            _adminApiClientMock.Verify(c => c.GetVirtualKeyUsageStatisticsAsync(1), Times.Once);
        }

        [Fact]
        public async Task ResetSpendAsync_ReturnsTrue()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.ResetVirtualKeySpendAsync(1))
                .ReturnsAsync(true);

            // Act
            var result = await _adapter.ResetSpendAsync(1);

            // Assert 
            Assert.True(result);
            _adminApiClientMock.Verify(c => c.ResetVirtualKeySpendAsync(1), Times.Once);
        }
    }
}
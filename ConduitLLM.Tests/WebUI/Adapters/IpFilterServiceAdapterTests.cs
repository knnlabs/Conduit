using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Models;
using ConduitLLM.WebUI.Services.Adapters;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;


namespace ConduitLLM.Tests.WebUI.Adapters
{
    public class IpFilterServiceAdapterTests
    {
        private readonly Mock<IAdminApiClient> _adminApiClientMock;
        private readonly Mock<ILogger<IpFilterServiceAdapter>> _loggerMock;
        private readonly IpFilterServiceAdapter _adapter;

        public IpFilterServiceAdapterTests()
        {
            _adminApiClientMock = new Mock<IAdminApiClient>();
            _loggerMock = new Mock<ILogger<IpFilterServiceAdapter>>();
            _adapter = new IpFilterServiceAdapter(_adminApiClientMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task GetAllFiltersAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var expectedFilters = new List<ConduitLLM.Configuration.DTOs.IpFilter.IpFilterDto>
            {
                new ConduitLLM.Configuration.DTOs.IpFilter.IpFilterDto { Id = 1, IpAddress = "192.168.1.1", FilterType = "whitelist" },
                new ConduitLLM.Configuration.DTOs.IpFilter.IpFilterDto { Id = 2, IpAddress = "10.0.0.1", FilterType = "blacklist" }
            };

            _adminApiClientMock.Setup(c => c.GetAllIpFiltersAsync())
                .ReturnsAsync(expectedFilters);

            // Act
            var result = await _adapter.GetAllFiltersAsync();

            // Assert
            Assert.Same(expectedFilters, result);
            _adminApiClientMock.Verify(c => c.GetAllIpFiltersAsync(), Times.Once);
        }

        [Fact]
        public async Task GetFilterByIdAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var expectedFilter = new ConduitLLM.Configuration.DTOs.IpFilter.IpFilterDto { Id = 1, IpAddress = "192.168.1.1", FilterType = "whitelist" };

            _adminApiClientMock.Setup(c => c.GetIpFilterByIdAsync(1))
                .ReturnsAsync(expectedFilter);

            // Act
            var result = await _adapter.GetFilterByIdAsync(1);

            // Assert
            Assert.Same(expectedFilter, result);
            _adminApiClientMock.Verify(c => c.GetIpFilterByIdAsync(1), Times.Once);
        }

        [Fact]
        public async Task CreateFilterAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var createDto = new ConduitLLM.Configuration.DTOs.IpFilter.CreateIpFilterDto { IpAddress = "192.168.1.2", FilterType = "whitelist" };
            var expectedFilter = new ConduitLLM.Configuration.DTOs.IpFilter.IpFilterDto { Id = 3, IpAddress = "192.168.1.2", FilterType = "whitelist" };

            _adminApiClientMock.Setup(c => c.CreateIpFilterAsync(createDto))
                .ReturnsAsync(expectedFilter);

            // Act
            var result = await _adapter.CreateFilterAsync(createDto);

            // Assert
            Assert.NotNull(result);
            Assert.Same(expectedFilter, result);
            _adminApiClientMock.Verify(c => c.CreateIpFilterAsync(createDto), Times.Once);
        }

        [Fact]
        public async Task UpdateFilterAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var updateDto = new ConduitLLM.Configuration.DTOs.IpFilter.UpdateIpFilterDto { IpAddress = "192.168.1.1", FilterType = "blacklist" };
            var expectedFilter = new ConduitLLM.Configuration.DTOs.IpFilter.IpFilterDto { Id = 1, IpAddress = "192.168.1.1", FilterType = "blacklist" };

            _adminApiClientMock.Setup(c => c.UpdateIpFilterAsync(1, updateDto))
                .ReturnsAsync(expectedFilter);

            // Act
            // Note: UpdateFilterAsync in the adapter now only takes the DTO which includes the ID
            updateDto.Id = 1;
            var result = await _adapter.UpdateFilterAsync(updateDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedFilter, result);
            _adminApiClientMock.Verify(c => c.UpdateIpFilterAsync(1, updateDto), Times.Once);
        }

        [Fact]
        public async Task DeleteFilterAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.DeleteIpFilterAsync(1))
                .ReturnsAsync(true);

            // Act
            var result = await _adapter.DeleteFilterAsync(1);

            // Assert
            Assert.True(result);
            _adminApiClientMock.Verify(c => c.DeleteIpFilterAsync(1), Times.Once);
        }

        [Fact]
        public async Task GetIpFilterSettingsAsync_ReturnsCorrectSettings_WithWhitelists()
        {
            // Arrange
            var settingsDto = new ConduitLLM.Configuration.DTOs.IpFilter.IpFilterSettingsDto
            {
                WhitelistFilters = new List<ConduitLLM.Configuration.DTOs.IpFilter.IpFilterDto>
                {
                    new ConduitLLM.Configuration.DTOs.IpFilter.IpFilterDto { Id = 1, IpAddress = "192.168.1.1", FilterType = "whitelist" },
                    new ConduitLLM.Configuration.DTOs.IpFilter.IpFilterDto { Id = 3, IpAddress = "172.16.0.1", FilterType = "whitelist" }
                },
                BlacklistFilters = new List<ConduitLLM.Configuration.DTOs.IpFilter.IpFilterDto>
                {
                    new ConduitLLM.Configuration.DTOs.IpFilter.IpFilterDto { Id = 2, IpAddress = "10.0.0.1", FilterType = "blacklist" }
                },
                FilterMode = "restrictive",
                IsEnabled = true,
                DefaultAllow = false,
                BypassForAdminUi = true,
                ExcludedEndpoints = new List<string> { "/api/v1/health" }
            };

            _adminApiClientMock.Setup(c => c.GetIpFilterSettingsAsync())
                .ReturnsAsync(settingsDto);

            // Act
            var result = await _adapter.GetIpFilterSettingsAsync();

            // Assert
            Assert.Equal(2, result.WhitelistFilters.Count);
            Assert.Single(result.BlacklistFilters);
            Assert.Equal("restrictive", result.FilterMode); // Should be restrictive because whitelists exist
            
            // Check whitelist
            Assert.Equal(1, result.WhitelistFilters[0].Id);
            Assert.Equal(3, result.WhitelistFilters[1].Id);
            
            // Check blacklist
            Assert.Equal(2, result.BlacklistFilters[0].Id);
            
            _adminApiClientMock.Verify(c => c.GetIpFilterSettingsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetIpFilterSettingsAsync_ReturnsPermissiveMode_WithoutWhitelists()
        {
            // Arrange
            var settingsDto = new ConduitLLM.Configuration.DTOs.IpFilter.IpFilterSettingsDto
            {
                WhitelistFilters = new List<ConduitLLM.Configuration.DTOs.IpFilter.IpFilterDto>(),
                BlacklistFilters = new List<ConduitLLM.Configuration.DTOs.IpFilter.IpFilterDto>
                {
                    new ConduitLLM.Configuration.DTOs.IpFilter.IpFilterDto { Id = 1, IpAddress = "10.0.0.1", FilterType = "blacklist" },
                    new ConduitLLM.Configuration.DTOs.IpFilter.IpFilterDto { Id = 2, IpAddress = "10.0.0.2", FilterType = "blacklist" }
                },
                FilterMode = "permissive",
                IsEnabled = true,
                DefaultAllow = true,
                BypassForAdminUi = true,
                ExcludedEndpoints = new List<string> { "/api/v1/health" }
            };

            _adminApiClientMock.Setup(c => c.GetIpFilterSettingsAsync())
                .ReturnsAsync(settingsDto);

            // Act
            var result = await _adapter.GetIpFilterSettingsAsync();

            // Assert
            Assert.Empty(result.WhitelistFilters);
            Assert.Equal(2, result.BlacklistFilters.Count);
            Assert.Equal("permissive", result.FilterMode); // Should be permissive because no whitelists exist
            _adminApiClientMock.Verify(c => c.GetIpFilterSettingsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetIpFilterSettingsAsync_HandlesExceptions()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.GetIpFilterSettingsAsync())
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _adapter.GetIpFilterSettingsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.WhitelistFilters);
            Assert.Empty(result.BlacklistFilters);
            Assert.Equal("permissive", result.FilterMode);
            _adminApiClientMock.Verify(c => c.GetIpFilterSettingsAsync(), Times.Once);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v != null && v.ToString().Contains("Error getting IP filter settings")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}
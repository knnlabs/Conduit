using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.IpFilter;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Models;
using ConduitLLM.WebUI.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.WebUI.Providers
{
    public class AdminApiClientIpFilterTests
    {
        private readonly Mock<IAdminApiClient> _mockAdminApiClient;
        private readonly Mock<ILogger<AdminApiClient>> _loggerMock;
        private readonly IIpFilterService _service;

        public AdminApiClientIpFilterTests()
        {
            // Create a mock that implements both interfaces
            _mockAdminApiClient = new Mock<IAdminApiClient>();
            Mock<IIpFilterService> mockService = _mockAdminApiClient.As<IIpFilterService>();
            _loggerMock = new Mock<ILogger<AdminApiClient>>();
            
            // Use the mock as the service
            _service = mockService.Object;
        }

        [Fact]
        public async Task GetAllFiltersAsync_ReturnsAllFilters_WhenApiReturnsData()
        {
            // Arrange
            var filters = new List<IpFilterDto>
            {
                new IpFilterDto { Id = 1, IpAddressOrCidr = "192.168.1.1", IsAllowed = true, Description = "Test filter 1" },
                new IpFilterDto { Id = 2, IpAddressOrCidr = "10.0.0.0/24", IsAllowed = false, Description = "Test filter 2" }
            };

            _mockAdminApiClient.Setup(c => c.GetAllIpFiltersAsync())
                .ReturnsAsync(filters);

            // Act
            var result = await _service.GetAllFiltersAsync();

            // Assert
            var resultList = result.ToList();
            Assert.Equal(2, resultList.Count);
            Assert.Equal(1, resultList[0].Id);
            Assert.Equal("192.168.1.1", resultList[0].IpAddressOrCidr);
            Assert.Equal(2, resultList[1].Id);
            Assert.Equal("10.0.0.0/24", resultList[1].IpAddressOrCidr);
            _mockAdminApiClient.Verify(c => c.GetAllIpFiltersAsync(), Times.Once);
        }

        [Fact]
        public async Task GetEnabledFiltersAsync_ReturnsEnabledFilters_WhenApiReturnsData()
        {
            // Arrange
            var filters = new List<IpFilterDto>
            {
                new IpFilterDto { Id = 1, IpAddressOrCidr = "192.168.1.1", IsAllowed = true, Description = "Test filter 1", IsEnabled = true }
            };

            _mockAdminApiClient.Setup(c => c.GetEnabledIpFiltersAsync())
                .ReturnsAsync(filters);

            // Act
            var result = await _service.GetEnabledFiltersAsync();

            // Assert
            var resultList = result.ToList();
            Assert.Single(resultList);
            Assert.Equal(1, resultList[0].Id);
            Assert.Equal("192.168.1.1", resultList[0].IpAddressOrCidr);
            Assert.True(resultList[0].IsEnabled);
            _mockAdminApiClient.Verify(c => c.GetEnabledIpFiltersAsync(), Times.Once);
        }

        [Fact]
        public async Task GetFilterByIdAsync_ReturnsFilter_WhenFilterExists()
        {
            // Arrange
            var filter = new IpFilterDto 
            { 
                Id = 1, 
                IpAddressOrCidr = "192.168.1.1", 
                IsAllowed = true, 
                Description = "Test filter", 
                IsEnabled = true 
            };

            _mockAdminApiClient.Setup(c => c.GetIpFilterByIdAsync(1))
                .ReturnsAsync(filter);

            // Act
            var result = await _service.GetFilterByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("192.168.1.1", result.IpAddressOrCidr);
            Assert.True(result.IsAllowed);
            Assert.True(result.IsEnabled);
            Assert.Equal("Test filter", result.Description);
            _mockAdminApiClient.Verify(c => c.GetIpFilterByIdAsync(1), Times.Once);
        }

        [Fact]
        public async Task CreateFilterAsync_ReturnsNewFilter_WhenCreationSucceeds()
        {
            // Arrange
            var createDto = new CreateIpFilterDto
            {
                IpAddressOrCidr = "192.168.1.1",
                IsAllowed = true,
                Description = "Test filter",
                IsEnabled = true
            };

            var createdFilter = new IpFilterDto
            {
                Id = 1,
                IpAddressOrCidr = "192.168.1.1",
                IsAllowed = true,
                Description = "Test filter",
                IsEnabled = true
            };

            _mockAdminApiClient.Setup(c => c.CreateIpFilterAsync(createDto))
                .ReturnsAsync(createdFilter);

            // Act
            var (success, errorMessage, filter) = await _service.CreateFilterAsync(createDto);

            // Assert
            Assert.True(success);
            Assert.Null(errorMessage);
            Assert.NotNull(filter);
            Assert.Equal(1, filter.Id);
            Assert.Equal("192.168.1.1", filter.IpAddressOrCidr);
            _mockAdminApiClient.Verify(c => c.CreateIpFilterAsync(createDto), Times.Once);
        }

        [Fact]
        public async Task UpdateFilterAsync_ReturnsSuccess_WhenUpdateSucceeds()
        {
            // Arrange
            var updateDto = new UpdateIpFilterDto
            {
                Id = 1,
                IpAddressOrCidr = "192.168.1.1",
                IsAllowed = false,
                Description = "Updated filter",
                IsEnabled = true
            };

            var updatedFilter = new IpFilterDto
            {
                Id = 1,
                IpAddressOrCidr = "192.168.1.1",
                IsAllowed = false,
                Description = "Updated filter",
                IsEnabled = true
            };

            _mockAdminApiClient.Setup(c => c.UpdateIpFilterAsync(1, updateDto))
                .ReturnsAsync(updatedFilter);

            // Act
            var (success, errorMessage) = await _service.UpdateFilterAsync(updateDto);

            // Assert
            Assert.True(success);
            Assert.Null(errorMessage);
            _mockAdminApiClient.Verify(c => c.UpdateIpFilterAsync(1, updateDto), Times.Once);
        }

        [Fact]
        public async Task DeleteFilterAsync_ReturnsSuccess_WhenDeletionSucceeds()
        {
            // Arrange
            _mockAdminApiClient.Setup(c => c.DeleteIpFilterAsync(1))
                .ReturnsAsync(true);

            // Act
            var (success, errorMessage) = await _service.DeleteFilterAsync(1);

            // Assert
            Assert.True(success);
            Assert.Null(errorMessage);
            _mockAdminApiClient.Verify(c => c.DeleteIpFilterAsync(1), Times.Once);
        }

        [Fact]
        public async Task GetIpFilterSettingsAsync_ReturnsSettings_WhenApiReturnsData()
        {
            // Arrange
            var settingsDto = new IpFilterSettingsDto
            {
                IsEnabled = true,
                DefaultAllow = false,
                BypassForAdminUi = true,
                FilterMode = "strict",
                ExcludedEndpoints = new List<string> { "/api/v1/health", "/api/v1/status" }
            };

            _mockAdminApiClient.Setup(c => c.GetIpFilterSettingsAsync())
                .ReturnsAsync(settingsDto);

            // Act
            var result = await _service.GetIpFilterSettingsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsEnabled);
            Assert.False(result.DefaultAllow);
            Assert.True(result.BypassForAdminUi);
            Assert.Equal("strict", result.FilterMode);
            Assert.Equal(2, result.ExcludedEndpoints.Count);
            Assert.Contains("/api/v1/health", result.ExcludedEndpoints);
            Assert.Contains("/api/v1/status", result.ExcludedEndpoints);
            _mockAdminApiClient.Verify(c => c.GetIpFilterSettingsAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateIpFilterSettingsAsync_ReturnsSuccess_WhenUpdateSucceeds()
        {
            // Arrange
            var settings = new IpFilterSettings
            {
                IsEnabled = true,
                DefaultAllow = false,
                BypassForAdminUi = true,
                FilterMode = "strict",
                ExcludedEndpoints = new List<string> { "/api/v1/health", "/api/v1/status" }
            };

            _mockAdminApiClient.Setup(c => c.UpdateIpFilterSettingsAsync(It.IsAny<IpFilterSettingsDto>()))
                .ReturnsAsync(true);

            // Act
            var (success, errorMessage) = await _service.UpdateIpFilterSettingsAsync(settings);

            // Assert
            Assert.True(success);
            Assert.Null(errorMessage);
            _mockAdminApiClient.Verify(c => c.UpdateIpFilterSettingsAsync(It.Is<IpFilterSettingsDto>(dto => 
                dto.IsEnabled == settings.IsEnabled &&
                dto.DefaultAllow == settings.DefaultAllow &&
                dto.BypassForAdminUi == settings.BypassForAdminUi &&
                dto.FilterMode == settings.FilterMode &&
                dto.ExcludedEndpoints.Count == settings.ExcludedEndpoints.Count
            )), Times.Once);
        }

        [Fact]
        public async Task IsIpAllowedAsync_ReturnsIsAllowed_WhenCheckSucceeds()
        {
            // Arrange
            var ipAddress = "192.168.1.1";
            var checkResult = new IpCheckResult
            {
                IsAllowed = true,
                MatchedFilter = new IpFilterDto
                {
                    Id = 1,
                    IpAddressOrCidr = "192.168.1.1",
                    IsAllowed = true
                }
            };

            _mockAdminApiClient.Setup(c => c.CheckIpAddressAsync(ipAddress))
                .ReturnsAsync(checkResult);

            // Act
            var result = await _service.IsIpAllowedAsync(ipAddress);

            // Assert
            Assert.True(result);
            _mockAdminApiClient.Verify(c => c.CheckIpAddressAsync(ipAddress), Times.Once);
        }

        [Fact]
        public async Task IsIpAllowedAsync_ReturnsTrue_WhenApiReturnsNull()
        {
            // Arrange
            var ipAddress = "192.168.1.1";

            _mockAdminApiClient.Setup(c => c.CheckIpAddressAsync(ipAddress))
                .ReturnsAsync((IpCheckResult)null);

            // Act
            var result = await _service.IsIpAllowedAsync(ipAddress);

            // Assert
            Assert.True(result); // Default to allowed if check fails
            _mockAdminApiClient.Verify(c => c.CheckIpAddressAsync(ipAddress), Times.Once);
        }
    }
}
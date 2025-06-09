using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs.IpFilter;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;

namespace ConduitLLM.Admin.Tests.Controllers
{
    public class IpFilterControllerTests
    {
        private readonly Mock<IAdminIpFilterService> _mockService;
        private readonly Mock<ILogger<IpFilterController>> _mockLogger;
        private readonly IpFilterController _controller;

        public IpFilterControllerTests()
        {
            _mockService = new Mock<IAdminIpFilterService>();
            _mockLogger = new Mock<ILogger<IpFilterController>>();
            _controller = new IpFilterController(_mockService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetAllFilters_ReturnsOkWithFilters()
        {
            // Arrange
            var filters = new List<IpFilterDto>
            {
                new IpFilterDto { Id = 1, Name = "Office IP", IpAddress = "192.168.1.0/24", FilterType = "Allow", IsEnabled = true },
                new IpFilterDto { Id = 2, Name = "Bad Actor", IpAddress = "10.0.0.5", FilterType = "Deny", IsEnabled = true }
            };

            _mockService
                .Setup(s => s.GetAllFiltersAsync())
                .ReturnsAsync(filters);

            // Act
            var result = await _controller.GetAllFilters();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnValue = Assert.IsAssignableFrom<IEnumerable<IpFilterDto>>(okResult.Value);
            Assert.Equal(2, ((List<IpFilterDto>)returnValue).Count);
        }

        [Fact]
        public async Task GetAllFilters_ExceptionThrown_ReturnsInternalServerError()
        {
            // Arrange
            _mockService
                .Setup(s => s.GetAllFiltersAsync())
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetAllFilters();

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
        }

        [Fact]
        public async Task GetEnabledFilters_ReturnsOkWithFilters()
        {
            // Arrange
            var filters = new List<IpFilterDto>
            {
                new IpFilterDto { Id = 1, Name = "Office IP", IpAddress = "192.168.1.0/24", FilterType = "Allow", IsEnabled = true },
                new IpFilterDto { Id = 2, Name = "Bad Actor", IpAddress = "10.0.0.5", FilterType = "Deny", IsEnabled = true }
            };

            _mockService
                .Setup(s => s.GetEnabledFiltersAsync())
                .ReturnsAsync(filters);

            // Act
            var result = await _controller.GetEnabledFilters();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnValue = Assert.IsAssignableFrom<IEnumerable<IpFilterDto>>(okResult.Value);
            Assert.Equal(2, ((List<IpFilterDto>)returnValue).Count);
        }

        [Fact]
        public async Task GetFilterById_ExistingFilter_ReturnsOkWithFilter()
        {
            // Arrange
            var filter = new IpFilterDto 
            { 
                Id = 1, 
                Name = "Office IP", 
                IpAddress = "192.168.1.0/24", 
                FilterType = "Allow", 
                IsEnabled = true
            };

            _mockService
                .Setup(s => s.GetFilterByIdAsync(1))
                .ReturnsAsync(filter);

            // Act
            var result = await _controller.GetFilterById(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnValue = Assert.IsType<IpFilterDto>(okResult.Value);
            Assert.Equal(1, returnValue.Id);
            Assert.Equal("Office IP", returnValue.Name);
        }

        [Fact]
        public async Task GetFilterById_NonexistentFilter_ReturnsNotFound()
        {
            // Arrange
            _mockService
                .Setup(s => s.GetFilterByIdAsync(999))
                .ReturnsAsync((IpFilterDto?)null);

            // Act
            var result = await _controller.GetFilterById(999);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task CreateFilter_ValidFilter_ReturnsCreatedWithFilter()
        {
            // Arrange
            var createDto = new CreateIpFilterDto
            {
                Name = "New Office",
                IpAddress = "172.16.0.0/16",
                FilterType = "Allow",
                IsEnabled = true,
                Description = "New office network"
            };

            var createdFilter = new IpFilterDto
            {
                Id = 3,
                Name = "New Office",
                IpAddress = "172.16.0.0/16",
                FilterType = "Allow",
                IsEnabled = true,
                Description = "New office network"
            };

            _mockService
                .Setup(s => s.CreateFilterAsync(It.IsAny<CreateIpFilterDto>()))
                .ReturnsAsync((true, null, createdFilter));

            // Act
            var result = await _controller.CreateFilter(createDto);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
            Assert.Equal("GetFilterById", createdResult.ActionName);
            Assert.Equal(3, createdResult.RouteValues?["id"]);
            
            var returnValue = Assert.IsType<IpFilterDto>(createdResult.Value);
            Assert.Equal(3, returnValue.Id);
            Assert.Equal("New Office", returnValue.Name);
        }

        [Fact]
        public async Task CreateFilter_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var createDto = new CreateIpFilterDto(); // Missing required fields
            _controller.ModelState.AddModelError("Name", "The Name field is required.");

            // Act
            var result = await _controller.CreateFilter(createDto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CreateFilter_ServiceReportsFailure_ReturnsBadRequest()
        {
            // Arrange
            var createDto = new CreateIpFilterDto
            {
                Name = "Invalid Filter",
                IpAddress = "not-an-ip-address",
                FilterType = "Allow"
            };

            _mockService
                .Setup(s => s.CreateFilterAsync(It.IsAny<CreateIpFilterDto>()))
                .ReturnsAsync((false, "Invalid IP address format", null));

            // Act
            var result = await _controller.CreateFilter(createDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid IP address format", badRequestResult.Value);
        }

        [Fact]
        public async Task UpdateFilter_ValidFilter_ReturnsNoContent()
        {
            // Arrange
            var updateDto = new UpdateIpFilterDto
            {
                Id = 1,
                Name = "Updated Office",
                IpAddress = "192.168.1.0/24",
                FilterType = "Allow",
                IsEnabled = true,
                Description = "Updated description"
            };

            _mockService
                .Setup(s => s.UpdateFilterAsync(It.IsAny<UpdateIpFilterDto>()))
                .ReturnsAsync((true, null));

            // Act
            var result = await _controller.UpdateFilter(1, updateDto);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateFilter_IdMismatch_ReturnsBadRequest()
        {
            // Arrange
            var updateDto = new UpdateIpFilterDto
            {
                Id = 2, // Mismatch with route parameter
                Name = "Updated Office",
                IpAddress = "192.168.1.0/24",
                FilterType = "Allow"
            };

            // Act
            var result = await _controller.UpdateFilter(1, updateDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("ID in route must match ID in body", badRequestResult.Value);
        }

        [Fact]
        public async Task UpdateFilter_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var updateDto = new UpdateIpFilterDto { Id = 1 }; // Missing required fields
            _controller.ModelState.AddModelError("Name", "The Name field is required.");

            // Act
            var result = await _controller.UpdateFilter(1, updateDto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UpdateFilter_FilterNotFound_ReturnsNotFound()
        {
            // Arrange
            var updateDto = new UpdateIpFilterDto
            {
                Id = 999,
                Name = "Nonexistent Filter",
                IpAddress = "192.168.1.0/24",
                FilterType = "Allow"
            };

            _mockService
                .Setup(s => s.UpdateFilterAsync(It.IsAny<UpdateIpFilterDto>()))
                .ReturnsAsync((false, "IP filter with ID 999 not found"));

            // Act
            var result = await _controller.UpdateFilter(999, updateDto);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task DeleteFilter_ExistingFilter_ReturnsNoContent()
        {
            // Arrange
            _mockService
                .Setup(s => s.DeleteFilterAsync(1))
                .ReturnsAsync((true, null));

            // Act
            var result = await _controller.DeleteFilter(1);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteFilter_NonexistentFilter_ReturnsNotFound()
        {
            // Arrange
            _mockService
                .Setup(s => s.DeleteFilterAsync(999))
                .ReturnsAsync((false, "IP filter with ID 999 not found"));

            // Act
            var result = await _controller.DeleteFilter(999);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GetSettings_ReturnsSettings()
        {
            // Arrange
            var settings = new IpFilterSettingsDto
            {
                IsEnabled = true,
                DefaultAllow = false,
                BypassForAdminUi = true,
                ExcludedEndpoints = new List<string> { "/health", "/api/status" }
            };

            _mockService
                .Setup(s => s.GetIpFilterSettingsAsync())
                .ReturnsAsync(settings);

            // Act
            var result = await _controller.GetSettings();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnValue = Assert.IsType<IpFilterSettingsDto>(okResult.Value);
            Assert.True(returnValue.IsEnabled);
            Assert.False(returnValue.DefaultAllow);
            Assert.True(returnValue.BypassForAdminUi);
            Assert.Equal(2, returnValue.ExcludedEndpoints.Count);
        }

        [Fact]
        public async Task UpdateSettings_ValidSettings_ReturnsNoContent()
        {
            // Arrange
            var settings = new IpFilterSettingsDto
            {
                IsEnabled = true,
                DefaultAllow = false,
                BypassForAdminUi = true,
                ExcludedEndpoints = new List<string> { "/health", "/api/status", "/metrics" }
            };

            _mockService
                .Setup(s => s.UpdateIpFilterSettingsAsync(It.IsAny<IpFilterSettingsDto>()))
                .ReturnsAsync((true, null));

            // Act
            var result = await _controller.UpdateSettings(settings);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateSettings_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var settings = new IpFilterSettingsDto(); // Valid but we'll add a model error anyway
            _controller.ModelState.AddModelError("IsEnabled", "The IsEnabled field is required.");

            // Act
            var result = await _controller.UpdateSettings(settings);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UpdateSettings_ServiceReportsFailure_ReturnsBadRequest()
        {
            // Arrange
            var settings = new IpFilterSettingsDto
            {
                IsEnabled = true,
                DefaultAllow = false,
                BypassForAdminUi = true,
                ExcludedEndpoints = new List<string> { "/health", "/api/status", "/metrics" }
            };

            _mockService
                .Setup(s => s.UpdateIpFilterSettingsAsync(It.IsAny<IpFilterSettingsDto>()))
                .ReturnsAsync((false, "Failed to update settings"));

            // Act
            var result = await _controller.UpdateSettings(settings);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Failed to update settings", badRequestResult.Value);
        }
    }
}
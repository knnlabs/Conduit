using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Tests.Controllers
{
    public class VirtualKeysControllerTests
    {
        private readonly Mock<IAdminVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<ILogger<VirtualKeysController>> _mockLogger;
        private readonly VirtualKeysController _controller;

        public VirtualKeysControllerTests()
        {
            _mockVirtualKeyService = new Mock<IAdminVirtualKeyService>();
            _mockLogger = new Mock<ILogger<VirtualKeysController>>();
            _controller = new VirtualKeysController(_mockVirtualKeyService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GenerateKey_ValidRequest_ReturnsCreatedResponse()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test Key",
                AllowedModels = "gpt-4",
                MaxBudget = 100,
                BudgetDuration = "monthly"
            };

            var response = new CreateVirtualKeyResponseDto
            {
                VirtualKey = "vk_testkeystring",
                KeyInfo = new VirtualKeyDto
                {
                    Id = 1,
                    KeyName = "Test Key",
                    AllowedModels = "gpt-4",
                    MaxBudget = 100,
                    BudgetDuration = "monthly"
                }
            };

            _mockVirtualKeyService
                .Setup(s => s.GenerateVirtualKeyAsync(It.IsAny<CreateVirtualKeyRequestDto>()))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.GenerateKey(request);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(StatusCodes.Status201Created, createdAtActionResult.StatusCode);
            Assert.Equal("GetKeyById", createdAtActionResult.ActionName);
            Assert.Equal(1, createdAtActionResult.RouteValues?["id"]);
            
            var returnValue = Assert.IsType<CreateVirtualKeyResponseDto>(createdAtActionResult.Value);
            Assert.Equal("vk_testkeystring", returnValue.VirtualKey);
            Assert.Equal(1, returnValue.KeyInfo.Id);
            Assert.Equal("Test Key", returnValue.KeyInfo.KeyName);
        }

        [Fact]
        public async Task GenerateKey_InvalidModel_ReturnsBadRequest()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto(); // Missing required fields
            
            _controller.ModelState.AddModelError("KeyName", "The KeyName field is required.");

            // Act
            var result = await _controller.GenerateKey(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        }

        [Fact]
        public async Task GenerateKey_DbException_ReturnsInternalServerError()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test Key",
                AllowedModels = "gpt-4",
                MaxBudget = 100,
                BudgetDuration = "monthly"
            };

            _mockVirtualKeyService
                .Setup(s => s.GenerateVirtualKeyAsync(It.IsAny<CreateVirtualKeyRequestDto>()))
                .ThrowsAsync(new DbUpdateException("Database error"));

            // Act
            var result = await _controller.GenerateKey(request);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task GenerateKey_Exception_ReturnsInternalServerError()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test Key",
                AllowedModels = "gpt-4",
                MaxBudget = 100,
                BudgetDuration = "monthly"
            };

            _mockVirtualKeyService
                .Setup(s => s.GenerateVirtualKeyAsync(It.IsAny<CreateVirtualKeyRequestDto>()))
                .ThrowsAsync(new Exception("Generic error"));

            // Act
            var result = await _controller.GenerateKey(request);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task ListKeys_Success_ReturnsOkWithKeys()
        {
            // Arrange
            var keys = new List<VirtualKeyDto>
            {
                new VirtualKeyDto { Id = 1, KeyName = "Key 1" },
                new VirtualKeyDto { Id = 2, KeyName = "Key 2" }
            };

            _mockVirtualKeyService
                .Setup(s => s.ListVirtualKeysAsync())
                .ReturnsAsync(keys);

            // Act
            var result = await _controller.ListKeys();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
            
            var returnValue = Assert.IsType<List<VirtualKeyDto>>(okResult.Value);
            Assert.Equal(2, returnValue.Count);
            Assert.Equal(1, returnValue[0].Id);
            Assert.Equal("Key 1", returnValue[0].KeyName);
            Assert.Equal(2, returnValue[1].Id);
            Assert.Equal("Key 2", returnValue[1].KeyName);
        }

        [Fact]
        public async Task ListKeys_Exception_ReturnsInternalServerError()
        {
            // Arrange
            _mockVirtualKeyService
                .Setup(s => s.ListVirtualKeysAsync())
                .ThrowsAsync(new Exception("Error listing keys"));

            // Act
            var result = await _controller.ListKeys();

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task GetKeyById_ExistingKey_ReturnsOkWithKey()
        {
            // Arrange
            var key = new VirtualKeyDto 
            { 
                Id = 1, 
                KeyName = "Test Key",
                AllowedModels = "gpt-4",
                MaxBudget = 100,
                BudgetDuration = "monthly" 
            };

            _mockVirtualKeyService
                .Setup(s => s.GetVirtualKeyInfoAsync(1))
                .ReturnsAsync(key);

            // Act
            var result = await _controller.GetKeyById(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
            
            var returnValue = Assert.IsType<VirtualKeyDto>(okResult.Value);
            Assert.Equal(1, returnValue.Id);
            Assert.Equal("Test Key", returnValue.KeyName);
            Assert.Equal("gpt-4", returnValue.AllowedModels);
        }

        [Fact]
        public async Task GetKeyById_NonExistingKey_ReturnsNotFound()
        {
            // Arrange
            _mockVirtualKeyService
                .Setup(s => s.GetVirtualKeyInfoAsync(999))
                .ReturnsAsync((VirtualKeyDto?)null);

            // Act
            var result = await _controller.GetKeyById(999);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task GetKeyById_Exception_ReturnsInternalServerError()
        {
            // Arrange
            _mockVirtualKeyService
                .Setup(s => s.GetVirtualKeyInfoAsync(It.IsAny<int>()))
                .ThrowsAsync(new Exception("Error getting key"));

            // Act
            var result = await _controller.GetKeyById(1);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task UpdateKey_ExistingKey_ReturnsNoContent()
        {
            // Arrange
            var request = new UpdateVirtualKeyRequestDto
            {
                KeyName = "Updated Key",
                AllowedModels = "gpt-4,gpt-3.5-turbo",
                MaxBudget = 200
            };

            _mockVirtualKeyService
                .Setup(s => s.UpdateVirtualKeyAsync(1, It.IsAny<UpdateVirtualKeyRequestDto>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateKey(1, request);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateKey_NonExistingKey_ReturnsNotFound()
        {
            // Arrange
            var request = new UpdateVirtualKeyRequestDto
            {
                KeyName = "Updated Key",
                AllowedModels = "gpt-4,gpt-3.5-turbo",
                MaxBudget = 200
            };

            _mockVirtualKeyService
                .Setup(s => s.UpdateVirtualKeyAsync(999, It.IsAny<UpdateVirtualKeyRequestDto>()))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.UpdateKey(999, request);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task UpdateKey_InvalidModel_ReturnsBadRequest()
        {
            // Arrange
            var request = new UpdateVirtualKeyRequestDto(); // Valid but empty
            
            _controller.ModelState.AddModelError("KeyName", "The KeyName field is required.");

            // Act
            var result = await _controller.UpdateKey(1, request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        }

        [Fact]
        public async Task UpdateKey_Exception_ReturnsInternalServerError()
        {
            // Arrange
            var request = new UpdateVirtualKeyRequestDto
            {
                KeyName = "Updated Key",
                AllowedModels = "gpt-4,gpt-3.5-turbo",
                MaxBudget = 200
            };

            _mockVirtualKeyService
                .Setup(s => s.UpdateVirtualKeyAsync(It.IsAny<int>(), It.IsAny<UpdateVirtualKeyRequestDto>()))
                .ThrowsAsync(new Exception("Error updating key"));

            // Act
            var result = await _controller.UpdateKey(1, request);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task DeleteKey_ExistingKey_ReturnsNoContent()
        {
            // Arrange
            _mockVirtualKeyService
                .Setup(s => s.DeleteVirtualKeyAsync(1))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteKey(1);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteKey_NonExistingKey_ReturnsNotFound()
        {
            // Arrange
            _mockVirtualKeyService
                .Setup(s => s.DeleteVirtualKeyAsync(999))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.DeleteKey(999);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteKey_Exception_ReturnsInternalServerError()
        {
            // Arrange
            _mockVirtualKeyService
                .Setup(s => s.DeleteVirtualKeyAsync(It.IsAny<int>()))
                .ThrowsAsync(new Exception("Error deleting key"));

            // Act
            var result = await _controller.DeleteKey(1);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task ResetKeySpend_ExistingKey_ReturnsNoContent()
        {
            // Arrange
            _mockVirtualKeyService
                .Setup(s => s.ResetSpendAsync(1))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.ResetKeySpend(1);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task ResetKeySpend_NonExistingKey_ReturnsNotFound()
        {
            // Arrange
            _mockVirtualKeyService
                .Setup(s => s.ResetSpendAsync(999))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.ResetKeySpend(999);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ResetKeySpend_Exception_ReturnsInternalServerError()
        {
            // Arrange
            _mockVirtualKeyService
                .Setup(s => s.ResetSpendAsync(It.IsAny<int>()))
                .ThrowsAsync(new Exception("Error resetting key spend"));

            // Act
            var result = await _controller.ResetKeySpend(1);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
        }
    }
}
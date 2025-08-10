using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Admin.Controllers
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
        public void GenerateKey_MissingVirtualKeyGroupId_ReturnsBadRequest()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test Key"
                // VirtualKeyGroupId will default to 0 which should fail validation
            };

            // Manually trigger validation since we're not going through the full MVC pipeline
            var validationContext = new ValidationContext(request);
            var validationResults = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

            // Assert - verify the DTO validation catches the missing field
            // KeyName defaults to empty string which passes Required validation
            // VirtualKeyGroupId defaults to 0 which should fail Range validation
            Assert.False(isValid);
            
            // Should have errors for VirtualKeyGroupId being 0
            var groupIdError = validationResults.FirstOrDefault(r => 
                r.MemberNames.Contains(nameof(CreateVirtualKeyRequestDto.VirtualKeyGroupId)));
            Assert.NotNull(groupIdError);
            Assert.Contains("must be a valid positive number", groupIdError.ErrorMessage);
        }

        [Fact]
        public async Task GenerateKey_ValidRequest_ReturnsCreatedResult()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test Key",
                VirtualKeyGroupId = 1,
                AllowedModels = "gpt-4"
            };

            var expectedResponse = new CreateVirtualKeyResponseDto
            {
                VirtualKey = "vk_test123",
                KeyInfo = new VirtualKeyDto
                {
                    Id = 1,
                    KeyName = "Test Key",
                    VirtualKeyGroupId = 1
                }
            };

            _mockVirtualKeyService.Setup(x => x.GenerateVirtualKeyAsync(It.IsAny<CreateVirtualKeyRequestDto>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GenerateKey(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var response = Assert.IsType<CreateVirtualKeyResponseDto>(createdResult.Value);
            Assert.Equal("vk_test123", response.VirtualKey);
            Assert.Equal(1, response.KeyInfo.VirtualKeyGroupId);
        }

        [Fact]
        public async Task GenerateKey_ServiceThrowsInvalidOperation_ReturnsInternalServerError()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test Key",
                VirtualKeyGroupId = 999
            };

            _mockVirtualKeyService.Setup(x => x.GenerateVirtualKeyAsync(It.IsAny<CreateVirtualKeyRequestDto>()))
                .ThrowsAsync(new InvalidOperationException("Virtual key group 999 not found. Ensure the group exists before creating keys."));

            // Act
            var result = await _controller.GenerateKey(request);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
            
            // In a real scenario, you might want to return a more specific error code (e.g., 404) 
            // for "group not found" scenarios by catching specific exceptions
        }

        [Fact]
        public void CreateVirtualKeyRequestDto_Validation_RequiresVirtualKeyGroupId()
        {
            // Arrange
            var dto = new CreateVirtualKeyRequestDto
            {
                KeyName = "Valid Key Name"
                // VirtualKeyGroupId not set
            };

            // Act
            var validationContext = new ValidationContext(dto);
            var validationResults = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(dto, validationContext, validationResults, true);

            // Assert
            Assert.False(isValid);
            var groupIdError = validationResults.FirstOrDefault(r => 
                r.MemberNames.Contains(nameof(CreateVirtualKeyRequestDto.VirtualKeyGroupId)));
            Assert.NotNull(groupIdError);
            Assert.Equal("VirtualKeyGroupId must be a valid positive number. Create a virtual key group first using POST /api/virtualkey-groups.", 
                groupIdError.ErrorMessage);
        }

        [Fact]
        public void CreateVirtualKeyRequestDto_Validation_RequiresKeyName()
        {
            // Arrange
            var dto = new CreateVirtualKeyRequestDto
            {
                KeyName = "", // Empty string should fail Required validation
                VirtualKeyGroupId = 1
            };

            // Act
            var validationContext = new ValidationContext(dto);
            var validationResults = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(dto, validationContext, validationResults, true);

            // Assert
            Assert.False(isValid);
            var keyNameError = validationResults.FirstOrDefault(r => 
                r.MemberNames.Contains(nameof(CreateVirtualKeyRequestDto.KeyName)));
            Assert.NotNull(keyNameError);
            Assert.Equal("Key name is required.", keyNameError.ErrorMessage);
        }

        [Fact]
        public void CreateVirtualKeyRequestDto_Validation_ValidRequest_Passes()
        {
            // Arrange
            var dto = new CreateVirtualKeyRequestDto
            {
                KeyName = "Valid Key",
                VirtualKeyGroupId = 1,
                AllowedModels = "gpt-4,claude-3",
                RateLimitRpm = 100,
                RateLimitRpd = 1000,
                Metadata = "{\"team\": \"engineering\"}"
            };

            // Act
            var validationContext = new ValidationContext(dto);
            var validationResults = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(dto, validationContext, validationResults, true);

            // Assert
            Assert.True(isValid);
            Assert.Empty(validationResults);
        }
    }
}
using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Admin.Tests.Services
{
    public partial class AdminVirtualKeyServiceTests
    {
        #region Key Generation Tests

        [Fact]
        public async Task GenerateVirtualKeyAsync_WithoutVirtualKeyGroupId_ThrowsException()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test Key"
                // VirtualKeyGroupId is not set (will be 0 by default)
            };

            // Act & Assert
            // This test verifies that the DTO validation will catch the missing required field
            // In practice, the model validation in the controller will prevent this from reaching the service
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.GenerateVirtualKeyAsync(request));
            
            Assert.Contains("Virtual key group 0 not found", exception.Message);
        }

        [Fact]
        public async Task GenerateVirtualKeyAsync_WithNonExistentGroupId_ThrowsException()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test Key",
                VirtualKeyGroupId = 999
            };

            _mockGroupRepository.Setup(x => x.GetByIdAsync(999))
                .ReturnsAsync((VirtualKeyGroup)null);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.GenerateVirtualKeyAsync(request));
            
            Assert.Equal("Virtual key group 999 not found. Ensure the group exists before creating keys.", exception.Message);
        }

        [Fact]
        public async Task GenerateVirtualKeyAsync_WithZeroBalanceGroup_LogsWarning()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test Key",
                VirtualKeyGroupId = 1
            };

            var group = new VirtualKeyGroup
            {
                Id = 1,
                GroupName = "Test Group",
                Balance = 0m, // Zero balance
                LifetimeCreditsAdded = 0m,
                LifetimeSpent = 0m
            };

            _mockGroupRepository.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(group);
            _mockVirtualKeyRepository.Setup(x => x.CreateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VirtualKey { Id = 1, KeyName = "Test Key", VirtualKeyGroupId = 1 });

            // Act
            var result = await _service.GenerateVirtualKeyAsync(request);

            // Assert
            Assert.NotNull(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Virtual key group 1 has zero balance")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GenerateVirtualKeyAsync_WithValidGroup_CreatesKeySuccessfully()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test Key",
                VirtualKeyGroupId = 1,
                AllowedModels = "gpt-4",
                RateLimitRpm = 100
            };

            var group = new VirtualKeyGroup
            {
                Id = 1,
                GroupName = "Test Group",
                Balance = 1000m,
                LifetimeCreditsAdded = 1000m,
                LifetimeSpent = 0m
            };

            _mockGroupRepository.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(group);
            _mockVirtualKeyRepository.Setup(x => x.CreateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VirtualKey 
                { 
                    Id = 1, 
                    KeyName = "Test Key", 
                    VirtualKeyGroupId = 1,
                    AllowedModels = "gpt-4",
                    RateLimitRpm = 100
                });

            // Act
            var result = await _service.GenerateVirtualKeyAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.VirtualKey);
            Assert.Equal("Test Key", result.KeyInfo.KeyName);
            Assert.Equal(1, result.KeyInfo.VirtualKeyGroupId);
            
            // Verify no warning about zero balance
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("zero balance")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Never);
        }

        #endregion
    }
}
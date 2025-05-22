using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.WebUI.Providers
{
    public class AdminApiClientVirtualKeyTests
    {
        private readonly Mock<IAdminApiClient> _adminApiClientMock;
        private readonly Mock<ILogger<AdminApiClient>> _loggerMock;
        private readonly IVirtualKeyService _virtualKeyService;

        public AdminApiClientVirtualKeyTests()
        {
            // Create a mock that implements both interfaces
            _adminApiClientMock = new Mock<IAdminApiClient>();
            Mock<IVirtualKeyService> mockService = _adminApiClientMock.As<IVirtualKeyService>();
            _loggerMock = new Mock<ILogger<AdminApiClient>>();
            
            // Use the mock as the service
            _virtualKeyService = mockService.Object;
        }

        [Fact]
        public async Task ListVirtualKeysAsync_ReturnsKeys_WhenApiReturnsData()
        {
            // Arrange
            var expectedKeys = new List<VirtualKeyDto>
            {
                new VirtualKeyDto { Id = 1, KeyName = "Test Key 1" },
                new VirtualKeyDto { Id = 2, KeyName = "Test Key 2" }
            };

            _adminApiClientMock.Setup(c => c.GetAllVirtualKeysAsync())
                .ReturnsAsync(expectedKeys);

            // Act
            var result = await _virtualKeyService.ListVirtualKeysAsync();

            // Assert
            Assert.Equal(expectedKeys.Count, result.Count);
            Assert.Equal(expectedKeys[0].Id, result[0].Id);
            Assert.Equal(expectedKeys[0].KeyName, result[0].KeyName);
            Assert.Equal(expectedKeys[1].Id, result[1].Id);
            Assert.Equal(expectedKeys[1].KeyName, result[1].KeyName);
            _adminApiClientMock.Verify(c => c.GetAllVirtualKeysAsync(), Times.Once);
        }

        [Fact]
        public async Task GetVirtualKeyInfoAsync_ReturnsKey_WhenKeyExists()
        {
            // Arrange
            var expectedKey = new VirtualKeyDto { 
                Id = 1, 
                KeyName = "Test Key",
                ApiKey = "vk-123456",
                CreatedAt = DateTime.UtcNow,
                BudgetAmount = 100,
                CurrentSpend = 50
            };

            _adminApiClientMock.Setup(c => c.GetVirtualKeyByIdAsync(1))
                .ReturnsAsync(expectedKey);

            // Act
            var result = await _virtualKeyService.GetVirtualKeyInfoAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedKey.Id, result.Id);
            Assert.Equal(expectedKey.KeyName, result.KeyName);
            Assert.Equal(expectedKey.ApiKey, result.ApiKey);
            Assert.Equal(expectedKey.CreatedAt, result.CreatedAt);
            Assert.Equal(expectedKey.BudgetAmount, result.BudgetAmount);
            Assert.Equal(expectedKey.CurrentSpend, result.CurrentSpend);
            _adminApiClientMock.Verify(c => c.GetVirtualKeyByIdAsync(1), Times.Once);
        }

        [Fact]
        public async Task GenerateVirtualKeyAsync_ReturnsKeyInfo_WhenCreationSucceeds()
        {
            // Arrange
            var createDto = new CreateVirtualKeyRequestDto { 
                KeyName = "New Key",
                BudgetAmount = 100,
                BudgetPeriod = BudgetPeriod.Monthly,
                IsEnabled = true
            };
            
            var keyInfo = new VirtualKeyDto { 
                Id = 1, 
                KeyName = "New Key",
                BudgetAmount = 100,
                BudgetPeriod = BudgetPeriod.Monthly,
                IsEnabled = true
            };
            
            var expectedResponse = new CreateVirtualKeyResponseDto { 
                VirtualKey = "vk-123456", 
                KeyInfo = keyInfo 
            };

            _adminApiClientMock.Setup(c => c.CreateVirtualKeyAsync(createDto))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _virtualKeyService.GenerateVirtualKeyAsync(createDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedResponse.VirtualKey, result.VirtualKey);
            Assert.Equal(expectedResponse.KeyInfo.Id, result.KeyInfo.Id);
            Assert.Equal(expectedResponse.KeyInfo.KeyName, result.KeyInfo.KeyName);
            _adminApiClientMock.Verify(c => c.CreateVirtualKeyAsync(createDto), Times.Once);
        }

        [Fact]
        public async Task UpdateVirtualKeyAsync_ReturnsTrue_WhenUpdateSucceeds()
        {
            // Arrange
            var updateDto = new UpdateVirtualKeyRequestDto { 
                KeyName = "Updated Key",
                BudgetAmount = 200,
                BudgetPeriod = BudgetPeriod.Weekly,
                IsEnabled = false
            };

            _adminApiClientMock.Setup(c => c.UpdateVirtualKeyAsync(1, updateDto))
                .ReturnsAsync(true);

            // Act
            var result = await _virtualKeyService.UpdateVirtualKeyAsync(1, updateDto);

            // Assert
            Assert.True(result);
            _adminApiClientMock.Verify(c => c.UpdateVirtualKeyAsync(1, updateDto), Times.Once);
        }

        [Fact]
        public async Task DeleteVirtualKeyAsync_ReturnsTrue_WhenDeletionSucceeds()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.DeleteVirtualKeyAsync(1))
                .ReturnsAsync(true);

            // Act
            var result = await _virtualKeyService.DeleteVirtualKeyAsync(1);

            // Assert
            Assert.True(result);
            _adminApiClientMock.Verify(c => c.DeleteVirtualKeyAsync(1), Times.Once);
        }

        [Fact]
        public async Task ResetSpendAsync_ReturnsTrue_WhenResetSucceeds()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.ResetVirtualKeySpendAsync(1))
                .ReturnsAsync(true);

            // Act
            var result = await _virtualKeyService.ResetSpendAsync(1);

            // Assert 
            Assert.True(result);
            _adminApiClientMock.Verify(c => c.ResetVirtualKeySpendAsync(1), Times.Once);
        }

        [Fact]
        public async Task UpdateSpendAsync_ReturnsTrue_WhenUpdateSucceeds()
        {
            // Arrange
            decimal cost = 10.5m;
            _adminApiClientMock.Setup(c => c.UpdateVirtualKeySpendAsync(1, cost))
                .ReturnsAsync(true);

            // Act
            var result = await _virtualKeyService.UpdateSpendAsync(1, cost);

            // Assert
            Assert.True(result);
            _adminApiClientMock.Verify(c => c.UpdateVirtualKeySpendAsync(1, cost), Times.Once);
        }

        [Fact]
        public async Task ValidateVirtualKeyAsync_ReturnsValidationInfo_WhenKeyIsValid()
        {
            // Arrange
            string key = "vk-123456";
            string requestedModel = "gpt-4";
            
            var validationResult = new VirtualKeyValidationResult { 
                IsValid = true,
                VirtualKeyId = 1
            };
            
            var validationInfo = new VirtualKeyValidationInfoDto {
                Id = 1,
                KeyName = "Test Key",
                IsEnabled = true,
                AllowedModels = new List<string> { "gpt-4" }
            };

            _adminApiClientMock.Setup(c => c.ValidateVirtualKeyAsync(key, requestedModel))
                .ReturnsAsync(validationResult);
                
            _adminApiClientMock.Setup(c => c.GetVirtualKeyValidationInfoAsync(1))
                .ReturnsAsync(validationInfo);

            // Act
            var result = await _virtualKeyService.ValidateVirtualKeyAsync(key, requestedModel);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(validationInfo.Id, result.Id);
            Assert.Equal(validationInfo.KeyName, result.KeyName);
            Assert.Equal(validationInfo.IsEnabled, result.IsEnabled);
            Assert.Contains("gpt-4", result.AllowedModels);
            _adminApiClientMock.Verify(c => c.ValidateVirtualKeyAsync(key, requestedModel), Times.Once);
            _adminApiClientMock.Verify(c => c.GetVirtualKeyValidationInfoAsync(1), Times.Once);
        }

        [Fact]
        public async Task GetVirtualKeyInfoForValidationAsync_ReturnsValidationInfo_WhenKeyExists()
        {
            // Arrange
            var validationInfo = new VirtualKeyValidationInfoDto {
                Id = 1,
                KeyName = "Test Key",
                IsEnabled = true,
                AllowedModels = new List<string> { "gpt-4" }
            };

            _adminApiClientMock.Setup(c => c.GetVirtualKeyValidationInfoAsync(1))
                .ReturnsAsync(validationInfo);

            // Act
            var result = await _virtualKeyService.GetVirtualKeyInfoForValidationAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(validationInfo.Id, result.Id);
            Assert.Equal(validationInfo.KeyName, result.KeyName);
            Assert.Equal(validationInfo.IsEnabled, result.IsEnabled);
            Assert.Contains("gpt-4", result.AllowedModels);
            _adminApiClientMock.Verify(c => c.GetVirtualKeyValidationInfoAsync(1), Times.Once);
        }

        [Fact]
        public async Task ResetBudgetIfExpiredAsync_ReturnsWasReset_WhenCheckSucceeds()
        {
            // Arrange
            var budgetCheckResult = new BudgetCheckResult {
                WasReset = true,
                IsWithinBudget = true
            };

            _adminApiClientMock.Setup(c => c.CheckVirtualKeyBudgetAsync(1))
                .ReturnsAsync(budgetCheckResult);

            // Act
            var result = await _virtualKeyService.ResetBudgetIfExpiredAsync(1);

            // Assert
            Assert.True(result);
            _adminApiClientMock.Verify(c => c.CheckVirtualKeyBudgetAsync(1), Times.Once);
        }

        [Fact]
        public async Task PerformMaintenanceAsync_CallsAdminApi()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.PerformVirtualKeyMaintenanceAsync())
                .Returns(Task.CompletedTask);

            // Act
            await _virtualKeyService.PerformMaintenanceAsync();

            // Assert
            _adminApiClientMock.Verify(c => c.PerformVirtualKeyMaintenanceAsync(), Times.Once);
        }
    }
}
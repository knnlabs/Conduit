using ConduitLLM.WebUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.WebUI.Services.Adapters;
using ConduitLLM.WebUI.Extensions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;
using Xunit;

namespace ConduitLLM.Tests.RepositoryServices
{
    public class VirtualKeyServiceTests
    {
        private readonly Mock<IAdminApiClient> _mockAdminApiClient;
        private readonly Mock<ILogger<VirtualKeyServiceAdapter>> _mockLogger;
        private readonly VirtualKeyServiceAdapter _service;

        public VirtualKeyServiceTests()
        {
            _mockAdminApiClient = new Mock<IAdminApiClient>();
            _mockLogger = new Mock<ILogger<VirtualKeyServiceAdapter>>();
            _service = new VirtualKeyServiceAdapter(_mockAdminApiClient.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GenerateVirtualKeyAsync_ShouldCreateKeyAndReturnResponse()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test Key",
                AllowedModels = "gpt-4,claude-v1",
                MaxBudget = 100.0m,
                BudgetDuration = "monthly",
                RateLimitRpm = 60,
                RateLimitRpd = 1000,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };
            
            var keyInfo = new VirtualKeyDto
            {
                Id = 1,
                KeyName = "Test Key",
                AllowedModels = "gpt-4,claude-v1",
                MaxBudget = 100.0m,
                BudgetDuration = "monthly",
                RateLimitRpm = 60,
                RateLimitRpd = 1000,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow
            };
            
            var response = new CreateVirtualKeyResponseDto
            {
                VirtualKey = "vk-1234567890abcdef",
                KeyInfo = keyInfo
            };
            
            _mockAdminApiClient.Setup(api => api.CreateVirtualKeyAsync(request))
                .ReturnsAsync(response);
            
            // Act
            var result = await _service.GenerateVirtualKeyAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("vk-1234567890abcdef", result.VirtualKey);
            Assert.NotNull(result.KeyInfo);
            Assert.Equal(1, result.KeyInfo.Id);
            Assert.Equal("Test Key", result.KeyInfo.KeyName);
        }

        [Fact]
        public async Task GetVirtualKeyInfoAsync_ShouldReturnKeyInfo()
        {
            // Arrange
            int keyId = 1;
            var keyInfo = new VirtualKeyDto
            {
                Id = keyId,
                Name = "Test Key",
                AllowedModels = "gpt-4,claude-v1",
                MaxBudget = 100.0m,
                BudgetDuration = "monthly",
                RateLimitRpm = 60,
                RateLimitRpd = 1000,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            
            _mockAdminApiClient.Setup(api => api.GetVirtualKeyByIdAsync(keyId))
                .ReturnsAsync(keyInfo);
            
            // Act
            var result = await _service.GetVirtualKeyInfoAsync(keyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(keyId, result.Id);
            Assert.Equal("Test Key", result.Name);
        }

        [Fact]
        public async Task ListVirtualKeysAsync_ShouldReturnAllKeys()
        {
            // Arrange
            var keys = new List<VirtualKeyDto>
            {
                new VirtualKeyDto
                {
                    Id = 1,
                    Name = "Test Key 1",
                    IsActive = true
                },
                new VirtualKeyDto
                {
                    Id = 2,
                    Name = "Test Key 2",
                    IsActive = false
                }
            };
            
            _mockAdminApiClient.Setup(api => api.GetAllVirtualKeysAsync())
                .ReturnsAsync(keys);
            
            // Act
            var result = await _service.ListVirtualKeysAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("Test Key 1", result[0].Name);
            Assert.Equal("Test Key 2", result[1].Name);
        }

        [Fact]
        public async Task UpdateVirtualKeyAsync_ShouldUpdateKey()
        {
            // Arrange
            int keyId = 1;
            var request = new UpdateVirtualKeyRequestDto
            {
                KeyName = "Updated Key",
                AllowedModels = "gpt-4,claude-v1,gemini-pro",
                MaxBudget = 200.0m
            };
            
            _mockAdminApiClient.Setup(api => api.UpdateVirtualKeyAsync(keyId, request))
                .ReturnsAsync(true);
            
            // Act
            var result = await _service.UpdateVirtualKeyAsync(keyId, request);

            // Assert
            Assert.True(result);
            _mockAdminApiClient.Verify(api => api.UpdateVirtualKeyAsync(keyId, request), Times.Once);
        }

        [Fact]
        public async Task DeleteVirtualKeyAsync_ShouldDeleteKey()
        {
            // Arrange
            int keyId = 1;
            
            _mockAdminApiClient.Setup(api => api.DeleteVirtualKeyAsync(keyId))
                .ReturnsAsync(true);
            
            // Act
            var result = await _service.DeleteVirtualKeyAsync(keyId);

            // Assert
            Assert.True(result);
            _mockAdminApiClient.Verify(api => api.DeleteVirtualKeyAsync(keyId), Times.Once);
        }

        [Fact]
        public async Task ResetSpendAsync_ShouldResetKeySpend()
        {
            // Arrange
            int keyId = 1;
            
            _mockAdminApiClient.Setup(api => api.ResetVirtualKeySpendAsync(keyId))
                .ReturnsAsync(true);
            
            // Act
            var result = await _service.ResetSpendAsync(keyId);

            // Assert
            Assert.True(result);
            _mockAdminApiClient.Verify(api => api.ResetVirtualKeySpendAsync(keyId), Times.Once);
        }

        [Fact]
        public async Task ValidateVirtualKeyAsync_ShouldReturnValidationInfo()
        {
            // Arrange
            string key = "vk-1234567890abcdef";
            string model = "gpt-4";
            
            var validationResult = new VirtualKeyValidationResult
            {
                IsValid = true,
                VirtualKeyId = 1
            };
            
            var validationInfo = new VirtualKeyValidationInfoDto
            {
                Id = 1,
                KeyName = "Test Key",
                AllowedModels = "gpt-4,claude-v1",
                MaxBudget = 100.0m,
                CurrentSpend = 50.0m,
                IsEnabled = true
            };
            
            _mockAdminApiClient.Setup(api => api.ValidateVirtualKeyAsync(key, model))
                .ReturnsAsync(validationResult);
                
            _mockAdminApiClient.Setup(api => api.GetVirtualKeyValidationInfoAsync(1))
                .ReturnsAsync(validationInfo);
            
            // Act
            var result = await _service.ValidateVirtualKeyAsync(key, model);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Test Key", result.KeyName);
            Assert.Equal(50.0m, result.CurrentSpend);
        }

        [Fact]
        public async Task UpdateSpendAsync_ShouldUpdateKeySpend()
        {
            // Arrange
            int keyId = 1;
            decimal cost = 10.5m;
            
            _mockAdminApiClient.Setup(api => api.UpdateVirtualKeySpendAsync(keyId, cost))
                .ReturnsAsync(true);
            
            // Act
            var result = await _service.UpdateSpendAsync(keyId, cost);

            // Assert
            Assert.True(result);
            _mockAdminApiClient.Verify(api => api.UpdateVirtualKeySpendAsync(keyId, cost), Times.Once);
        }

        [Fact]
        public async Task ResetBudgetIfExpiredAsync_ShouldResetBudget()
        {
            // Arrange
            int keyId = 1;
            
            var budgetCheckResult = new BudgetCheckResult { WasReset = true };
            
            _mockAdminApiClient.Setup(api => api.CheckVirtualKeyBudgetAsync(keyId))
                .ReturnsAsync(budgetCheckResult);
            
            // Act
            var result = await _service.ResetBudgetIfExpiredAsync(keyId);

            // Assert
            Assert.True(result);
            _mockAdminApiClient.Verify(api => api.CheckVirtualKeyBudgetAsync(keyId), Times.Once);
        }

        [Fact]
        public async Task GetVirtualKeyInfoForValidationAsync_ShouldReturnValidationInfo()
        {
            // Arrange
            int keyId = 1;
            
            var validationInfo = new VirtualKeyValidationInfoDto
            {
                Id = keyId,
                KeyName = "Test Key",
                AllowedModels = "gpt-4,claude-v1",
                MaxBudget = 100.0m,
                CurrentSpend = 50.0m,
                IsEnabled = true
            };
            
            _mockAdminApiClient.Setup(api => api.GetVirtualKeyValidationInfoAsync(keyId))
                .ReturnsAsync(validationInfo);
            
            // Act
            var result = await _service.GetVirtualKeyInfoForValidationAsync(keyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(keyId, result.Id);
            Assert.Equal("Test Key", result.KeyName);
            Assert.Equal(50.0m, result.CurrentSpend);
        }

        [Fact]
        public async Task PerformMaintenanceAsync_ShouldCallMaintenanceApiMethod()
        {
            // Arrange
            _mockAdminApiClient.Setup(api => api.PerformVirtualKeyMaintenanceAsync())
                .Returns(Task.CompletedTask);
            
            // Act
            await _service.PerformMaintenanceAsync();

            // Assert
            _mockAdminApiClient.Verify(api => api.PerformVirtualKeyMaintenanceAsync(), Times.Once);
        }
    }
}
using ConduitLLM.WebUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.WebUI.Extensions;
using ConduitLLM.WebUI.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Tests.WebUI.Extensions;

namespace ConduitLLM.Tests.RepositoryServices
{
    public class RepositoryVirtualKeyServiceTests
    {
        private readonly Mock<IVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<ILogger<RepositoryVirtualKeyService>> _mockLogger;
        private readonly RepositoryVirtualKeyService _service;

        public RepositoryVirtualKeyServiceTests()
        {
            _mockVirtualKeyService = new Mock<IVirtualKeyService>();
            _mockLogger = new Mock<ILogger<RepositoryVirtualKeyService>>();
            _service = new RepositoryVirtualKeyService(_mockVirtualKeyService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GenerateVirtualKeyAsync_ShouldCallUnderlyingService()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test Key",
                AllowedModels = "gpt-4,claude-v1",
                MaxBudget = 100.0m
            };

            var expectedResponse = new CreateVirtualKeyResponseDto
            {
                VirtualKey = "vk-test123",
                KeyInfo = new VirtualKeyDto { Id = 1, Name = "Test Key" }
            };

            _mockVirtualKeyService.Setup(s => s.GenerateVirtualKeyAsync(request))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _service.GenerateVirtualKeyAsync(request);

            // Assert
            Assert.Same(expectedResponse, result);
            _mockVirtualKeyService.Verify(s => s.GenerateVirtualKeyAsync(request), Times.Once);
        }

        [Fact]
        public async Task GetVirtualKeyInfoAsync_ShouldCallUnderlyingService()
        {
            // Arrange
            int keyId = 123;
            var expectedDto = new VirtualKeyDto { Id = keyId, Name = "Test Key" };

            _mockVirtualKeyService.Setup(s => s.GetVirtualKeyInfoAsync(keyId))
                .ReturnsAsync(expectedDto);

            // Act
            var result = await _service.GetVirtualKeyInfoAsync(keyId);

            // Assert
            Assert.Same(expectedDto, result);
            _mockVirtualKeyService.Verify(s => s.GetVirtualKeyInfoAsync(keyId), Times.Once);
        }

        [Fact]
        public async Task ListVirtualKeysAsync_ShouldCallUnderlyingService()
        {
            // Arrange
            var expectedKeys = new List<VirtualKeyDto>
            {
                new VirtualKeyDto { Id = 1, Name = "Key 1" },
                new VirtualKeyDto { Id = 2, Name = "Key 2" }
            };

            _mockVirtualKeyService.Setup(s => s.ListVirtualKeysAsync())
                .ReturnsAsync(expectedKeys);

            // Act
            var result = await _service.ListVirtualKeysAsync();

            // Assert
            Assert.Same(expectedKeys, result);
            _mockVirtualKeyService.Verify(s => s.ListVirtualKeysAsync(), Times.Once);
        }

        [Fact]
        public async Task ValidateVirtualKeyAsync_ShouldCallUnderlyingService()
        {
            // Arrange
            string key = "vk-test123";
            string requestedModel = "gpt-4";
            var validationInfoDto = new VirtualKeyValidationInfoDto { Id = 1, KeyName = "Test Key" };

            _mockVirtualKeyService.Setup(s => s.ValidateVirtualKeyAsync(key, requestedModel))
                .ReturnsAsync(validationInfoDto);

            // Act
            var result = await _service.ValidateVirtualKeyAsync(key, requestedModel);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(validationInfoDto.Id, result.Id);
            _mockVirtualKeyService.Verify(s => s.ValidateVirtualKeyAsync(key, requestedModel), Times.Once);
        }

        [Fact]
        public async Task ResetBudgetIfExpiredAsync_ShouldCallUnderlyingService()
        {
            // Arrange
            int keyId = 123;
            var cancellationToken = CancellationToken.None;
            
            _mockVirtualKeyService.Setup(s => s.ResetBudgetIfExpiredAsync(keyId, cancellationToken))
                .ReturnsAsync(true);

            // Act
            var result = await _service.ResetBudgetIfExpiredAsync(keyId, cancellationToken);

            // Assert
            Assert.True(result);
            _mockVirtualKeyService.Verify(s => s.ResetBudgetIfExpiredAsync(keyId, cancellationToken), Times.Once);
        }

        [Fact]
        public async Task GetVirtualKeyInfoForValidationAsync_ShouldCallUnderlyingService()
        {
            // Arrange
            int keyId = 123;
            var cancellationToken = CancellationToken.None;
            var validationInfoDto = new VirtualKeyValidationInfoDto { Id = keyId, KeyName = "Test Key" };
            
            _mockVirtualKeyService.Setup(s => s.GetVirtualKeyInfoForValidationAsync(keyId, cancellationToken))
                .ReturnsAsync(validationInfoDto);

            // Act
            var result = await _service.GetVirtualKeyInfoForValidationAsync(keyId, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(validationInfoDto.Id, result.Id);
            _mockVirtualKeyService.Verify(s => s.GetVirtualKeyInfoForValidationAsync(keyId, cancellationToken), Times.Once);
        }
    }
}
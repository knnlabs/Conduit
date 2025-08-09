using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Admin.Tests.Services
{
    public partial class AdminVirtualKeyServiceTests
    {
        #region GenerateVirtualKeyAsync Tests

        [Fact]
        public async Task GenerateVirtualKeyAsync_ValidRequest_CreatesKeySuccessfully()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test API Key",
                AllowedModels = "gpt-4,claude-3",
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                RateLimitRpm = 60,
                RateLimitRpd = 1000,
                VirtualKeyGroupId = 1
            };
            
            // Mock the group exists
            _mockGroupRepository.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(new VirtualKeyGroup { Id = 1, GroupName = "Test Group", Balance = 100m });

            _mockVirtualKeyRepository.Setup(x => x.CreateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VirtualKey
                {
                    Id = 1,
                    KeyName = request.KeyName,
                    KeyHash = "someHash",
                    IsEnabled = true,
                    AllowedModels = request.AllowedModels,
                    VirtualKeyGroupId = 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

            // Act
            var result = await _service.GenerateVirtualKeyAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.VirtualKey);
            Assert.StartsWith(VirtualKeyConstants.KeyPrefix, result.VirtualKey);
            Assert.NotNull(result.KeyInfo);
            Assert.Equal(1, result.KeyInfo.Id);
            Assert.Equal("Test API Key", result.KeyInfo.KeyName);

            // Verify event was published
            _mockPublishEndpoint.Verify(x => x.Publish(
                It.IsAny<VirtualKeyCreated>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion
    }
}
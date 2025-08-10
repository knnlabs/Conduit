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

namespace ConduitLLM.Tests.Admin.Services
{
    public partial class AdminVirtualKeyServiceTests
    {
        #region PerformMaintenanceAsync Tests

        [Fact]
        public async Task PerformMaintenanceAsync_ProcessesExpiredKeys()
        {
            // Arrange
            var keys = new List<VirtualKey>
            {
                // Expired key that should be disabled
                new VirtualKey
                {
                    Id = 1,
                    KeyName = "Expired Key",
                    IsEnabled = true,
                    ExpiresAt = DateTime.UtcNow.AddDays(-1),
                    VirtualKeyGroupId = 1
                },
                // Valid key that shouldn't change
                new VirtualKey
                {
                    Id = 2,
                    KeyName = "Valid Key",
                    IsEnabled = true,
                    ExpiresAt = DateTime.UtcNow.AddDays(30),
                    VirtualKeyGroupId = 1
                }
            };

            _mockVirtualKeyRepository.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(keys);

            _mockVirtualKeyRepository.Setup(x => x.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            await _service.PerformMaintenanceAsync();

            // Assert
            // Verify expired key was disabled
            Assert.False(keys[0].IsEnabled);
            
            // Only the expired key should be updated
            _mockVirtualKeyRepository.Verify(x => x.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion
    }
}
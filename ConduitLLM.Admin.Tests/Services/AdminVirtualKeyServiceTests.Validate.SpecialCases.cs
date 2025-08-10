using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Entities;
using Moq;
using Xunit;

namespace ConduitLLM.Admin.Tests.Services
{
    public partial class AdminVirtualKeyServiceTests
    {
        #region Special Cases Tests

        [Fact]
        public async Task ValidateVirtualKeyAsync_ValidKeyNoExpiration_ReturnsValid()
        {
            // Arrange
            var key = VirtualKeyConstants.KeyPrefix + "testkey123";
            var virtualKey = new VirtualKey
            {
                Id = 1,
                KeyName = "Test Key",
                KeyHash = "hash123",
                IsEnabled = true,
                ExpiresAt = null, // No expiration
                VirtualKeyGroupId = 1,
                CreatedAt = DateTime.UtcNow.AddYears(-1), // Old key but no expiration
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);
            
            var group = new VirtualKeyGroup { Id = 1, Balance = 1000m };
            _mockGroupRepository.Setup(x => x.GetByKeyIdAsync(1))
                .ReturnsAsync(group);

            // Act
            var result = await _service.ValidateVirtualKeyAsync(key);

            // Assert
            Assert.True(result.IsValid);
            Assert.Null(result.ErrorMessage);
            Assert.Equal(1, result.VirtualKeyId);
        }

        #endregion
    }
}
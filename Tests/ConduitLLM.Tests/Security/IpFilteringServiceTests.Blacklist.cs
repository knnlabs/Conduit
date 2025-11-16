using FluentAssertions;

namespace ConduitLLM.Tests.Security
{
    public partial class IpFilteringServiceTests
    {
        #region Blacklist Management Tests

        [Fact]
        public async Task AddToBlacklist_WithValidIp_ShouldBlockIp()
        {
            // Arrange
            var service = new MockIpFilteringService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);
            var maliciousIp = "5.6.7.8";

            // Act
            var result = await service.AddToBlacklistAsync(maliciousIp, "Known attacker");

            // Assert
            result.Should().BeTrue();
            (await service.IsAllowedAsync(maliciousIp)).Should().BeFalse();
        }

        [Fact]
        public async Task RemoveFromBlacklist_WithExistingIp_ShouldUnblockIp()
        {
            // Arrange
            var service = new MockIpFilteringService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);
            var ip = "5.6.7.8";
            await service.AddToBlacklistAsync(ip, "Test");

            // Act
            var result = await service.RemoveFromBlacklistAsync(ip);

            // Assert
            result.Should().BeTrue();
            (await service.IsAllowedAsync(ip)).Should().BeTrue(); // Default allow
        }

        [Fact]
        public async Task GetBlacklist_ShouldReturnAllBlacklistedEntries()
        {
            // Arrange
            var service = new MockIpFilteringService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);

            // Act
            var blacklist = await service.GetBlacklistAsync();

            // Assert
            blacklist.Should().HaveCountGreaterThanOrEqualTo(2);
            blacklist.Should().Contain(entry => entry.IpAddress == "203.0.113.0/24");
            blacklist.Should().Contain(entry => entry.IpAddress == "198.51.100.14");
        }

        #endregion
    }
}
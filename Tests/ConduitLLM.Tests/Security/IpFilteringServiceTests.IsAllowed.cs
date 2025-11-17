using FluentAssertions;

namespace ConduitLLM.Tests.Security
{
    public partial class IpFilteringServiceTests
    {
        #region IsAllowed Tests

        [Theory]
        [InlineData("192.168.1.100", true)] // In whitelist range
        [InlineData("192.168.1.255", true)] // In whitelist range
        [InlineData("10.0.0.1", true)]      // Exact match in whitelist
        [InlineData("172.16.50.50", true)]  // In whitelist range
        [InlineData("8.8.8.8", true)]       // Not in any list, default allow
        public async Task IsAllowed_WithWhitelistAndDefaultAllow_ShouldReturnExpectedResult(string ipAddress, bool expected)
        {
            // Arrange
            var service = new MockIpFilteringService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);

            // Act
            var result = await service.IsAllowedAsync(ipAddress);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("203.0.113.50", false)]  // In blacklist range
        [InlineData("198.51.100.14", false)] // Exact match in blacklist
        [InlineData("192.168.1.100", true)]  // In whitelist, not in blacklist
        public async Task IsAllowed_WithBlacklist_ShouldBlockBlacklistedIps(string ipAddress, bool expected)
        {
            // Arrange
            var service = new MockIpFilteringService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);

            // Act
            var result = await service.IsAllowedAsync(ipAddress);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public async Task IsAllowed_WithDefaultDeny_ShouldOnlyAllowWhitelisted()
        {
            // Arrange
            _mockConfiguration.Setup(x => x["Security:DefaultAction"]).Returns("Deny");
            var service = new MockIpFilteringService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);

            // Act & Assert
            (await service.IsAllowedAsync("192.168.1.100")).Should().BeTrue();  // In whitelist
            (await service.IsAllowedAsync("8.8.8.8")).Should().BeFalse();       // Not in whitelist
        }

        [Fact]
        public async Task IsAllowed_WithDisabledFiltering_ShouldAlwaysAllow()
        {
            // Arrange
            _mockConfiguration.Setup(x => x["Security:EnableIpFiltering"]).Returns("false");
            var service = new MockIpFilteringService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);

            // Act & Assert
            (await service.IsAllowedAsync("203.0.113.50")).Should().BeTrue();  // Even blacklisted
            (await service.IsAllowedAsync("1.2.3.4")).Should().BeTrue();       // Any IP
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task IsAllowed_WithInvalidIp_ShouldThrowArgumentException(string invalidIp)
        {
            // Arrange
            var service = new MockIpFilteringService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => service.IsAllowedAsync(invalidIp));
        }

        [Fact]
        public async Task IsAllowed_WithMalformedIp_ShouldReturnFalse()
        {
            // Arrange
            var service = new MockIpFilteringService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);

            // Act
            var result = await service.IsAllowedAsync("not.an.ip.address");

            // Assert
            result.Should().BeFalse();
        }

        #endregion
    }
}
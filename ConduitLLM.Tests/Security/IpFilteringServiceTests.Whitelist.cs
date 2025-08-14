using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace ConduitLLM.Tests.Security
{
    public partial class IpFilteringServiceTests
    {
        #region Whitelist Management Tests

        [Fact]
        public async Task AddToWhitelist_WithValidIp_ShouldAddSuccessfully()
        {
            // Arrange
            var service = new MockIpFilteringService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);
            var newIp = "1.2.3.4";

            // Act
            var result = await service.AddToWhitelistAsync(newIp);

            // Assert
            result.Should().BeTrue();
            (await service.IsInWhitelistAsync(newIp)).Should().BeTrue();
        }

        [Fact]
        public async Task AddToWhitelist_WithCidrRange_ShouldAddSuccessfully()
        {
            // Arrange
            var service = new MockIpFilteringService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);
            var cidr = "10.10.0.0/16";

            // Act
            var result = await service.AddToWhitelistAsync(cidr);

            // Assert
            result.Should().BeTrue();
            (await service.IsInWhitelistAsync("10.10.1.1")).Should().BeTrue();
            (await service.IsInWhitelistAsync("10.10.255.255")).Should().BeTrue();
        }

        [Fact]
        public async Task RemoveFromWhitelist_WithExistingIp_ShouldRemoveSuccessfully()
        {
            // Arrange
            var service = new MockIpFilteringService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);
            var ip = "1.2.3.4";
            await service.AddToWhitelistAsync(ip);

            // Act
            var result = await service.RemoveFromWhitelistAsync(ip);

            // Assert
            result.Should().BeTrue();
            (await service.IsInWhitelistAsync(ip)).Should().BeFalse();
        }

        [Fact]
        public async Task GetWhitelist_ShouldReturnAllWhitelistedEntries()
        {
            // Arrange
            var service = new MockIpFilteringService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);

            // Act
            var whitelist = await service.GetWhitelistAsync();

            // Assert
            whitelist.Should().Contain("192.168.1.0/24");
            whitelist.Should().Contain("10.0.0.1");
            whitelist.Should().Contain("172.16.0.0/16");
        }

        #endregion
    }
}
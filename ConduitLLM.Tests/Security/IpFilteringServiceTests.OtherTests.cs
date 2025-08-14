using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Security
{
    public partial class IpFilteringServiceTests
    {
        #region CIDR Range Tests

        [Theory]
        [InlineData("10.0.0.0/8", "10.255.255.255", true)]
        [InlineData("10.0.0.0/8", "11.0.0.0", false)]
        [InlineData("192.168.0.0/16", "192.168.100.50", true)]
        [InlineData("192.168.0.0/16", "192.169.0.0", false)]
        [InlineData("172.16.10.0/24", "172.16.10.100", true)]
        [InlineData("172.16.10.0/24", "172.16.11.1", false)]
        public async Task IsInRange_WithCidrNotation_ShouldCalculateCorrectly(string cidr, string testIp, bool expected)
        {
            // Arrange - Create a clean configuration without overlapping ranges
            var cleanConfig = new Mock<IConfiguration>();
            var emptySection = new Mock<IConfigurationSection>();
            emptySection.Setup(x => x.GetChildren()).Returns(new List<IConfigurationSection>());
            cleanConfig.Setup(x => x.GetSection("Security:IpWhitelist")).Returns(emptySection.Object);
            cleanConfig.Setup(x => x.GetSection("Security:IpBlacklist")).Returns(emptySection.Object);
            cleanConfig.Setup(x => x["Security:EnableIpFiltering"]).Returns("true");
            cleanConfig.Setup(x => x["Security:DefaultAction"]).Returns("Deny");
            
            var service = new MockIpFilteringService(cleanConfig.Object, _mockCache.Object, _mockLogger.Object);
            await service.AddToWhitelistAsync(cidr);

            // Act
            var result = await service.IsInWhitelistAsync(testIp);

            // Assert
            result.Should().Be(expected);
        }

        #endregion

        #region Caching Tests

        [Fact]
        public async Task IsAllowed_ShouldCacheResults()
        {
            // Arrange
            var cacheEntry = new Mock<ICacheEntry>();
            object cachedValue = true;
            
            _mockCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out cachedValue))
                .Returns(true);

            var service = new MockIpFilteringService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);

            // Act
            var result1 = await service.IsAllowedAsync("1.2.3.4");
            var result2 = await service.IsAllowedAsync("1.2.3.4");

            // Assert
            result1.Should().BeTrue();
            result2.Should().BeTrue();
            
            // Verify cache was checked
            _mockCache.Verify(x => x.TryGetValue(It.IsAny<object>(), out It.Ref<object>.IsAny), Times.AtLeast(2));
        }

        #endregion

        #region Temporary Block Tests

        [Fact]
        public async Task BlockTemporarily_ShouldBlockForSpecifiedDuration()
        {
            // Arrange
            var service = new MockIpFilteringService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);
            var ip = "1.2.3.4";
            var duration = TimeSpan.FromMinutes(30);

            // Act
            await service.BlockTemporarilyAsync(ip, duration, "Rate limit exceeded");

            // Assert
            (await service.IsAllowedAsync(ip)).Should().BeFalse();
            
            // Check that it's a temporary block
            var blacklist = await service.GetBlacklistAsync();
            blacklist.Should().Contain(b => b.IpAddress == ip && b.ExpiresAt.HasValue);
        }

        #endregion

        #region Statistics Tests

        [Fact]
        public async Task GetStatistics_ShouldReturnFilteringStats()
        {
            // Arrange
            var service = new MockIpFilteringService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);
            
            // Perform some operations
            await service.IsAllowedAsync("192.168.1.100"); // Allowed
            await service.IsAllowedAsync("203.0.113.50");  // Blocked
            await service.IsAllowedAsync("8.8.8.8");       // Allowed

            // Act
            var stats = await service.GetStatisticsAsync();

            // Assert
            stats.TotalRequests.Should().Be(3);
            stats.AllowedRequests.Should().Be(2);
            stats.BlockedRequests.Should().Be(1);
            stats.WhitelistSize.Should().BeGreaterThanOrEqualTo(3);
            stats.BlacklistSize.Should().BeGreaterThanOrEqualTo(2);
        }

        #endregion
    }
}
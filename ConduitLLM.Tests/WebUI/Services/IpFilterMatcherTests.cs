using System;
using System.Collections.Generic;
using System.Linq;

using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.Services;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.WebUI.Services
{
    public class IpFilterMatcherTests
    {
        private readonly Mock<ILogger<IpFilterMatcher>> _mockLogger;
        private readonly IpFilterMatcher _matcher;

        public IpFilterMatcherTests()
        {
            _mockLogger = new Mock<ILogger<IpFilterMatcher>>();
            _matcher = new IpFilterMatcher(_mockLogger.Object);
        }

        [Fact]
        public void IsIpAllowed_WithNoFilters_ReturnsDefaultAllowValue()
        {
            // Arrange
            var ipAddress = "192.168.1.1";
            var filters = new List<IpFilterEntity>();

            // Act & Assert
            Assert.True(_matcher.IsIpAllowed(ipAddress, filters, defaultAllow: true));
            Assert.False(_matcher.IsIpAllowed(ipAddress, filters, defaultAllow: false));
        }

        [Fact]
        public void IsIpAllowed_WithNoEnabledFilters_ReturnsDefaultAllowValue()
        {
            // Arrange
            var ipAddress = "192.168.1.1";
            var filters = new List<IpFilterEntity>
            {
                new IpFilterEntity
                {
                    FilterType = IpFilterConstants.BLACKLIST,
                    IpAddressOrCidr = "192.168.1.1",
                    IsEnabled = false
                }
            };

            // Act & Assert
            Assert.True(_matcher.IsIpAllowed(ipAddress, filters, defaultAllow: true));
            Assert.False(_matcher.IsIpAllowed(ipAddress, filters, defaultAllow: false));
        }

        [Fact]
        public void IsIpAllowed_WithInvalidIpAddress_ReturnsDefaultAllowValue()
        {
            // Arrange
            var ipAddress = "invalid-ip";
            var filters = new List<IpFilterEntity>
            {
                new IpFilterEntity
                {
                    FilterType = IpFilterConstants.BLACKLIST,
                    IpAddressOrCidr = "192.168.1.1",
                    IsEnabled = true
                }
            };

            // Act & Assert
            Assert.True(_matcher.IsIpAllowed(ipAddress, filters, defaultAllow: true));
            Assert.False(_matcher.IsIpAllowed(ipAddress, filters, defaultAllow: false));
        }

        [Fact]
        public void IsIpAllowed_WithMatchingWhitelist_ReturnsTrue()
        {
            // Arrange
            var ipAddress = "192.168.1.1";
            var filters = new List<IpFilterEntity>
            {
                new IpFilterEntity
                {
                    FilterType = IpFilterConstants.WHITELIST,
                    IpAddressOrCidr = "192.168.1.1",
                    IsEnabled = true
                }
            };

            // Act
            var result = _matcher.IsIpAllowed(ipAddress, filters, defaultAllow: false);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsIpAllowed_WithNonMatchingWhitelist_ReturnsFalse()
        {
            // Arrange
            var ipAddress = "192.168.1.1";
            var filters = new List<IpFilterEntity>
            {
                new IpFilterEntity
                {
                    FilterType = IpFilterConstants.WHITELIST,
                    IpAddressOrCidr = "192.168.1.2",
                    IsEnabled = true
                }
            };

            // Act
            var result = _matcher.IsIpAllowed(ipAddress, filters, defaultAllow: true);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsIpAllowed_WithMatchingBlacklist_ReturnsFalse()
        {
            // Arrange
            var ipAddress = "192.168.1.1";
            var filters = new List<IpFilterEntity>
            {
                new IpFilterEntity
                {
                    FilterType = IpFilterConstants.BLACKLIST,
                    IpAddressOrCidr = "192.168.1.1",
                    IsEnabled = true
                }
            };

            // Act
            var result = _matcher.IsIpAllowed(ipAddress, filters, defaultAllow: true);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsIpAllowed_WithNonMatchingBlacklist_ReturnsDefaultAllow()
        {
            // Arrange
            var ipAddress = "192.168.1.1";
            var filters = new List<IpFilterEntity>
            {
                new IpFilterEntity
                {
                    FilterType = IpFilterConstants.BLACKLIST,
                    IpAddressOrCidr = "192.168.1.2",
                    IsEnabled = true
                }
            };

            // Act & Assert
            Assert.True(_matcher.IsIpAllowed(ipAddress, filters, defaultAllow: true));
            Assert.False(_matcher.IsIpAllowed(ipAddress, filters, defaultAllow: false));
        }

        [Fact]
        public void IsIpAllowed_WithMatchingWhitelistAndBlacklist_ReturnsTrue()
        {
            // Arrange
            var ipAddress = "192.168.1.1";
            var filters = new List<IpFilterEntity>
            {
                new IpFilterEntity
                {
                    FilterType = IpFilterConstants.WHITELIST,
                    IpAddressOrCidr = "192.168.1.1",
                    IsEnabled = true
                },
                new IpFilterEntity
                {
                    FilterType = IpFilterConstants.BLACKLIST,
                    IpAddressOrCidr = "192.168.1.1",
                    IsEnabled = true
                }
            };

            // Act
            var result = _matcher.IsIpAllowed(ipAddress, filters, defaultAllow: false);

            // Assert - Whitelist should take precedence
            Assert.True(result);
        }

        [Fact]
        public void IsIpAllowed_WithCidrRangeMatch_ReturnsExpectedResult()
        {
            // Arrange
            var ipAddress = "192.168.1.100";
            var filters = new List<IpFilterEntity>
            {
                new IpFilterEntity
                {
                    FilterType = IpFilterConstants.BLACKLIST,
                    IpAddressOrCidr = "192.168.1.0/24",
                    IsEnabled = true
                }
            };

            // Act
            var result = _matcher.IsIpAllowed(ipAddress, filters, defaultAllow: true);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsIpAllowed_WithMixedIPv4AndIPv6_HandlesCorrectly()
        {
            // Arrange
            var ipv4Address = "192.168.1.1";
            var ipv6Address = "2001:db8::1";

            var filters = new List<IpFilterEntity>
            {
                new IpFilterEntity
                {
                    FilterType = IpFilterConstants.BLACKLIST,
                    IpAddressOrCidr = "2001:db8::/32",
                    IsEnabled = true
                }
            };

            // Act
            var ipv4Result = _matcher.IsIpAllowed(ipv4Address, filters, defaultAllow: true);
            var ipv6Result = _matcher.IsIpAllowed(ipv6Address, filters, defaultAllow: true);

            // Assert
            Assert.True(ipv4Result); // IPv4 address is not in IPv6 blacklist
            Assert.False(ipv6Result); // IPv6 address is in IPv6 blacklist
        }
    }
}

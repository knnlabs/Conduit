using ConduitLLM.Core.Utilities;
using FluentAssertions;

namespace ConduitLLM.Tests.Core.Utilities
{
    /// <summary>
    /// Unit tests for IpAddressHelper utility class
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Security")]
    public class IpAddressHelperTests
    {
        #region CIDR Matching Tests - IPv4

        [Theory]
        [InlineData("192.168.1.100", "192.168.1.0/24", true)]
        [InlineData("192.168.1.1", "192.168.1.0/24", true)]
        [InlineData("192.168.1.255", "192.168.1.0/24", true)]
        [InlineData("192.168.2.1", "192.168.1.0/24", false)]
        [InlineData("10.0.0.50", "10.0.0.0/8", true)]
        [InlineData("10.255.255.255", "10.0.0.0/8", true)]
        [InlineData("11.0.0.1", "10.0.0.0/8", false)]
        [InlineData("172.16.50.50", "172.16.0.0/12", true)]
        [InlineData("172.31.255.255", "172.16.0.0/12", true)]
        [InlineData("172.32.0.1", "172.16.0.0/12", false)]
        public void IsIpInCidrRange_IPv4_ShouldReturnExpectedResult(string ipAddress, string cidrRange, bool expected)
        {
            // Act
            var result = IpAddressHelper.IsIpInCidrRange(ipAddress, cidrRange);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("192.168.1.100", "192.168.1.100", true)]
        [InlineData("192.168.1.100", "192.168.1.101", false)]
        [InlineData("192.168.1.100", "192.168.1.0/24", true)]
        public void IsIpInRange_IPv4_ShouldReturnExpectedResult(string ipAddress, string rule, bool expected)
        {
            // Act
            var result = IpAddressHelper.IsIpInRange(ipAddress, rule);

            // Assert
            result.Should().Be(expected);
        }

        #endregion

        #region CIDR Matching Tests - IPv6

        [Theory]
        [InlineData("2001:db8::1", "2001:db8::/32", true)]
        [InlineData("2001:db8:ffff:ffff:ffff:ffff:ffff:ffff", "2001:db8::/32", true)]
        [InlineData("2001:db9::1", "2001:db8::/32", false)]
        [InlineData("fe80::1", "fe80::/10", true)]
        [InlineData("fe80:ffff:ffff:ffff:ffff:ffff:ffff:ffff", "fe80::/10", true)]
        public void IsIpInCidrRange_IPv6_ShouldReturnExpectedResult(string ipAddress, string cidrRange, bool expected)
        {
            // Act
            var result = IpAddressHelper.IsIpInCidrRange(ipAddress, cidrRange);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("2001:db8::1", "2001:db8::1", true)]
        [InlineData("2001:db8::1", "2001:db8::2", false)]
        [InlineData("2001:db8::1", "2001:db8::/32", true)]
        public void IsIpInRange_IPv6_ShouldReturnExpectedResult(string ipAddress, string rule, bool expected)
        {
            // Act
            var result = IpAddressHelper.IsIpInRange(ipAddress, rule);

            // Assert
            result.Should().Be(expected);
        }

        #endregion

        #region Validation Tests

        [Theory]
        [InlineData("192.168.1.1", true)]
        [InlineData("10.0.0.1", true)]
        [InlineData("255.255.255.255", true)]
        [InlineData("0.0.0.0", true)]
        [InlineData("192.168.1.0/24", true)]
        [InlineData("10.0.0.0/8", true)]
        [InlineData("192.168.1.0/32", true)]
        [InlineData("192.168.1.0/0", true)]
        [InlineData("2001:db8::1", true)]
        [InlineData("2001:db8::/32", true)]
        [InlineData("::1", true)]
        [InlineData("::", true)]
        [InlineData("fe80::1", true)]
        [InlineData("invalid", false)]
        [InlineData("", false)]
        [InlineData("192.168.1.1/33", false)] // Invalid IPv4 prefix
        [InlineData("2001:db8::1/129", false)] // Invalid IPv6 prefix
        // Note: "192.168.1" is parsed as "192.168.0.1" by .NET's IPAddress.TryParse
        [InlineData("192.168.1.256", false)] // Invalid octet
        public void IsValidIpAddressOrCidr_ShouldReturnExpectedResult(string ipAddressOrCidr, bool expected)
        {
            // Act
            var result = IpAddressHelper.IsValidIpAddressOrCidr(ipAddressOrCidr);

            // Assert
            result.Should().Be(expected);
        }

        #endregion

        #region Private IP Tests

        [Theory]
        [InlineData("10.0.0.1", true)]
        [InlineData("10.255.255.255", true)]
        [InlineData("172.16.0.1", true)]
        [InlineData("172.31.255.255", true)]
        [InlineData("172.15.0.1", false)] // Not in 172.16.0.0/12
        [InlineData("172.32.0.1", false)] // Not in 172.16.0.0/12
        [InlineData("192.168.0.1", true)]
        [InlineData("192.168.255.255", true)]
        [InlineData("169.254.0.1", true)] // Link-local
        [InlineData("127.0.0.1", true)] // Loopback
        [InlineData("127.255.255.255", true)] // Loopback range
        [InlineData("8.8.8.8", false)] // Google DNS - public
        [InlineData("1.1.1.1", false)] // Cloudflare DNS - public
        public void IsPrivateIp_IPv4_ShouldReturnExpectedResult(string ipAddress, bool expected)
        {
            // Act
            var result = IpAddressHelper.IsPrivateIp(ipAddress);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("::1", true)] // IPv6 loopback
        [InlineData("fe80::1", true)] // IPv6 link-local
        [InlineData("fc00::1", true)] // Unique local address
        [InlineData("fd00::1", true)] // Unique local address
        [InlineData("2001:db8::1", false)] // Documentation range - not private
        public void IsPrivateIp_IPv6_ShouldReturnExpectedResult(string ipAddress, bool expected)
        {
            // Act
            var result = IpAddressHelper.IsPrivateIp(ipAddress);

            // Assert
            result.Should().Be(expected);
        }

        #endregion

        #region IP Version Detection Tests

        [Theory]
        [InlineData("192.168.1.1", true)]
        [InlineData("10.0.0.1", true)]
        [InlineData("2001:db8::1", false)]
        [InlineData("::1", false)]
        [InlineData("invalid", false)]
        public void IsIpv4_ShouldReturnExpectedResult(string ipAddress, bool expected)
        {
            // Act
            var result = IpAddressHelper.IsIpv4(ipAddress);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("2001:db8::1", true)]
        [InlineData("::1", true)]
        [InlineData("fe80::1", true)]
        [InlineData("192.168.1.1", false)]
        [InlineData("10.0.0.1", false)]
        [InlineData("invalid", false)]
        public void IsIpv6_ShouldReturnExpectedResult(string ipAddress, bool expected)
        {
            // Act
            var result = IpAddressHelper.IsIpv6(ipAddress);

            // Assert
            result.Should().Be(expected);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void IsIpInRange_WithNullInput_ShouldReturnFalse()
        {
            // Act & Assert
            IpAddressHelper.IsIpInRange(null!, "192.168.1.0/24").Should().BeFalse();
            IpAddressHelper.IsIpInRange("192.168.1.1", null!).Should().BeFalse();
            IpAddressHelper.IsIpInRange(null!, null!).Should().BeFalse();
        }

        [Fact]
        public void IsIpInRange_WithEmptyInput_ShouldReturnFalse()
        {
            // Act & Assert
            IpAddressHelper.IsIpInRange("", "192.168.1.0/24").Should().BeFalse();
            IpAddressHelper.IsIpInRange("192.168.1.1", "").Should().BeFalse();
            IpAddressHelper.IsIpInRange("   ", "192.168.1.0/24").Should().BeFalse();
        }

        [Fact]
        public void IsIpInCidrRange_WithMixedAddressFamilies_ShouldReturnFalse()
        {
            // IPv4 address with IPv6 CIDR
            IpAddressHelper.IsIpInCidrRange("192.168.1.1", "2001:db8::/32").Should().BeFalse();

            // IPv6 address with IPv4 CIDR
            IpAddressHelper.IsIpInCidrRange("2001:db8::1", "192.168.1.0/24").Should().BeFalse();
        }

        [Fact]
        public void IsIpInCidrRange_WithInvalidCidr_ShouldReturnFalse()
        {
            // Missing prefix
            IpAddressHelper.IsIpInCidrRange("192.168.1.1", "192.168.1.0/").Should().BeFalse();

            // Non-numeric prefix
            IpAddressHelper.IsIpInCidrRange("192.168.1.1", "192.168.1.0/abc").Should().BeFalse();

            // Multiple slashes
            IpAddressHelper.IsIpInCidrRange("192.168.1.1", "192.168.1.0/24/8").Should().BeFalse();
        }

        [Fact]
        public void IsValidIpAddressOrCidr_WithNullOrWhitespace_ShouldReturnFalse()
        {
            IpAddressHelper.IsValidIpAddressOrCidr(null!).Should().BeFalse();
            IpAddressHelper.IsValidIpAddressOrCidr("").Should().BeFalse();
            IpAddressHelper.IsValidIpAddressOrCidr("   ").Should().BeFalse();
        }

        #endregion
    }
}

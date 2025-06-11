using System.Collections.Generic;

using ConduitLLM.WebUI.Services;

using Xunit;

namespace ConduitLLM.Tests.WebUI.Services
{
    public class IpAddressValidatorTests
    {
        [Theory]
        [InlineData("192.168.1.1", true)]
        [InlineData("10.0.0.1", true)]
        [InlineData("172.16.0.1", true)]
        [InlineData("255.255.255.255", true)]
        [InlineData("0.0.0.0", true)]
        [InlineData("2001:db8::1", true)]
        [InlineData("::1", true)]
        [InlineData("fe80::1234:5678:abcd:ef12", true)]
        [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334", true)]
        [InlineData("192.168.1", false)]
        [InlineData("192.168.1.256", false)]
        [InlineData("192.168.1.1.1", false)]
        [InlineData("192.168.1.a", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        [InlineData("not-an-ip", false)]
        [InlineData("2001:db8::xxxx", false)]
        public void IsValidIpAddress_WithVariousInputs_ReturnsExpectedResult(string? ipAddress, bool expected)
        {
            // Act
            var result = IpAddressValidator.IsValidIpAddress(ipAddress);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("192.168.1.0/24", true)]
        [InlineData("10.0.0.0/8", true)]
        [InlineData("172.16.0.0/12", true)]
        [InlineData("192.168.1.128/25", true)]
        [InlineData("0.0.0.0/0", true)]
        [InlineData("2001:db8::/32", true)]
        [InlineData("2001:db8::/128", true)]
        [InlineData("2001:0db8:85a3::/48", true)]
        [InlineData("fe80::/10", true)]
        [InlineData("192.168.1.0", false)]
        [InlineData("192.168.1.0/", false)]
        [InlineData("192.168.1.0/33", false)]
        [InlineData("192.168.1.0/-1", false)]
        [InlineData("192.168.1/24", false)]
        [InlineData("2001:db8::/129", false)]
        [InlineData("2001:db8::/abc", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        [InlineData("not-a-cidr", false)]
        public void IsValidCidr_WithVariousInputs_ReturnsExpectedResult(string? cidr, bool expected)
        {
            // Act
            var result = IpAddressValidator.IsValidCidr(cidr);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("192.168.1.10", "192.168.1.0/24", true)]
        [InlineData("192.168.2.10", "192.168.1.0/24", false)]
        [InlineData("10.10.10.10", "10.0.0.0/8", true)]
        [InlineData("11.10.10.10", "10.0.0.0/8", false)]
        [InlineData("192.168.1.128", "192.168.1.128/25", true)]
        [InlineData("192.168.1.127", "192.168.1.128/25", false)]
        [InlineData("0.0.0.1", "0.0.0.0/0", true)]
        [InlineData("255.255.255.255", "0.0.0.0/0", true)]
        [InlineData("2001:db8::1", "2001:db8::/32", true)]
        [InlineData("2001:db9::1", "2001:db8::/32", false)]
        [InlineData("fe80::1", "fe80::/10", true)]
        [InlineData("fe90::1", "fe80::/10", false)]
        [InlineData("192.168.1.1", "2001:db8::/32", false)] // IPv4 vs IPv6
        [InlineData("2001:db8::1", "192.168.1.0/24", false)] // IPv6 vs IPv4
        public void IsIpInCidrRange_WithVariousInputs_ReturnsExpectedResult(string ipAddress, string cidr, bool expected)
        {
            // Act
            var result = IpAddressValidator.IsIpInCidrRange(ipAddress, cidr);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("192.168.1.1", "192.168.1.1")]
        [InlineData("10.0.0.1", "10.0.0.1")]
        [InlineData("2001:db8::1", "2001:db8::1")]
        [InlineData("2001:DB8::1", "2001:db8::1")] // Case normalization
        [InlineData("2001:0db8:0000:0000:0000:0000:0000:0001", "2001:db8::1")] // IPv6 compression
        [InlineData("192.168.1.0/24", "192.168.1.0/24")]
        [InlineData("2001:db8::/32", "2001:db8::/32")]
        [InlineData("invalid-ip", "invalid-ip")] // Invalid should return as-is
        [InlineData("", "")]
        [InlineData(null, null)]
        public void StandardizeIpAddressOrCidr_WithVariousInputs_ReturnsExpectedResult(string? input, string? expected)
        {
            // Act
            var result = IpAddressValidator.StandardizeIpAddressOrCidr(input);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}

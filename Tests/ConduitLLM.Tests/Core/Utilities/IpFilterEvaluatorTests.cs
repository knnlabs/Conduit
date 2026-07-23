using System.Collections.Generic;
using System.Linq;

using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Utilities;

using FluentAssertions;

namespace ConduitLLM.Tests.Core.Utilities
{
    /// <summary>
    /// Unit tests for the shared IP filter precedence model used by both the Gateway data plane
    /// and the Admin control plane.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Security")]
    public class IpFilterEvaluatorTests
    {
        private static IpFilterEntity Rule(string cidr, string type) =>
            new() { IpAddressOrCidr = cidr, FilterType = type, IsEnabled = true };

        private static List<IpFilterEntity> White(params string[] cidrs) =>
            cidrs.Select(c => Rule(c, IpFilterConstants.WHITELIST)).ToList();

        private static List<IpFilterEntity> Black(params string[] cidrs) =>
            cidrs.Select(c => Rule(c, IpFilterConstants.BLACKLIST)).ToList();

        [Fact]
        public void Evaluate_BlacklistMatch_Denies()
        {
            var decision = IpFilterEvaluator.Evaluate("10.0.0.5", White(), Black("10.0.0.0/8"), defaultAllow: true);
            decision.IsAllowed.Should().BeFalse();
        }

        [Fact]
        public void Evaluate_BlacklistTakesPrecedenceOverWhitelist()
        {
            var decision = IpFilterEvaluator.Evaluate("10.0.0.5", White("10.0.0.0/8"), Black("10.0.0.5"), defaultAllow: true);
            decision.IsAllowed.Should().BeFalse();
        }

        [Fact]
        public void Evaluate_WhitelistActive_Match_Allows()
        {
            var decision = IpFilterEvaluator.Evaluate("192.168.1.10", White("192.168.1.0/24"), Black(), defaultAllow: false);
            decision.IsAllowed.Should().BeTrue();
        }

        [Fact]
        public void Evaluate_WhitelistActive_NoMatch_Denies()
        {
            // The whitelist "footgun": any active allow rule makes filtering restrictive, denying every
            // IP not explicitly listed — even with defaultAllow = true.
            var decision = IpFilterEvaluator.Evaluate("8.8.8.8", White("192.168.1.0/24"), Black(), defaultAllow: true);
            decision.IsAllowed.Should().BeFalse();
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void Evaluate_NoRules_UsesDefaultAllow(bool defaultAllow, bool expected)
        {
            var decision = IpFilterEvaluator.Evaluate("8.8.8.8", White(), Black(), defaultAllow);
            decision.IsAllowed.Should().Be(expected);
        }

        [Fact]
        public void Evaluate_IPv6CidrBlacklist_Denies()
        {
            var decision = IpFilterEvaluator.Evaluate("2001:db8::1", White(), Black("2001:db8::/32"), defaultAllow: true);
            decision.IsAllowed.Should().BeFalse();
        }

        [Fact]
        public void Evaluate_IPv4MappedAddress_Ipv4CidrBlacklistDenies()
        {
            var decision = IpFilterEvaluator.Evaluate(
                "::ffff:203.0.113.9",
                White(),
                Black("203.0.113.0/24"),
                defaultAllow: true);

            decision.IsAllowed.Should().BeFalse();
        }

        [Fact]
        public void Evaluate_IPv4MappedAddress_Ipv4WhitelistAllows()
        {
            var decision = IpFilterEvaluator.Evaluate(
                "::ffff:203.0.113.9",
                White("203.0.113.9"),
                Black(),
                defaultAllow: false);

            decision.IsAllowed.Should().BeTrue();
        }
    }
}

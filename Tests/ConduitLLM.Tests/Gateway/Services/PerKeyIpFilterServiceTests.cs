using System.Collections.Generic;

using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Gateway.Interfaces;

using FluentAssertions;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace ConduitLLM.Tests.Gateway.Services
{
    /// <summary>
    /// Unit tests for per-virtual-key IP filtering in <see cref="IpFilterService"/>.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Security")]
    public class PerKeyIpFilterServiceTests
    {
        private static IpFilterEntity Rule(int vkId, string cidr, string type) =>
            new() { Id = vkId * 100, VirtualKeyId = vkId, IpAddressOrCidr = cidr, FilterType = type, IsEnabled = true };

        private static IpFilterService BuildService(params IpFilterEntity[] perKeyFilters)
        {
            var repo = new Mock<IIpFilterRepository>();
            repo.Setup(r => r.GetEnabledPerKeyAsync(default))
                .ReturnsAsync(perKeyFilters);

            var globalSettings = new Mock<IGlobalSettingsCacheService>();
            var cache = new MemoryCache(new MemoryCacheOptions());

            return new IpFilterService(
                repo.Object,
                globalSettings.Object,
                cache,
                NullLogger<IpFilterService>.Instance);
        }

        [Fact]
        public async Task IsIpAllowedForVirtualKeyAsync_KeyWithNoRules_Allows()
        {
            var service = BuildService(Rule(1, "192.168.1.0/24", IpFilterConstants.WHITELIST));

            // Key 2 has no per-key rules → unrestricted.
            var allowed = await service.IsIpAllowedForVirtualKeyAsync("8.8.8.8", virtualKeyId: 2);

            allowed.Should().BeTrue();
        }

        [Fact]
        public async Task IsIpAllowedForVirtualKeyAsync_KeyWhitelist_Match_Allows()
        {
            var service = BuildService(Rule(1, "192.168.1.0/24", IpFilterConstants.WHITELIST));

            var allowed = await service.IsIpAllowedForVirtualKeyAsync("192.168.1.5", virtualKeyId: 1);

            allowed.Should().BeTrue();
        }

        [Fact]
        public async Task IsIpAllowedForVirtualKeyAsync_KeyWhitelist_NoMatch_Denies()
        {
            var service = BuildService(Rule(1, "192.168.1.0/24", IpFilterConstants.WHITELIST));

            // The key's whitelist is restrictive: any IP not listed is denied for THIS key.
            var allowed = await service.IsIpAllowedForVirtualKeyAsync("8.8.8.8", virtualKeyId: 1);

            allowed.Should().BeFalse();
        }

        [Fact]
        public async Task IsIpAllowedForVirtualKeyAsync_KeyBlacklist_DeniesListed_AllowsOthers()
        {
            var service = BuildService(Rule(1, "10.0.0.0/8", IpFilterConstants.BLACKLIST));

            (await service.IsIpAllowedForVirtualKeyAsync("10.0.0.5", virtualKeyId: 1)).Should().BeFalse();
            (await service.IsIpAllowedForVirtualKeyAsync("8.8.8.8", virtualKeyId: 1)).Should().BeTrue();
        }

        [Fact]
        public async Task IsIpAllowedForVirtualKeyAsync_OneKeysRulesDoNotAffectAnother()
        {
            // Key 1 is restricted to 192.168.1.0/24; key 2 has its own blacklist. They must not cross-apply.
            var service = BuildService(
                Rule(1, "192.168.1.0/24", IpFilterConstants.WHITELIST),
                Rule(2, "203.0.113.0/24", IpFilterConstants.BLACKLIST));

            // 8.8.8.8 is denied for key 1 (whitelist) but allowed for key 2 (only 203.0.113.x blocked).
            (await service.IsIpAllowedForVirtualKeyAsync("8.8.8.8", virtualKeyId: 1)).Should().BeFalse();
            (await service.IsIpAllowedForVirtualKeyAsync("8.8.8.8", virtualKeyId: 2)).Should().BeTrue();
        }
    }
}

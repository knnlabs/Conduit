using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Security
{
    /// <summary>
    /// Unit tests for IP filtering service implementations.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Security")]
    public class IpFilteringServiceTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IMemoryCache> _mockCache;
        private readonly Mock<ILogger<IIpFilteringService>> _mockLogger;
        private readonly ITestOutputHelper _output;

        public IpFilteringServiceTests(ITestOutputHelper output)
        {
            _output = output;
            _mockConfiguration = new Mock<IConfiguration>();
            _mockCache = new Mock<IMemoryCache>();
            _mockLogger = new Mock<ILogger<IIpFilteringService>>();
            
            SetupConfiguration();
        }

        private void SetupConfiguration()
        {
            // Setup whitelist configuration
            var whitelistSection = new Mock<IConfigurationSection>();
            whitelistSection.Setup(x => x.GetChildren()).Returns(new List<IConfigurationSection>
            {
                CreateConfigSection("0", "192.168.1.0/24"),
                CreateConfigSection("1", "10.0.0.1"),
                CreateConfigSection("2", "172.16.0.0/16")
            });
            _mockConfiguration.Setup(x => x.GetSection("Security:IpWhitelist")).Returns(whitelistSection.Object);

            // Setup blacklist configuration
            var blacklistSection = new Mock<IConfigurationSection>();
            blacklistSection.Setup(x => x.GetChildren()).Returns(new List<IConfigurationSection>
            {
                CreateConfigSection("0", "203.0.113.0/24"),
                CreateConfigSection("1", "198.51.100.14")
            });
            _mockConfiguration.Setup(x => x.GetSection("Security:IpBlacklist")).Returns(blacklistSection.Object);

            // Setup other security settings
            _mockConfiguration.Setup(x => x["Security:EnableIpFiltering"]).Returns("true");
            _mockConfiguration.Setup(x => x["Security:DefaultAction"]).Returns("Allow"); // Allow by default
        }

        private IConfigurationSection CreateConfigSection(string key, string value)
        {
            var section = new Mock<IConfigurationSection>();
            section.Setup(x => x.Key).Returns(key);
            section.Setup(x => x.Value).Returns(value);
            return section.Object;
        }

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
            blacklist.Should().HaveCountGreaterOrEqualTo(2);
            blacklist.Should().Contain(entry => entry.IpAddress == "203.0.113.0/24");
            blacklist.Should().Contain(entry => entry.IpAddress == "198.51.100.14");
        }

        #endregion

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
            // Arrange
            var service = new MockIpFilteringService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);
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
            stats.WhitelistSize.Should().BeGreaterOrEqualTo(3);
            stats.BlacklistSize.Should().BeGreaterOrEqualTo(2);
        }

        #endregion
    }

    // Interfaces and models for testing
    public interface IIpFilteringService
    {
        Task<bool> IsAllowedAsync(string ipAddress);
        Task<bool> AddToWhitelistAsync(string ipAddressOrCidr);
        Task<bool> RemoveFromWhitelistAsync(string ipAddressOrCidr);
        Task<bool> IsInWhitelistAsync(string ipAddress);
        Task<IEnumerable<string>> GetWhitelistAsync();
        Task<bool> AddToBlacklistAsync(string ipAddressOrCidr, string reason);
        Task<bool> RemoveFromBlacklistAsync(string ipAddressOrCidr);
        Task<IEnumerable<BlacklistEntry>> GetBlacklistAsync();
        Task BlockTemporarilyAsync(string ipAddress, TimeSpan duration, string reason);
        Task<FilteringStatistics> GetStatisticsAsync();
    }

    public class BlacklistEntry
    {
        public string IpAddress { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime AddedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class FilteringStatistics
    {
        public long TotalRequests { get; set; }
        public long AllowedRequests { get; set; }
        public long BlockedRequests { get; set; }
        public int WhitelistSize { get; set; }
        public int BlacklistSize { get; set; }
    }

    // Mock implementation
    public class MockIpFilteringService : IIpFilteringService
    {
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<IIpFilteringService> _logger;
        private readonly HashSet<string> _whitelist = new();
        private readonly Dictionary<string, BlacklistEntry> _blacklist = new();
        private readonly FilteringStatistics _stats = new();

        public MockIpFilteringService(
            IConfiguration configuration,
            IMemoryCache cache,
            ILogger<IIpFilteringService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            LoadConfiguredLists();
        }

        private void LoadConfiguredLists()
        {
            // Load whitelist from configuration
            var whitelistSection = _configuration.GetSection("Security:IpWhitelist");
            foreach (var item in whitelistSection.GetChildren())
            {
                if (!string.IsNullOrWhiteSpace(item.Value))
                    _whitelist.Add(item.Value);
            }

            // Load blacklist from configuration
            var blacklistSection = _configuration.GetSection("Security:IpBlacklist");
            foreach (var item in blacklistSection.GetChildren())
            {
                if (!string.IsNullOrWhiteSpace(item.Value))
                {
                    _blacklist[item.Value] = new BlacklistEntry
                    {
                        IpAddress = item.Value,
                        Reason = "Configured in settings",
                        AddedAt = DateTime.UtcNow
                    };
                }
            }
        }

        public async Task<bool> IsAllowedAsync(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                throw new ArgumentException("IP address cannot be null or empty", nameof(ipAddress));

            await Task.Delay(1); // Simulate async work
            
            _stats.TotalRequests++;

            // Check if filtering is enabled
            var filteringEnabled = _configuration["Security:EnableIpFiltering"] == "true";
            if (!filteringEnabled)
            {
                _stats.AllowedRequests++;
                return true;
            }

            // Validate IP format
            if (!IPAddress.TryParse(ipAddress, out var ip))
            {
                _logger.LogWarning("Invalid IP address format: {IpAddress}", ipAddress);
                _stats.BlockedRequests++;
                return false;
            }

            // Check cache
            var cacheKey = $"ip_filter_{ipAddress}";
            if (_cache.TryGetValue(cacheKey, out object? cachedResult) && cachedResult is bool cached)
                return cached;

            // Check blacklist first (takes precedence)
            if (IsInList(ipAddress, _blacklist.Keys))
            {
                _stats.BlockedRequests++;
                _logger.LogInformation("IP {IpAddress} blocked (blacklisted)", ipAddress);
                return false;
            }

            // Check whitelist
            var inWhitelist = IsInList(ipAddress, _whitelist);
            
            // Determine result based on default action
            var defaultAllow = _configuration["Security:DefaultAction"] != "Deny";
            var allowed = inWhitelist || defaultAllow;

            if (allowed)
                _stats.AllowedRequests++;
            else
                _stats.BlockedRequests++;

            return allowed;
        }

        private bool IsInList(string ipAddress, IEnumerable<string> list)
        {
            if (!IPAddress.TryParse(ipAddress, out var ip))
                return false;

            foreach (var entry in list)
            {
                if (entry == ipAddress)
                    return true;

                // Check CIDR ranges
                if (entry.Contains('/') && IsInCidrRange(ip, entry))
                    return true;
            }

            return false;
        }

        private bool IsInCidrRange(IPAddress ip, string cidr)
        {
            var parts = cidr.Split('/');
            if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var network))
                return false;

            if (!int.TryParse(parts[1], out var prefixLength))
                return false;

            // Simple implementation for IPv4
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var ipBytes = ip.GetAddressBytes();
                var networkBytes = network.GetAddressBytes();
                var maskBits = 32 - prefixLength;
                
                for (int i = 0; i < 4; i++)
                {
                    var shift = Math.Max(0, Math.Min(8, maskBits - (3 - i) * 8));
                    var mask = (byte)(0xFF << shift);
                    
                    if ((ipBytes[i] & mask) != (networkBytes[i] & mask))
                        return false;
                }
                
                return true;
            }

            return false;
        }

        public async Task<bool> AddToWhitelistAsync(string ipAddressOrCidr)
        {
            if (string.IsNullOrWhiteSpace(ipAddressOrCidr))
                throw new ArgumentException("IP address cannot be null or empty", nameof(ipAddressOrCidr));

            await Task.Delay(1);
            _whitelist.Add(ipAddressOrCidr);
            _logger.LogInformation("Added {IpAddress} to whitelist", ipAddressOrCidr);
            return true;
        }

        public async Task<bool> RemoveFromWhitelistAsync(string ipAddressOrCidr)
        {
            await Task.Delay(1);
            return _whitelist.Remove(ipAddressOrCidr);
        }

        public async Task<bool> IsInWhitelistAsync(string ipAddress)
        {
            await Task.Delay(1);
            return IsInList(ipAddress, _whitelist);
        }

        public async Task<IEnumerable<string>> GetWhitelistAsync()
        {
            await Task.Delay(1);
            return _whitelist.ToList();
        }

        public async Task<bool> AddToBlacklistAsync(string ipAddressOrCidr, string reason)
        {
            if (string.IsNullOrWhiteSpace(ipAddressOrCidr))
                throw new ArgumentException("IP address cannot be null or empty", nameof(ipAddressOrCidr));

            await Task.Delay(1);
            _blacklist[ipAddressOrCidr] = new BlacklistEntry
            {
                IpAddress = ipAddressOrCidr,
                Reason = reason,
                AddedAt = DateTime.UtcNow
            };
            _logger.LogInformation("Added {IpAddress} to blacklist: {Reason}", ipAddressOrCidr, reason);
            return true;
        }

        public async Task<bool> RemoveFromBlacklistAsync(string ipAddressOrCidr)
        {
            await Task.Delay(1);
            return _blacklist.Remove(ipAddressOrCidr);
        }

        public async Task<IEnumerable<BlacklistEntry>> GetBlacklistAsync()
        {
            await Task.Delay(1);
            return _blacklist.Values.ToList();
        }

        public async Task BlockTemporarilyAsync(string ipAddress, TimeSpan duration, string reason)
        {
            await Task.Delay(1);
            _blacklist[ipAddress] = new BlacklistEntry
            {
                IpAddress = ipAddress,
                Reason = reason,
                AddedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(duration)
            };
            _logger.LogInformation("Temporarily blocked {IpAddress} for {Duration}: {Reason}", 
                ipAddress, duration, reason);
        }

        public async Task<FilteringStatistics> GetStatisticsAsync()
        {
            await Task.Delay(1);
            _stats.WhitelistSize = _whitelist.Count;
            _stats.BlacklistSize = _blacklist.Count;
            return _stats;
        }
    }
}
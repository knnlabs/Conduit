using System.Net;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Security
{
    /// <summary>
    /// Unit tests for IP filtering service implementations.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Security")]
    public partial class IpFilteringServiceTests
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
                
                // Calculate how many bits to check
                var bytesToCheck = prefixLength / 8;
                var remainingBits = prefixLength % 8;
                
                // Check full bytes
                for (int i = 0; i < bytesToCheck; i++)
                {
                    if (ipBytes[i] != networkBytes[i])
                        return false;
                }
                
                // Check remaining bits if any
                if (remainingBits > 0 && bytesToCheck < 4)
                {
                    var mask = (byte)(0xFF << (8 - remainingBits));
                    if ((ipBytes[bytesToCheck] & mask) != (networkBytes[bytesToCheck] & mask))
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
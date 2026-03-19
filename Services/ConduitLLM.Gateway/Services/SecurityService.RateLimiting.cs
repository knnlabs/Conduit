using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Security.Models;

namespace ConduitLLM.Gateway.Services
{
    public partial class SecurityService
    {
        /// <inheritdoc/>
        public async Task<RateLimitCheckResult> CheckVirtualKeyRateLimitAsync(HttpContext context, string virtualKeyId, string endpoint)
        {
            if (!context.Items.ContainsKey("VirtualKeyEntity"))
            {
                return new RateLimitCheckResult { IsAllowed = true };
            }

            var virtualKey = context.Items["VirtualKeyEntity"] as VirtualKey;
            if (virtualKey == null)
            {
                return new RateLimitCheckResult { IsAllowed = true };
            }

            var now = DateTime.UtcNow;
            var result = new RateLimitCheckResult { IsAllowed = true };

            // Check RPM (Requests Per Minute) limit
            if (virtualKey.RateLimitRpm.HasValue && virtualKey.RateLimitRpm.Value > 0)
            {
                var rpmKey = $"{VkeyRateLimitPrefix}rpm:{virtualKeyId}";
                var rpmCount = await GetRateLimitCountAsync(rpmKey, 60);

                if (rpmCount >= virtualKey.RateLimitRpm.Value)
                {
                    Logger.LogWarning("Virtual Key {KeyId} exceeded RPM limit: {Count}/{Limit}",
                        virtualKeyId, rpmCount, virtualKey.RateLimitRpm.Value);

                    result.IsAllowed = false;
                    result.Limit = virtualKey.RateLimitRpm.Value;
                    result.Remaining = 0;
                    result.ResetsAt = now.AddSeconds(60);
                    return result;
                }

                await IncrementRateLimitCountAsync(rpmKey, 60);
                result.Limit = virtualKey.RateLimitRpm.Value;
                result.Remaining = virtualKey.RateLimitRpm.Value - (rpmCount + 1);
            }

            // Check RPD (Requests Per Day) limit
            if (virtualKey.RateLimitRpd.HasValue && virtualKey.RateLimitRpd.Value > 0)
            {
                var rpdKey = $"{VkeyRateLimitPrefix}rpd:{virtualKeyId}";
                var rpdCount = await GetRateLimitCountAsync(rpdKey, 86400);

                if (rpdCount >= virtualKey.RateLimitRpd.Value)
                {
                    Logger.LogWarning("Virtual Key {KeyId} exceeded RPD limit: {Count}/{Limit}",
                        virtualKeyId, rpdCount, virtualKey.RateLimitRpd.Value);

                    result.IsAllowed = false;
                    result.Limit = virtualKey.RateLimitRpd.Value;
                    result.Remaining = 0;
                    result.ResetsAt = now.Date.AddDays(1);
                    return result;
                }

                await IncrementRateLimitCountAsync(rpdKey, 86400);

                if (!virtualKey.RateLimitRpm.HasValue)
                {
                    result.Limit = virtualKey.RateLimitRpd.Value;
                    result.Remaining = virtualKey.RateLimitRpd.Value - (rpdCount + 1);
                    result.ResetsAt = now.Date.AddDays(1);
                }
            }

            return result;
        }

        private async Task<int> GetRateLimitCountAsync(string key, int windowSeconds)
        {
            if (_options.UseDistributedTracking && DistributedCache != null)
            {
                var cachedValue = await DistributedCache.GetStringAsync(key);
                if (!string.IsNullOrEmpty(cachedValue))
                {
                    if (int.TryParse(cachedValue, out var count))
                        return count;

                    try
                    {
                        var data = JsonSerializer.Deserialize<RateLimitData>(cachedValue);
                        return data?.Count ?? 0;
                    }
                    catch
                    {
                        return 0;
                    }
                }
            }
            else
            {
                return MemoryCache.Get<int>(key);
            }

            return 0;
        }

        private async Task IncrementRateLimitCountAsync(string key, int windowSeconds)
        {
            var currentCount = await GetRateLimitCountAsync(key, windowSeconds);
            currentCount++;

            if (_options.UseDistributedTracking && DistributedCache != null)
            {
                await DistributedCache.SetStringAsync(
                    key,
                    currentCount.ToString(),
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(windowSeconds)
                    });
            }
            else
            {
                MemoryCache.Set(key, currentCount, TimeSpan.FromSeconds(windowSeconds));
            }
        }

        /// <summary>
        /// Checks IP rate limiting with discovery-specific overrides
        /// </summary>
        private async Task<SecurityCheckResult> CheckIpRateLimitWithDiscoveryAsync(string ipAddress, string path)
        {
            // Check discovery-specific rate limiting first
            if (_options.RateLimiting.Discovery.Enabled && IsDiscoveryPath(path))
            {
                var discoveryResult = await CheckDiscoveryRateLimitAsync(ipAddress, path);
                if (!discoveryResult.IsAllowed)
                {
                    return discoveryResult;
                }
            }

            // Fall through to base IP rate limiting
            return await CheckIpRateLimitAsync(ipAddress);
        }

        private bool IsDiscoveryPath(string path)
        {
            return _options.RateLimiting.Discovery.DiscoveryPaths
                .Any(discoveryPath => path.Contains(discoveryPath, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<SecurityCheckResult> CheckDiscoveryRateLimitAsync(string ipAddress, string path)
        {
            var discoveryKey = $"{RateLimitPrefix}discovery:{ipAddress}";

            var discoveryCount = await GetRateLimitCountAsync(discoveryKey, _options.RateLimiting.Discovery.WindowSeconds);
            discoveryCount++;

            if (discoveryCount > _options.RateLimiting.Discovery.MaxRequests)
            {
                Logger.LogWarning("Discovery rate limit exceeded for {IpAddress}: {Count} requests in {Window} seconds for path {Path}",
                    ipAddress, discoveryCount, _options.RateLimiting.Discovery.WindowSeconds, path);

                return new SecurityCheckResult
                {
                    IsAllowed = false,
                    Reason = $"Discovery rate limit exceeded for path {path}",
                    StatusCode = 429,
                    Headers = new Dictionary<string, string>
                    {
                        ["Retry-After"] = _options.RateLimiting.Discovery.WindowSeconds.ToString(),
                        ["X-RateLimit-Limit"] = _options.RateLimiting.Discovery.MaxRequests.ToString(),
                        ["X-RateLimit-Scope"] = "discovery",
                        ["X-RateLimit-Remaining"] = Math.Max(0, _options.RateLimiting.Discovery.MaxRequests - discoveryCount).ToString()
                    }
                };
            }

            // Check per-model capability rate limiting
            if (path.Contains("/capabilities/", StringComparison.OrdinalIgnoreCase))
            {
                var modelMatch = ExtractModelFromPath(path);
                if (!string.IsNullOrEmpty(modelMatch))
                {
                    var capabilityResult = await CheckModelCapabilityRateLimitAsync(ipAddress, modelMatch);
                    if (!capabilityResult.IsAllowed)
                    {
                        return capabilityResult;
                    }
                }
            }

            await IncrementRateLimitCountAsync(discoveryKey, _options.RateLimiting.Discovery.WindowSeconds);
            return SecurityCheckResult.Allowed();
        }

        private async Task<SecurityCheckResult> CheckModelCapabilityRateLimitAsync(string ipAddress, string modelName)
        {
            var capabilityKey = $"{RateLimitPrefix}capability:{ipAddress}:{modelName}";

            var capabilityCount = await GetRateLimitCountAsync(capabilityKey, _options.RateLimiting.Discovery.CapabilityCheckWindowSeconds);
            capabilityCount++;

            if (capabilityCount > _options.RateLimiting.Discovery.MaxCapabilityChecksPerModel)
            {
                Logger.LogWarning("Model capability rate limit exceeded for {IpAddress} and model {Model}: {Count} requests in {Window} seconds",
                    ipAddress, modelName, capabilityCount, _options.RateLimiting.Discovery.CapabilityCheckWindowSeconds);

                return new SecurityCheckResult
                {
                    IsAllowed = false,
                    Reason = $"Capability check rate limit exceeded for model {modelName}",
                    StatusCode = 429,
                    Headers = new Dictionary<string, string>
                    {
                        ["Retry-After"] = _options.RateLimiting.Discovery.CapabilityCheckWindowSeconds.ToString(),
                        ["X-RateLimit-Limit"] = _options.RateLimiting.Discovery.MaxCapabilityChecksPerModel.ToString(),
                        ["X-RateLimit-Scope"] = "model-capability",
                        ["X-RateLimit-Model"] = modelName
                    }
                };
            }

            await IncrementRateLimitCountAsync(capabilityKey, _options.RateLimiting.Discovery.CapabilityCheckWindowSeconds);
            return SecurityCheckResult.Allowed();
        }

        private static string ExtractModelFromPath(string path)
        {
            try
            {
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < segments.Length - 2; i++)
                {
                    if (segments[i].Equals("models", StringComparison.OrdinalIgnoreCase) &&
                        i + 2 < segments.Length &&
                        segments[i + 2].Equals("capabilities", StringComparison.OrdinalIgnoreCase))
                    {
                        return segments[i + 1];
                    }
                }
                return "";
            }
            catch
            {
                return "";
            }
        }
    }
}

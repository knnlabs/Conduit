using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Http.Services
{
    public partial class SecurityService
    {
        /// <inheritdoc/>
        public async Task<RateLimitCheckResult> CheckVirtualKeyRateLimitAsync(HttpContext context, string virtualKeyId, string endpoint)
        {
            // Get the Virtual Key entity from context to check its limits
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
                var rpmKey = $"{VKEY_RATE_LIMIT_PREFIX}rpm:{virtualKeyId}";
                var rpmCount = await GetRateLimitCountAsync(rpmKey, 60); // 60 seconds window
                
                if (rpmCount >= virtualKey.RateLimitRpm.Value)
                {
                    _logger.LogWarning("Virtual Key {KeyId} exceeded RPM limit: {Count}/{Limit}", 
                        virtualKeyId, rpmCount, virtualKey.RateLimitRpm.Value);
                    
                    result.IsAllowed = false;
                    result.Limit = virtualKey.RateLimitRpm.Value;
                    result.Remaining = 0;
                    result.ResetsAt = now.AddSeconds(60);
                    return result;
                }

                // Increment counter
                await IncrementRateLimitCountAsync(rpmKey, 60);
                result.Limit = virtualKey.RateLimitRpm.Value;
                result.Remaining = virtualKey.RateLimitRpm.Value - (rpmCount + 1);
            }

            // Check RPD (Requests Per Day) limit
            if (virtualKey.RateLimitRpd.HasValue && virtualKey.RateLimitRpd.Value > 0)
            {
                var rpdKey = $"{VKEY_RATE_LIMIT_PREFIX}rpd:{virtualKeyId}";
                var rpdCount = await GetRateLimitCountAsync(rpdKey, 86400); // 24 hours in seconds
                
                if (rpdCount >= virtualKey.RateLimitRpd.Value)
                {
                    _logger.LogWarning("Virtual Key {KeyId} exceeded RPD limit: {Count}/{Limit}", 
                        virtualKeyId, rpdCount, virtualKey.RateLimitRpd.Value);
                    
                    result.IsAllowed = false;
                    result.Limit = virtualKey.RateLimitRpd.Value;
                    result.Remaining = 0;
                    result.ResetsAt = now.Date.AddDays(1); // Next day
                    return result;
                }

                // Increment counter
                await IncrementRateLimitCountAsync(rpdKey, 86400);
                
                // If we have RPM limit, that takes precedence for response headers
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
            if (_options.UseDistributedTracking && _distributedCache != null)
            {
                var cachedValue = await _distributedCache.GetStringAsync(key);
                if (!string.IsNullOrEmpty(cachedValue))
                {
                    if (int.TryParse(cachedValue, out var count))
                        return count;
                    
                    // Try to deserialize as complex object for backward compatibility
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
                return _memoryCache.Get<int>(key);
            }
            
            return 0;
        }

        private async Task IncrementRateLimitCountAsync(string key, int windowSeconds)
        {
            var currentCount = await GetRateLimitCountAsync(key, windowSeconds);
            currentCount++;

            if (_options.UseDistributedTracking && _distributedCache != null)
            {
                await _distributedCache.SetStringAsync(
                    key,
                    currentCount.ToString(),
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(windowSeconds)
                    });
            }
            else
            {
                _memoryCache.Set(key, currentCount, TimeSpan.FromSeconds(windowSeconds));
            }
        }

        private async Task<SecurityCheckResult> CheckIpRateLimitAsync(string ipAddress, string path = "")
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

            // Check general IP rate limiting
            var key = $"{RATE_LIMIT_PREFIX}{SERVICE_NAME}:{ipAddress}";
            var now = DateTime.UtcNow;

            // Get current request count
            var requestCount = 0;
            if (_options.UseDistributedTracking && _distributedCache != null)
            {
                var cachedValue = await _distributedCache.GetStringAsync(key);
                if (!string.IsNullOrEmpty(cachedValue))
                {
                    var data = JsonSerializer.Deserialize<RateLimitData>(cachedValue);
                    requestCount = data?.Count ?? 0;
                }
            }
            else
            {
                requestCount = _memoryCache.Get<int>(key);
            }

            requestCount++;

            if (requestCount > _options.RateLimiting.MaxRequests)
            {
                _logger.LogWarning("IP rate limit exceeded for {IpAddress}: {Count} requests in {Window} seconds",
                    ipAddress, requestCount, _options.RateLimiting.WindowSeconds);

                return new SecurityCheckResult
                {
                    IsAllowed = false,
                    Reason = $"Rate limit exceeded for path {path}",
                    StatusCode = 429,
                    Headers = new Dictionary<string, string>
                    {
                        ["Retry-After"] = _options.RateLimiting.WindowSeconds.ToString(),
                        ["X-RateLimit-Limit"] = _options.RateLimiting.MaxRequests.ToString(),
                        ["X-RateLimit-Scope"] = "general"
                    }
                };
            }

            // Update the counter
            var rateLimitData = new RateLimitData
            {
                Count = requestCount,
                Source = SERVICE_NAME,
                WindowStart = now
            };

            if (_options.UseDistributedTracking && _distributedCache != null)
            {
                await _distributedCache.SetStringAsync(
                    key,
                    JsonSerializer.Serialize(rateLimitData),
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.RateLimiting.WindowSeconds)
                    });
            }
            else
            {
                _memoryCache.Set(key, requestCount, TimeSpan.FromSeconds(_options.RateLimiting.WindowSeconds));
            }

            return new SecurityCheckResult { IsAllowed = true };
        }

        /// <summary>
        /// Checks if the path is a discovery-related endpoint
        /// </summary>
        private bool IsDiscoveryPath(string path)
        {
            return _options.RateLimiting.Discovery.DiscoveryPaths
                .Any(discoveryPath => path.Contains(discoveryPath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks discovery-specific rate limits
        /// </summary>
        private async Task<SecurityCheckResult> CheckDiscoveryRateLimitAsync(string ipAddress, string path)
        {
            var discoveryKey = $"{RATE_LIMIT_PREFIX}discovery:{ipAddress}";
            var now = DateTime.UtcNow;

            // Get current discovery request count
            var discoveryCount = await GetRateLimitCountAsync(discoveryKey, _options.RateLimiting.Discovery.WindowSeconds);
            discoveryCount++;

            if (discoveryCount > _options.RateLimiting.Discovery.MaxRequests)
            {
                _logger.LogWarning("Discovery rate limit exceeded for {IpAddress}: {Count} requests in {Window} seconds for path {Path}",
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

            // Check per-model capability rate limiting for capability endpoints
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

            // Increment discovery counter
            await IncrementRateLimitCountAsync(discoveryKey, _options.RateLimiting.Discovery.WindowSeconds);

            return new SecurityCheckResult { IsAllowed = true };
        }

        /// <summary>
        /// Checks per-model capability rate limits
        /// </summary>
        private async Task<SecurityCheckResult> CheckModelCapabilityRateLimitAsync(string ipAddress, string modelName)
        {
            var capabilityKey = $"{RATE_LIMIT_PREFIX}capability:{ipAddress}:{modelName}";
            var now = DateTime.UtcNow;

            var capabilityCount = await GetRateLimitCountAsync(capabilityKey, _options.RateLimiting.Discovery.CapabilityCheckWindowSeconds);
            capabilityCount++;

            if (capabilityCount > _options.RateLimiting.Discovery.MaxCapabilityChecksPerModel)
            {
                _logger.LogWarning("Model capability rate limit exceeded for {IpAddress} and model {Model}: {Count} requests in {Window} seconds",
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

            // Increment capability counter
            await IncrementRateLimitCountAsync(capabilityKey, _options.RateLimiting.Discovery.CapabilityCheckWindowSeconds);

            return new SecurityCheckResult { IsAllowed = true };
        }

        /// <summary>
        /// Extracts model name from capability path
        /// </summary>
        private string ExtractModelFromPath(string path)
        {
            try
            {
                // Match patterns like /v1/discovery/models/{model}/capabilities/{capability}
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
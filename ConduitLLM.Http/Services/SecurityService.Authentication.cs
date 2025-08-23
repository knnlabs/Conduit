using System.Text.Json;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace ConduitLLM.Http.Services
{
    public partial class SecurityService
    {
        /// <inheritdoc/>
        public async Task RecordFailedAuthAsync(string ipAddress, string attemptedKey)
        {
            var key = $"{FAILED_LOGIN_PREFIX}{ipAddress}";
            var banKey = $"{BAN_PREFIX}{ipAddress}";

            // Get current failed attempts
            var attempts = 0;
            if (_options.UseDistributedTracking && _distributedCache != null)
            {
                var cachedValue = await _distributedCache.GetStringAsync(key);
                if (!string.IsNullOrEmpty(cachedValue))
                {
                    var data = JsonSerializer.Deserialize<FailedAuthData>(cachedValue);
                    attempts = data?.Attempts ?? 0;
                }
            }
            else
            {
                attempts = _memoryCache.Get<int>(key);
            }

            attempts++;

            // Log the attempt
            _logger.LogWarning("Failed authentication attempt {Attempts}/{MaxAttempts} for IP {IpAddress} with key {Key}", 
                attempts, _options.FailedAuth.MaxAttempts, ipAddress, 
                attemptedKey.Length > 10 ? attemptedKey.Substring(0, 10) + "..." : attemptedKey);

            // Check if we should ban the IP
            if (attempts >= _options.FailedAuth.MaxAttempts)
            {
                var banInfo = new BannedIpInfo
                {
                    BannedUntil = DateTime.UtcNow.AddMinutes(_options.FailedAuth.BanDurationMinutes),
                    FailedAttempts = attempts,
                    Source = SERVICE_NAME,
                    Reason = "Exceeded max failed Virtual Key authentication attempts",
                    LastAttemptedKey = attemptedKey.Length > 10 ? attemptedKey.Substring(0, 10) + "..." : attemptedKey
                };

                if (_options.UseDistributedTracking && _distributedCache != null)
                {
                    await _distributedCache.SetStringAsync(
                        banKey,
                        JsonSerializer.Serialize(banInfo),
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.FailedAuth.BanDurationMinutes)
                        });
                }
                else
                {
                    _memoryCache.Set(banKey, banInfo, TimeSpan.FromMinutes(_options.FailedAuth.BanDurationMinutes));
                }

                _logger.LogWarning("IP {IpAddress} has been banned after {Attempts} failed Virtual Key authentication attempts", 
                    ipAddress, attempts);

                // Record IP ban in security event monitoring
                _securityEventMonitoring?.RecordIpBan(ipAddress, "Exceeded max failed Virtual Key authentication attempts", attempts);

                // Clear the failed attempts counter
                if (_options.UseDistributedTracking && _distributedCache != null)
                {
                    await _distributedCache.RemoveAsync(key);
                }
                else
                {
                    _memoryCache.Remove(key);
                }
            }
            else
            {
                // Update the failed attempts counter
                var authData = new FailedAuthData
                {
                    Attempts = attempts,
                    Source = SERVICE_NAME,
                    LastAttempt = DateTime.UtcNow,
                    LastAttemptedKey = attemptedKey.Length > 10 ? attemptedKey.Substring(0, 10) + "..." : attemptedKey
                };

                if (_options.UseDistributedTracking && _distributedCache != null)
                {
                    await _distributedCache.SetStringAsync(
                        key,
                        JsonSerializer.Serialize(authData),
                        new DistributedCacheEntryOptions
                        {
                            SlidingExpiration = TimeSpan.FromMinutes(_options.FailedAuth.BanDurationMinutes)
                        });
                }
                else
                {
                    _memoryCache.Set(key, attempts, TimeSpan.FromMinutes(_options.FailedAuth.BanDurationMinutes));
                }
            }
        }

        /// <inheritdoc/>
        public async Task ClearFailedAuthAttemptsAsync(string ipAddress)
        {
            var key = $"{FAILED_LOGIN_PREFIX}{ipAddress}";

            if (_options.UseDistributedTracking && _distributedCache != null)
            {
                await _distributedCache.RemoveAsync(key);
            }
            else
            {
                _memoryCache.Remove(key);
            }

            _logger.LogDebug("Cleared failed authentication attempts for IP {IpAddress} after successful auth", ipAddress);
        }

        /// <inheritdoc/>
        public async Task<bool> IsIpBannedAsync(string ipAddress)
        {
            var banKey = $"{BAN_PREFIX}{ipAddress}";

            if (_options.UseDistributedTracking && _distributedCache != null)
            {
                var cachedValue = await _distributedCache.GetStringAsync(banKey);
                if (!string.IsNullOrEmpty(cachedValue))
                {
                    var banInfo = JsonSerializer.Deserialize<BannedIpInfo>(cachedValue);
                    return banInfo?.BannedUntil > DateTime.UtcNow;
                }
            }
            else
            {
                var banInfo = _memoryCache.Get<BannedIpInfo>(banKey);
                return banInfo?.BannedUntil > DateTime.UtcNow;
            }

            return false;
        }
    }
}
using ConduitLLM.Core.Constants;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Interface for distributed virtual key rate limiting
    /// </summary>
    public interface IVirtualKeyRateLimitService
    {
        /// <summary>
        /// Checks if a request is allowed under the rate limit using sliding window algorithm
        /// </summary>
        Task<RateLimitCheckResult> CheckRateLimitAsync(string virtualKeyHash, int? rpmLimit, int? rpdLimit);
        
        /// <summary>
        /// Gets current usage statistics for a virtual key
        /// </summary>
        Task<RateLimitUsage> GetUsageAsync(string virtualKeyHash);
        
        /// <summary>
        /// Updates cached rate limits for a virtual key
        /// </summary>
        Task UpdateRateLimitsAsync(string virtualKeyHash, int? rpmLimit, int? rpdLimit);
        
        /// <summary>
        /// Removes all rate limit data for a virtual key
        /// </summary>
        Task RemoveRateLimitsAsync(string virtualKeyHash);
    }
    
    /// <summary>
    /// Result of a rate limit check
    /// </summary>
    public class RateLimitCheckResult
    {
        public bool IsAllowed { get; set; }
        public int RequestsRemaining { get; set; }
        public int Limit { get; set; }
        public DateTime ResetsAt { get; set; }
        public string LimitType { get; set; } = ""; // RPM or RPD
    }
    
    /// <summary>
    /// Current usage statistics
    /// </summary>
    public class RateLimitUsage
    {
        public int RequestsThisMinute { get; set; }
        public int RequestsToday { get; set; }
        public DateTime MinuteWindowStart { get; set; }
        public DateTime DayWindowStart { get; set; }
    }
    
    /// <summary>
    /// Redis-based implementation of virtual key rate limiting
    /// Uses sliding window algorithm with sorted sets for accuracy
    /// </summary>
    public class RedisVirtualKeyRateLimitService : IVirtualKeyRateLimitService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisVirtualKeyRateLimitService> _logger;
        private readonly SlidingWindowRateLimiter _slidingWindow;

        public RedisVirtualKeyRateLimitService(
            IConnectionMultiplexer redis,
            ILogger<RedisVirtualKeyRateLimitService> logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _slidingWindow = new SlidingWindowRateLimiter(redis, logger);
        }
        
        public async Task<RateLimitCheckResult> CheckRateLimitAsync(string virtualKeyHash, int? rpmLimit, int? rpdLimit)
        {
            if (string.IsNullOrEmpty(virtualKeyHash))
                throw new ArgumentException("Virtual key hash cannot be null or empty", nameof(virtualKeyHash));
            
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            SlidingWindowResult? rpmResult = null;
            SlidingWindowResult? rpdResult = null;

            // Check RPM limit first (more restrictive)
            if (rpmLimit.HasValue && rpmLimit.Value > 0)
            {
                var rpmKey = RedisKeys.RateLimit.VirtualKeyRpm(virtualKeyHash);
                rpmResult = await _slidingWindow.CheckAsync(rpmKey, now, 60000, rpmLimit.Value); // 60 seconds in ms
                
                if (!rpmResult.IsAllowed)
                {
                    _logger.LogWarning("Virtual key {KeyHash} exceeded RPM limit: {Current}/{Limit}", 
                        virtualKeyHash, rpmResult.Current, rpmLimit.Value);
                    
                    return new RateLimitCheckResult
                    {
                        IsAllowed = false,
                        RequestsRemaining = 0,
                        Limit = rpmLimit.Value,
                        ResetsAt = DateTime.UtcNow.AddSeconds(60),
                        LimitType = "RPM"
                    };
                }
            }
            
            // Check RPD limit if configured (even if RPM was checked)
            if (rpdLimit.HasValue && rpdLimit.Value > 0)
            {
                var rpdKey = RedisKeys.RateLimit.VirtualKeyRpd(virtualKeyHash);
                rpdResult = await _slidingWindow.CheckAsync(rpdKey, now, 86400000, rpdLimit.Value); // 24 hours in ms
                
                if (!rpdResult.IsAllowed)
                {
                    _logger.LogWarning("Virtual key {KeyHash} exceeded RPD limit: {Current}/{Limit}", 
                        virtualKeyHash, rpdResult.Current, rpdLimit.Value);
                    
                    return new RateLimitCheckResult
                    {
                        IsAllowed = false,
                        RequestsRemaining = 0,
                        Limit = rpdLimit.Value,
                        ResetsAt = DateTime.UtcNow.Date.AddDays(1),
                        LimitType = "RPD"
                    };
                }
            }
            
            // Both checks passed (or were not configured)
            // Return the more restrictive limit info (RPM if both are configured)
            if (rpmResult != null)
            {
                return new RateLimitCheckResult
                {
                    IsAllowed = true,
                    RequestsRemaining = Math.Max(0, rpmLimit!.Value - rpmResult.Current),
                    Limit = rpmLimit.Value,
                    ResetsAt = DateTime.UtcNow.AddSeconds(60),
                    LimitType = "RPM"
                };
            }
            else if (rpdResult != null)
            {
                return new RateLimitCheckResult
                {
                    IsAllowed = true,
                    RequestsRemaining = Math.Max(0, rpdLimit!.Value - rpdResult.Current),
                    Limit = rpdLimit.Value,
                    ResetsAt = DateTime.UtcNow.Date.AddDays(1),
                    LimitType = "RPD"
                };
            }
            
            // No limits configured
            return new RateLimitCheckResult
            {
                IsAllowed = true,
                RequestsRemaining = int.MaxValue,
                Limit = int.MaxValue,
                ResetsAt = DateTime.UtcNow.AddHours(1)
            };
        }
        
        public async Task<RateLimitUsage> GetUsageAsync(string virtualKeyHash)
        {
            if (string.IsNullOrEmpty(virtualKeyHash))
                throw new ArgumentException("Virtual key hash cannot be null or empty", nameof(virtualKeyHash));
            
            var db = _redis.GetDatabase();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            var usage = new RateLimitUsage
            {
                MinuteWindowStart = DateTime.UtcNow.AddMinutes(-1),
                DayWindowStart = DateTime.UtcNow.Date
            };
            
            // Get RPM usage
            var rpmKey = RedisKeys.RateLimit.VirtualKeyRpm(virtualKeyHash);
            await db.SortedSetRemoveRangeByScoreAsync(rpmKey, 0, now - 60000);
            usage.RequestsThisMinute = (int)await db.SortedSetLengthAsync(rpmKey);

            // Get RPD usage
            var rpdKey = RedisKeys.RateLimit.VirtualKeyRpd(virtualKeyHash);
            await db.SortedSetRemoveRangeByScoreAsync(rpdKey, 0, now - 86400000);
            usage.RequestsToday = (int)await db.SortedSetLengthAsync(rpdKey);
            
            return usage;
        }
        
        public async Task UpdateRateLimitsAsync(string virtualKeyHash, int? rpmLimit, int? rpdLimit)
        {
            if (string.IsNullOrEmpty(virtualKeyHash))
                throw new ArgumentException("Virtual key hash cannot be null or empty", nameof(virtualKeyHash));
            
            var db = _redis.GetDatabase();
            var limitsKey = RedisKeys.RateLimit.VirtualKeyLimits(virtualKeyHash);
            
            var transaction = db.CreateTransaction();
            
            if (rpmLimit.HasValue)
                _ = transaction.HashSetAsync(limitsKey, "rpm", rpmLimit.Value);
            else
                _ = transaction.HashDeleteAsync(limitsKey, "rpm");
            
            if (rpdLimit.HasValue)
                _ = transaction.HashSetAsync(limitsKey, "rpd", rpdLimit.Value);
            else
                _ = transaction.HashDeleteAsync(limitsKey, "rpd");
            
            _ = transaction.HashSetAsync(limitsKey, "updated", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            _ = transaction.KeyExpireAsync(limitsKey, TimeSpan.FromDays(7));
            
            await transaction.ExecuteAsync();
            
            _logger.LogDebug("Updated rate limits for virtual key {KeyHash}: RPM={RPM}, RPD={RPD}", 
                virtualKeyHash, rpmLimit, rpdLimit);
        }
        
        public async Task RemoveRateLimitsAsync(string virtualKeyHash)
        {
            if (string.IsNullOrEmpty(virtualKeyHash))
                throw new ArgumentException("Virtual key hash cannot be null or empty", nameof(virtualKeyHash));
            
            var db = _redis.GetDatabase();
            
            var transaction = db.CreateTransaction();
            _ = transaction.KeyDeleteAsync(RedisKeys.RateLimit.VirtualKeyLimits(virtualKeyHash));
            _ = transaction.KeyDeleteAsync(RedisKeys.RateLimit.VirtualKeyRpm(virtualKeyHash));
            _ = transaction.KeyDeleteAsync(RedisKeys.RateLimit.VirtualKeyRpd(virtualKeyHash));
            _ = transaction.KeyDeleteAsync(RedisKeys.RateLimit.VirtualKeyRpmSeq(virtualKeyHash));
            _ = transaction.KeyDeleteAsync(RedisKeys.RateLimit.VirtualKeyRpdSeq(virtualKeyHash));
            
            await transaction.ExecuteAsync();
            
            _logger.LogDebug("Removed all rate limit data for virtual key {KeyHash}", virtualKeyHash);
        }
        
    }
}
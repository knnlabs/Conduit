using ConduitLLM.Security.Models;
using ConduitLLM.Security.Options;
using ConduitLLM.Security.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Tests.Security;

public sealed class SecurityServiceBaseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecordFailedAuthAsync_ReachesBanThreshold(bool useDistributedTracking)
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(memoryCache, useDistributedTracking, maxFailedAttempts: 3);

        await service.RecordFailedAuthAsync("203.0.113.10");
        await service.RecordFailedAuthAsync("203.0.113.10");
        await service.RecordFailedAuthAsync("203.0.113.10");

        Assert.True(await service.IsIpBannedAsync("203.0.113.10"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckIpRateLimitAsync_DeniesRequestBeyondLimit(bool useDistributedTracking)
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(memoryCache, useDistributedTracking, maxRequests: 2);

        Assert.True((await service.CheckRateLimitAsync("203.0.113.20")).IsAllowed);
        Assert.True((await service.CheckRateLimitAsync("203.0.113.20")).IsAllowed);

        var result = await service.CheckRateLimitAsync("203.0.113.20");

        Assert.False(result.IsAllowed);
        Assert.Equal(StatusCodes.Status429TooManyRequests, result.StatusCode);
        Assert.Equal("60", result.Headers["Retry-After"]);
        Assert.Equal("2", result.Headers["X-RateLimit-Limit"]);
    }

    private static TestSecurityService CreateService(
        IMemoryCache memoryCache,
        bool useDistributedTracking,
        int maxFailedAttempts = 5,
        int maxRequests = 100)
    {
        var options = new SecurityOptionsBase
        {
            UseDistributedTracking = useDistributedTracking,
            FailedAuth =
            {
                Enabled = true,
                MaxAttempts = maxFailedAttempts,
                BanDurationMinutes = 5
            },
            RateLimiting =
            {
                Enabled = true,
                MaxRequests = maxRequests,
                WindowSeconds = 60
            }
        };

        IDistributedCache? distributedCache = useDistributedTracking
            ? new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()))
            : null;

        return new TestSecurityService(options, memoryCache, distributedCache);
    }

    private sealed class TestSecurityService : SecurityServiceBase
    {
        private readonly SecurityOptionsBase _options;

        public TestSecurityService(
            SecurityOptionsBase options,
            IMemoryCache memoryCache,
            IDistributedCache? distributedCache)
            : base(NullLogger<TestSecurityService>.Instance, memoryCache, distributedCache)
        {
            _options = options;
        }

        protected override string ServiceName => "test";
        protected override SecurityOptionsBase Options => _options;

        public override Task<SecurityCheckResult> IsRequestAllowedAsync(HttpContext context)
        {
            return Task.FromResult(SecurityCheckResult.Allowed());
        }

        public Task<SecurityCheckResult> CheckRateLimitAsync(string ipAddress)
        {
            return CheckIpRateLimitAsync(ipAddress);
        }
    }
}

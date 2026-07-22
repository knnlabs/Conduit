using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services;

public sealed class SlidingWindowRateLimiterTests
{
    [Fact]
    public void SlidingWindowScript_ExpiresWindowAndSequenceKeysTogether()
    {
        var script = typeof(SlidingWindowRateLimiter)
            .GetField("SlidingWindowScript", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?.GetRawConstantValue() as string;

        Assert.NotNull(script);
        Assert.Contains("local expiry = math.ceil(window / 1000) + 60", script);
        Assert.Contains("redis.call('EXPIRE', key, expiry)", script);
        Assert.Contains("redis.call('EXPIRE', key .. ':seq', expiry)", script);
    }
}

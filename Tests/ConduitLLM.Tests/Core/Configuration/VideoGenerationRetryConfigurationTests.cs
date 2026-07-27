using ConduitLLM.Core.Configuration;

using AwesomeAssertions;

namespace ConduitLLM.Tests.Core.Configuration;

/// <summary>
/// Pins the backoff semantics fixed in #1266: jitter is applied before the cap, so
/// MaxDelaySeconds is a hard ceiling, and the result never drops below 1 second.
/// </summary>
[Trait("Category", "Unit")]
public class VideoGenerationRetryConfigurationTests
{
    [Fact]
    public void CalculateRetryDelay_NeverExceedsMaxDelay_EvenWithJitter()
    {
        var config = new VideoGenerationRetryConfiguration
        {
            BaseDelaySeconds = 30,
            MaxDelaySeconds = 3600,
            JitterPercentage = 20
        };

        // At retryCount 7+ the raw backoff (30 * 2^7 = 3840) exceeds the cap; positive
        // jitter must not be able to push the final delay past it.
        for (var i = 0; i < 200; i++)
        {
            config.CalculateRetryDelay(retryCount: 10).Should().BeLessThanOrEqualTo(3600);
        }
    }

    [Fact]
    public void CalculateRetryDelay_HasFloorOfOneSecond()
    {
        var config = new VideoGenerationRetryConfiguration
        {
            BaseDelaySeconds = 1,
            MaxDelaySeconds = 3600,
            JitterPercentage = 100
        };

        // With ±100% jitter on a 1-second base, the raw value can reach 0; the floor holds.
        for (var i = 0; i < 200; i++)
        {
            config.CalculateRetryDelay(retryCount: 0).Should().BeGreaterThanOrEqualTo(1);
        }
    }

    [Fact]
    public void CalculateRetryDelay_ZeroJitter_IsDeterministicExponentialBackoff()
    {
        var config = new VideoGenerationRetryConfiguration
        {
            BaseDelaySeconds = 30,
            MaxDelaySeconds = 3600,
            JitterPercentage = 0
        };

        config.CalculateRetryDelay(0).Should().Be(30);
        config.CalculateRetryDelay(1).Should().Be(60);
        config.CalculateRetryDelay(2).Should().Be(120);
        config.CalculateRetryDelay(10).Should().Be(3600);
    }

    [Fact]
    public void CalculateRetryDelay_JitterStaysWithinConfiguredBand()
    {
        var config = new VideoGenerationRetryConfiguration
        {
            BaseDelaySeconds = 100,
            MaxDelaySeconds = 100000,
            JitterPercentage = 20
        };

        for (var i = 0; i < 200; i++)
        {
            var delay = config.CalculateRetryDelay(retryCount: 0);
            delay.Should().BeInRange(80, 120);
        }
    }
}

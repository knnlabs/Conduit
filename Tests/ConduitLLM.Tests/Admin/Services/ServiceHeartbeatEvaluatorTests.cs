using ConduitLLM.Admin.Services;

using AwesomeAssertions;

namespace ConduitLLM.Tests.Admin.Services;

public class ServiceHeartbeatEvaluatorTests
{
    [Theory]
    [InlineData(0, 30, "healthy")]      // just recorded
    [InlineData(30, 30, "healthy")]     // one interval old
    [InlineData(60, 30, "healthy")]     // exactly 2 intervals → still healthy (boundary)
    [InlineData(61, 30, "degraded")]    // just past 2 intervals
    [InlineData(120, 30, "degraded")]   // exactly 4 intervals → still degraded (boundary)
    [InlineData(121, 30, "unhealthy")]  // past 4 intervals → heartbeats lost
    [InlineData(600, 30, "unhealthy")]  // long gone
    [InlineData(60, 0, "healthy")]      // non-positive interval falls back to the 30s default (2x = 60)
    [InlineData(61, 0, "degraded")]     // ...and past that default is degraded
    [InlineData(20, 15, "healthy")]     // a different reported cadence is honored
    [InlineData(70, 15, "unhealthy")]   // 70s with a 15s cadence is > 4 intervals
    public void EvaluateStatus_MapsHeartbeatAgeToStatus(double ageSeconds, double intervalSeconds, string expected)
    {
        ServiceHeartbeatEvaluator.EvaluateStatus(ageSeconds, intervalSeconds).Should().Be(expected);
    }
}

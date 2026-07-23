using ConduitLLM.Providers;

using FluentAssertions;

namespace ConduitLLM.Tests.Providers;

public sealed class RouteCircuitRegistryTests
{
    [Fact]
    public void TryBeginAttempt_ExpiredProbeCannotBeReleasedByEarlierClaim()
    {
        var mappingId = Random.Shared.Next(30_000_001, 40_000_000);
        var failedAt = DateTime.UtcNow.AddSeconds(-31);
        for (var failure = 0; failure < 5; failure++)
            RouteCircuitRegistry.Failure(mappingId, failedAt);

        try
        {
            var firstAttemptAt = DateTime.UtcNow;
            RouteCircuitRegistry.TryBeginAttempt(mappingId, firstAttemptAt, out var firstClaim).Should().BeTrue();
            RouteCircuitRegistry.TryBeginAttempt(mappingId, firstAttemptAt.AddSeconds(29), out _).Should().BeFalse();
            RouteCircuitRegistry.TryBeginAttempt(mappingId, firstAttemptAt.AddSeconds(31), out var secondClaim).Should().BeTrue();

            RouteCircuitRegistry.ReleaseProbe(mappingId, firstClaim);

            RouteCircuitRegistry.TryBeginAttempt(mappingId, firstAttemptAt.AddSeconds(31), out _).Should().BeFalse();
            RouteCircuitRegistry.ReleaseProbe(mappingId, secondClaim);
            RouteCircuitRegistry.TryBeginAttempt(mappingId, firstAttemptAt.AddSeconds(31), out _).Should().BeTrue();
        }
        finally
        {
            RouteCircuitRegistry.Success(mappingId);
        }
    }
}

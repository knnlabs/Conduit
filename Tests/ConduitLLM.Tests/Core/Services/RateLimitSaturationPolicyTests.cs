using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services;

[Trait("Category", "Unit")]
[Trait("Component", "RateLimitSaturationPolicy")]
public class RateLimitSaturationPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(RateLimitSaturationPolicy.NormalPriority)]
    [InlineData(RateLimitSaturationPolicy.HighPriority)]
    public void SaturationFractionFor_NormalAndAbove_AppliesTheFullCeiling(int? priority)
    {
        Assert.Null(RateLimitSaturationPolicy.SaturationFractionFor(priority, 0.8));
    }

    [Theory]
    [InlineData(RateLimitSaturationPolicy.LowPriority)]
    [InlineData(-1)]
    public void SaturationFractionFor_BelowNormal_ReturnsTheThreshold(int priority)
    {
        Assert.Equal(0.8, RateLimitSaturationPolicy.SaturationFractionFor(priority, 0.8));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(0.0)]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    public void SaturationFractionFor_ThresholdOutsideTheOpenInterval_DisablesShedding(double threshold)
    {
        // 1 means the capped ceiling would equal the full one; mislabelling that denial as a
        // saturation shed would be a lie, so shedding switches off entirely.
        Assert.Null(RateLimitSaturationPolicy.SaturationFractionFor(RateLimitSaturationPolicy.LowPriority, threshold));
    }

    [Theory]
    [InlineData(1_000, 0.8, 800)]
    [InlineData(5, 0.8, 4)]
    [InlineData(1, 0.8, 0)] // the whole allowance sits inside the reserved headroom
    [InlineData(10_000, 0.95, 9_500)]
    public void Cap_FloorsToTheShedTiersShare(long limit, double fraction, long expected)
    {
        Assert.Equal(expected, RateLimitSaturationPolicy.Cap(limit, fraction));
    }

    [Theory]
    [InlineData("group:RPM:saturation", true)]
    [InlineData("group:TPM:saturation", true)]
    [InlineData("group:RPM", false)]
    [InlineData("RPM", false)]
    public void IsSaturationScope_RecognisesOnlyTheSuffix(string scope, bool expected)
    {
        Assert.Equal(expected, RateLimitSaturationPolicy.IsSaturationScope(scope));
    }
}

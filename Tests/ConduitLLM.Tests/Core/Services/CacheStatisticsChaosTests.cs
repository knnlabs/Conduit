namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Chaos tests to verify distributed cache statistics behavior under adverse conditions.
    /// Split across multiple partial class files:
    /// - CacheStatisticsChaosTests.Setup.cs - Setup, mocks, and helper methods
    /// - CacheStatisticsChaosTests.FailureTests.cs - Redis failures, network partition, clock skew tests
    /// - CacheStatisticsChaosTests.LoadTests.cs - Load testing and memory pressure tests
    /// </summary>
    public partial class CacheStatisticsChaosTests : IDisposable
    {
    }
}
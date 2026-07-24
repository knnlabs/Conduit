using ConduitLLM.Core.Services;
using FluentAssertions;

namespace ConduitLLM.Tests.Core.Services;

[Trait("Category", "Unit")]
[Trait("Component", "DistributedLock")]
public sealed class PostgresDistributedLockServiceTests
{
    [Fact]
    public void GetLockId_IsStableAndAvoidsKnownThirtyOneHashCollision()
    {
        var first = PostgresDistributedLockService.GetLockId("media:cleanup:leader");
        var second = PostgresDistributedLockService.GetLockId("media:cleanup:leader");

        first.Should().Be(second);
        PostgresDistributedLockService.GetLockId("Aa")
            .Should().NotBe(PostgresDistributedLockService.GetLockId("BB"));
    }
}

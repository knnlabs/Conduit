using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Gateway.Billing;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Http.Billing;

public class SpendReservationServiceTests
{
    [Fact]
    public async Task ReserveMapsInsufficientBalanceWithoutThrowing()
    {
        var batch = new Mock<IBatchSpendUpdateService>();
        batch.Setup(x => x.TryReserveSpendAsync(7, 1.25m, "request-1"))
            .ReturnsAsync(false);
        var service = new SpendReservationService(
            batch.Object,
            Mock.Of<ILogger<SpendReservationService>>());

        var result = await service.ReserveAsync(7, "request-1", 1.25m);

        Assert.Equal(SpendReservationOutcome.InsufficientBalance, result.Outcome);
    }

    [Fact]
    public async Task SettleDelegatesToAtomicBatchSettlement()
    {
        var expected = new SpendReservationSettlementResult(
            SpendReservationSettlementStatus.Settled,
            0.75m);
        var batch = new Mock<IBatchSpendUpdateService>();
        batch.Setup(x => x.SettleSpendReservationAsync(7, "request-2", 0.75m, null))
            .ReturnsAsync(expected);
        var service = new SpendReservationService(
            batch.Object,
            Mock.Of<ILogger<SpendReservationService>>());

        var result = await service.SettleAsync(7, "request-2", 0.75m);

        Assert.Same(expected, result);
    }
}

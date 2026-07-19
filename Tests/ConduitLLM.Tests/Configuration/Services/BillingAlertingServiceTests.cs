using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.Configuration.Services;

public class BillingAlertingServiceTests
{
    [Fact]
    public async Task SendCriticalAlertAsync_DuringCooldown_RecordsEveryAuditEvent()
    {
        var logger = new Mock<ILogger<BillingAlertingService>>();
        var auditService = new Mock<IBillingAuditService>();
        var service = new BillingAlertingService(logger.Object, auditService.Object);

        await service.SendCriticalAlertAsync("first failure", 101, new { Amount = 1.25m });
        await service.SendCriticalAlertAsync("second failure", 202, new { Amount = 2.50m });

        auditService.Verify(
            audit => audit.LogBillingEventAsync(It.IsAny<BillingAuditEvent>()),
            Times.Exactly(2));
        auditService.Verify(
            audit => audit.LogBillingEventAsync(It.Is<BillingAuditEvent>(billingEvent =>
                billingEvent.EventType == BillingAuditEventType.SpendUpdateFailed &&
                billingEvent.VirtualKeyId == 202 &&
                billingEvent.FailureReason == "second failure" &&
                billingEvent.MetadataJson != null &&
                billingEvent.MetadataJson.Contains("2.50"))),
            Times.Once);
    }

    [Fact]
    public async Task SendCriticalAlertAsync_AcrossInstances_ThrottlesNotificationsGlobally()
    {
        var firstLogger = new Mock<ILogger<BillingAlertingService>>();
        var secondLogger = new Mock<ILogger<BillingAlertingService>>();
        var firstService = new BillingAlertingService(firstLogger.Object);
        var secondService = new BillingAlertingService(secondLogger.Object);

        await Task.WhenAll(
            firstService.SendCriticalAlertAsync("first failure"),
            secondService.SendCriticalAlertAsync("second failure"));

        var criticalNotifications = CountLogs(firstLogger, LogLevel.Critical) +
            CountLogs(secondLogger, LogLevel.Critical);

        Assert.True(criticalNotifications <= 1);
    }

    private static int CountLogs(Mock<ILogger<BillingAlertingService>> logger, LogLevel level) =>
        logger.Invocations.Count(invocation =>
            invocation.Method.Name == nameof(ILogger.Log) &&
            invocation.Arguments[0] is LogLevel actualLevel &&
            actualLevel == level);
}

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs.HealthMonitoring;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Enums;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Gateway.Interfaces;
using ConduitLLM.Gateway.Services;
using ConduitLLM.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ConduitLLM.Tests.Gateway.Services;

public sealed class BillingReconciliationServiceTests : IDisposable
{
    private readonly DbContextOptions<ConduitDbContext> _dbOptions;
    private readonly ConduitDbContext _context;
    private readonly Mock<IAlertManagementService> _alerts = new();
    private readonly BillingReconciliationService _service;
    private readonly DateTime _window = new(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc);
    private readonly SqliteTestDatabase _database;

    public BillingReconciliationServiceTests()
    {
        _database = new SqliteTestDatabase();
        _dbOptions = _database.Options;
        _context = _database.CreateContext();
        var factory = new Mock<IDbContextFactory<ConduitDbContext>>();
        factory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _database.CreateContext());
        _service = new BillingReconciliationService(
            factory.Object,
            _alerts.Object,
            Options.Create(new BillingReconciliationOptions()),
            Mock.Of<ILogger<BillingReconciliationService>>());
    }

    [Fact]
    public async Task MatchingRequestAndLedger_DoNotCreateAnomaly()
    {
        await SeedAsync(requestCost: 1.25m, ledgerCost: 1.25m);

        await _service.ReconcileWindowAsync(_context, _window, _window.AddHours(1), CancellationToken.None);

        Assert.Empty(await _context.BillingAuditEvents.ToListAsync());
        _alerts.Verify(x => x.TriggerAlertAsync(It.IsAny<HealthAlert>()), Times.Never);
    }

    [Fact]
    public async Task MissingLedgerDebit_CreatesDurableAnomalyAndAlert()
    {
        await SeedAsync(requestCost: 1.25m, ledgerCost: null);

        await _service.ReconcileWindowAsync(_context, _window, _window.AddHours(1), CancellationToken.None);

        var anomaly = Assert.Single(await _context.BillingAuditEvents.ToListAsync());
        Assert.Equal(BillingAuditEventType.BillingReconciliationMismatch, anomaly.EventType);
        Assert.Equal(10, anomaly.VirtualKeyGroupId);
        Assert.Equal(1.25m, anomaly.CalculatedCost);
        Assert.Contains("ledgerDifference", anomaly.MetadataJson);
        _alerts.Verify(x => x.TriggerAlertAsync(It.Is<HealthAlert>(a =>
            a.Type == AlertType.DataIntegrity && a.Component == "BillingReconciliation")), Times.Once);
    }

    [Fact]
    public async Task ProviderMarkupDrift_IsDetectedIndependentlyOfLedger()
    {
        await SeedAsync(
            requestCost: 1m,
            ledgerCost: 1m,
            providerCost: 1m,
            providerMarkup: 1.2m);

        await _service.ReconcileWindowAsync(_context, _window, _window.AddHours(1), CancellationToken.None);

        var anomaly = Assert.Single(await _context.BillingAuditEvents.ToListAsync());
        Assert.Contains("providerDifference", anomaly.MetadataJson);
        Assert.Equal(0.2m, anomaly.CalculatedCost);
    }

    [Fact]
    public async Task DifferenceMustExceedBothThresholds()
    {
        await SeedAsync(requestCost: 1m, ledgerCost: 0.995m);

        await _service.ReconcileWindowAsync(_context, _window, _window.AddHours(1), CancellationToken.None);

        Assert.Empty(await _context.BillingAuditEvents.ToListAsync());
    }

    private async Task SeedAsync(
        decimal requestCost,
        decimal? ledgerCost,
        decimal? providerCost = null,
        decimal? providerMarkup = null)
    {
        var group = new VirtualKeyGroup { Id = 10, GroupName = "test", Balance = 100 };
        var key = new VirtualKey
        {
            Id = 20,
            KeyName = "key",
            KeyHash = "hash",
            VirtualKeyGroupId = group.Id,
            VirtualKeyGroup = group
        };
        _context.VirtualKeyGroups.Add(group);
        _context.VirtualKeys.Add(key);
        _context.RequestLogs.Add(new RequestLog
        {
            VirtualKeyId = key.Id,
            VirtualKey = key,
            ModelName = "model",
            RequestType = "chat",
            Cost = requestCost,
            Timestamp = _window.AddMinutes(2),
            BilledAtUtc = _window.AddMinutes(2),
            BillingMethod = providerCost.HasValue ? RequestBillingMethod.ProviderReportedCost : RequestBillingMethod.ModelCost,
            ProviderReportedCostUsd = providerCost,
            ProviderCostMarkupMultiplier = providerMarkup
        });

        if (ledgerCost.HasValue)
        {
            _context.VirtualKeyGroupTransactions.Add(new VirtualKeyGroupTransaction
            {
                VirtualKeyGroupId = group.Id,
                VirtualKeyGroup = group,
                TransactionType = TransactionType.Debit,
                ReferenceType = ReferenceType.VirtualKey,
                Amount = ledgerCost.Value,
                BalanceAfter = 100 - ledgerCost.Value,
                BillingWindowStartUtc = _window
            });
        }

        await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
        _database.Dispose();
    }
}

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Services;

/// <summary>
/// Service for auditing billing events with batch writing and async processing.
/// </summary>
public class BillingAuditService : BatchAuditServiceBase<BillingAuditEvent>, IBillingAuditService
{
    /// <summary>
    /// Creates a new instance of the BillingAuditService.
    /// </summary>
    /// <param name="serviceProvider">Service provider for creating scoped DbContexts</param>
    /// <param name="logger">Logger instance</param>
    public BillingAuditService(
        IServiceProvider serviceProvider,
        ILogger<BillingAuditService> logger)
        : base(serviceProvider, logger)
    {
    }

    #region Template Method Implementations

    /// <inheritdoc/>
    protected override DbSet<BillingAuditEvent> GetDbSet(ConduitDbContext context)
        => context.BillingAuditEvents;

    /// <inheritdoc/>
    protected override string EntityName => "Billing";

    #endregion

    #region IBillingAuditService Implementation (Wrapper Methods)

    /// <inheritdoc/>
    public Task LogBillingEventAsync(BillingAuditEvent auditEvent)
        => LogEventAsync(auditEvent);

    /// <inheritdoc/>
    public void LogBillingEvent(BillingAuditEvent auditEvent)
        => LogEvent(auditEvent);

    /// <inheritdoc/>
    public Task CleanupOldAuditEventsAsync()
        => CleanupOldEventsAsync();

    #endregion

}

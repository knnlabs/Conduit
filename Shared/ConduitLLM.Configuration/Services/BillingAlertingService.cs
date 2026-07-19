using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.DTOs.HealthMonitoring;
using ConduitLLM.Configuration.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Service for handling critical billing system alerts and notifications
    /// </summary>
    public class BillingAlertingService : IBillingAlertingService
    {
        private readonly ILogger<BillingAlertingService> _logger;
        private readonly IBillingAuditService? _auditService;
        private static readonly object AlertCooldownLock = new();
        private static DateTime _lastAlertTime = DateTime.MinValue;
        private static readonly TimeSpan AlertCooldown = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Initializes a new instance of the BillingAlertingService
        /// </summary>
        public BillingAlertingService(
            ILogger<BillingAlertingService> logger,
            IBillingAuditService? auditService = null)
        {
            _logger = logger;
            _auditService = auditService;
        }

        /// <inheritdoc />
        public async Task SendCriticalAlertAsync(string message, int? virtualKeyId = null, object? additionalContext = null)
        {
            try
            {
                var now = DateTime.UtcNow;
                var serializedContext = additionalContext != null
                    ? JsonSerializer.Serialize(additionalContext)
                    : null;

                // Every failure must be durably recorded. Notification throttling below must not
                // suppress billing audit data used for revenue-loss reporting.
                if (_auditService != null)
                {
                    try
                    {
                        await _auditService.LogBillingEventAsync(new BillingAuditEvent
                        {
                            EventType = BillingAuditEventType.SpendUpdateFailed,
                            VirtualKeyId = virtualKeyId,
                            FailureReason = message,
                            Timestamp = now,
                            MetadataJson = serializedContext
                        });
                    }
                    catch (Exception ex)
                    {
                        // An audit-store outage must not prevent the outbound critical alert.
                        _logger.LogError(ex, "Failed to record critical billing alert in the audit log");
                    }
                }

                var shouldNotify = false;
                lock (AlertCooldownLock)
                {
                    if (now - _lastAlertTime >= AlertCooldown)
                    {
                        _lastAlertTime = now;
                        shouldNotify = true;
                    }
                }

                if (!shouldNotify)
                {
                    _logger.LogWarning("Alert suppressed due to cooldown: {Message}", message);
                    return;
                }

                // Log critical error as the currently configured outbound notification.
                _logger.LogCritical("BILLING SYSTEM CRITICAL ALERT: {Message} | VirtualKeyId: {VirtualKeyId} | Context: {Context}",
                    message, virtualKeyId, serializedContext ?? "N/A");

                // Additional notification mechanisms can be added here
                // For example, sending to external monitoring systems, PagerDuty, etc.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send critical billing alert");
            }
        }

    }
}

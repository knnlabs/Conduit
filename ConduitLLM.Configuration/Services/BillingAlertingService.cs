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
        private DateTime _lastAlertTime = DateTime.MinValue;
        private readonly TimeSpan _alertCooldown = TimeSpan.FromMinutes(5);

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
                // Rate limit alerts to prevent spamming
                if (DateTime.UtcNow - _lastAlertTime < _alertCooldown)
                {
                    _logger.LogWarning("Alert suppressed due to cooldown: {Message}", message);
                    return;
                }

                _lastAlertTime = DateTime.UtcNow;

                // Log critical error
                _logger.LogCritical("BILLING SYSTEM CRITICAL ALERT: {Message} | VirtualKeyId: {VirtualKeyId} | Context: {Context}",
                    message, virtualKeyId, additionalContext != null ? JsonSerializer.Serialize(additionalContext) : "N/A");

                // Record in audit log if available
                if (_auditService != null)
                {
                    await _auditService.LogBillingEventAsync(new BillingAuditEvent
                    {
                        EventType = BillingAuditEventType.SpendUpdateFailed,
                        VirtualKeyId = virtualKeyId,
                        FailureReason = message,
                        Timestamp = DateTime.UtcNow,
                        MetadataJson = additionalContext != null 
                            ? JsonSerializer.Serialize(additionalContext)
                            : null
                    });
                }

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
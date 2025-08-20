using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MassTransit;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Events;
using ConduitLLM.Configuration.Models;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Cache configuration service - Audit and rollback functionality
    /// </summary>
    public partial class CacheConfigurationService
    {
        public async Task<IEnumerable<CacheConfigurationAudit>> GetAuditHistoryAsync(
            string region, 
            int limit = 100, 
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.CacheConfigurationAudits
                .Where(a => a.Region == region)
                .OrderByDescending(a => a.ChangedAt)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task<CacheRegionConfig> RollbackConfigurationAsync(
            string region, 
            int auditId, 
            string rolledBackBy, 
            CancellationToken cancellationToken = default)
        {
            var audit = await _dbContext.CacheConfigurationAudits
                .Where(a => a.Id == auditId && a.Region == region)
                .FirstOrDefaultAsync(cancellationToken);

            if (audit == null)
            {
                throw new InvalidOperationException($"Audit entry {auditId} not found for region {region}");
            }

            if (string.IsNullOrEmpty(audit.OldConfigJson))
            {
                throw new InvalidOperationException($"No previous configuration available to rollback to");
            }

            var configToRestore = JsonSerializer.Deserialize<CacheRegionConfig>(audit.OldConfigJson);
            if (configToRestore == null)
            {
                throw new InvalidOperationException($"Failed to deserialize previous configuration");
            }

            // Update configuration with rollback flag
            var result = await UpdateConfigurationAsync(
                region, 
                configToRestore, 
                rolledBackBy, 
                $"Rollback to configuration from {audit.ChangedAt:yyyy-MM-dd HH:mm:ss}", 
                cancellationToken);

            // Publish rollback event
            await _publishEndpoint.Publish(new CacheConfigurationChangedEvent
            {
                Region = region,
                Action = "RolledBack",
                NewConfig = configToRestore,
                ChangedBy = rolledBackBy,
                ChangedAt = DateTime.UtcNow,
                Reason = $"Rollback to audit entry {auditId}",
                IsRollback = true,
                ChangeSource = "API"
            }, cancellationToken);

            return result;
        }
    }
}
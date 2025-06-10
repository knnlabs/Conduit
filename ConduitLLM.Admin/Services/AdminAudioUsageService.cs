using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Audio;
using ConduitLLM.Configuration.Repositories;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service implementation for managing audio usage analytics.
    /// </summary>
    public class AdminAudioUsageService : IAdminAudioUsageService
    {
        private readonly IAudioUsageLogRepository _repository;
        private readonly IVirtualKeyRepository _virtualKeyRepository;
        private readonly ILogger<AdminAudioUsageService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminAudioUsageService"/> class.
        /// </summary>
        public AdminAudioUsageService(
            IAudioUsageLogRepository repository,
            IVirtualKeyRepository virtualKeyRepository,
            ILogger<AdminAudioUsageService> logger)
        {
            _repository = repository;
            _virtualKeyRepository = virtualKeyRepository;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<PagedResult<AudioUsageDto>> GetUsageLogsAsync(AudioUsageQueryDto query)
        {
            var pagedResult = await _repository.GetPagedAsync(query);
            
            return new PagedResult<AudioUsageDto>
            {
                Items = pagedResult.Items.Select(MapToDto).ToList(),
                TotalCount = pagedResult.TotalCount,
                Page = pagedResult.Page,
                PageSize = pagedResult.PageSize,
                TotalPages = pagedResult.TotalPages
            };
        }

        /// <inheritdoc/>
        public async Task<AudioUsageSummaryDto> GetUsageSummaryAsync(DateTime startDate, DateTime endDate, string? virtualKey = null, string? provider = null)
        {
            return await _repository.GetUsageSummaryAsync(startDate, endDate, virtualKey, provider);
        }

        /// <inheritdoc/>
        public async Task<AudioKeyUsageDto> GetUsageByKeyAsync(string virtualKey, DateTime? startDate = null, DateTime? endDate = null)
        {
            var logs = await _repository.GetByVirtualKeyAsync(virtualKey, startDate, endDate);
            var key = await _virtualKeyRepository.GetByKeyHashAsync(virtualKey);
            
            var effectiveStartDate = startDate ?? DateTime.UtcNow.AddDays(-30);
            var effectiveEndDate = endDate ?? DateTime.UtcNow;
            
            var operationBreakdown = await _repository.GetOperationBreakdownAsync(effectiveStartDate, effectiveEndDate, virtualKey);
            var providerBreakdown = await _repository.GetProviderBreakdownAsync(effectiveStartDate, effectiveEndDate, virtualKey);

            return new AudioKeyUsageDto
            {
                VirtualKey = virtualKey,
                KeyName = key?.KeyName,
                TotalOperations = logs.Count,
                TotalCost = logs.Sum(l => l.Cost),
                OperationBreakdown = operationBreakdown,
                ProviderBreakdown = providerBreakdown,
                RecentLogs = logs.Take(10).Select(MapToDto).ToList()
            };
        }

        /// <inheritdoc/>
        public async Task<AudioProviderUsageDto> GetUsageByProviderAsync(string provider, DateTime? startDate = null, DateTime? endDate = null)
        {
            var logs = await _repository.GetByProviderAsync(provider, startDate, endDate);
            
            var effectiveStartDate = startDate ?? DateTime.UtcNow.AddDays(-30);
            var effectiveEndDate = endDate ?? DateTime.UtcNow;
            
            var operationBreakdown = await _repository.GetOperationBreakdownAsync(effectiveStartDate, effectiveEndDate);
            
            // Calculate daily trend
            var dailyTrend = logs
                .GroupBy(l => l.Timestamp.Date)
                .Select(g => new DailyUsageTrend
                {
                    Date = g.Key,
                    Operations = g.Count(),
                    Cost = g.Sum(l => l.Cost)
                })
                .OrderBy(t => t.Date)
                .ToList();

            var successCount = logs.Count(l => l.StatusCode == null || (l.StatusCode >= 200 && l.StatusCode < 300));
            var totalDuration = logs.Where(l => l.DurationSeconds.HasValue).Sum(l => l.DurationSeconds!.Value);
            var avgResponseTime = logs.Count > 0 ? (totalDuration / logs.Count) * 1000 : 0; // Convert to ms

            return new AudioProviderUsageDto
            {
                Provider = provider,
                TotalOperations = logs.Count,
                SuccessRate = logs.Count > 0 ? (successCount / (double)logs.Count) * 100 : 0,
                AverageResponseTime = avgResponseTime,
                TotalCost = logs.Sum(l => l.Cost),
                OperationBreakdown = operationBreakdown,
                DailyTrend = dailyTrend
            };
        }

        /// <inheritdoc/>
        public async Task<RealtimeSessionMetricsDto> GetRealtimeSessionMetricsAsync()
        {
            // TODO: Implement real-time session tracking
            _logger.LogWarning("Real-time session metrics not yet implemented");
            
            return await Task.FromResult(new RealtimeSessionMetricsDto
            {
                ActiveSessions = 0,
                SessionsByProvider = new Dictionary<string, int>(),
                AverageSessionDuration = 0,
                TotalSessionTimeToday = 0,
                TotalCostToday = 0,
                PeakConcurrentSessions = 0,
                SuccessRate = 100,
                AverageTurnsPerSession = 0
            });
        }

        /// <inheritdoc/>
        public async Task<List<RealtimeSessionDto>> GetActiveSessionsAsync()
        {
            // TODO: Implement real-time session tracking
            _logger.LogWarning("Active sessions tracking not yet implemented");
            return await Task.FromResult(new List<RealtimeSessionDto>());
        }

        /// <inheritdoc/>
        public async Task<RealtimeSessionDto?> GetSessionDetailsAsync(string sessionId)
        {
            // TODO: Implement real-time session tracking
            _logger.LogWarning("Session details not yet implemented");
            return await Task.FromResult<RealtimeSessionDto?>(null);
        }

        /// <inheritdoc/>
        public async Task<bool> TerminateSessionAsync(string sessionId)
        {
            // TODO: Implement real-time session termination
            _logger.LogWarning("Session termination not yet implemented");
            return await Task.FromResult(false);
        }

        /// <inheritdoc/>
        public async Task<string> ExportUsageDataAsync(AudioUsageQueryDto query, string format)
        {
            // TODO: Implement export functionality
            _logger.LogWarning("Export not yet implemented");
            return await Task.FromResult("Export not yet implemented");
        }

        /// <inheritdoc/>
        public async Task<int> CleanupOldLogsAsync(int retentionDays)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            var deletedCount = await _repository.DeleteOldLogsAsync(cutoffDate);
            
            _logger.LogInformation("Cleaned up {Count} audio usage logs older than {Date}", 
                deletedCount, cutoffDate);
            
            return deletedCount;
        }

        private static AudioUsageDto MapToDto(Configuration.Entities.AudioUsageLog log)
        {
            return new AudioUsageDto
            {
                Id = log.Id,
                VirtualKey = log.VirtualKey,
                Provider = log.Provider,
                OperationType = log.OperationType,
                Model = log.Model,
                RequestId = log.RequestId,
                SessionId = log.SessionId,
                DurationSeconds = log.DurationSeconds,
                CharacterCount = log.CharacterCount,
                InputTokens = log.InputTokens,
                OutputTokens = log.OutputTokens,
                Cost = log.Cost,
                Language = log.Language,
                Voice = log.Voice,
                StatusCode = log.StatusCode,
                ErrorMessage = log.ErrorMessage,
                IpAddress = log.IpAddress,
                UserAgent = log.UserAgent,
                Timestamp = log.Timestamp
            };
        }
    }
}
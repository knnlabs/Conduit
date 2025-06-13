using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Audio;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

using CsvHelper;
using CsvHelper.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminAudioUsageService"/> class.
        /// </summary>
        public AdminAudioUsageService(
            IAudioUsageLogRepository repository,
            IVirtualKeyRepository virtualKeyRepository,
            ILogger<AdminAudioUsageService> logger,
            IServiceProvider serviceProvider)
        {
            _repository = repository;
            _virtualKeyRepository = virtualKeyRepository;
            _logger = logger;
            _serviceProvider = serviceProvider;
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
        public async Task<Interfaces.AudioKeyUsageDto> GetUsageByKeyAsync(string virtualKey, DateTime? startDate = null, DateTime? endDate = null)
        {
            var logs = await _repository.GetByVirtualKeyAsync(virtualKey, startDate, endDate);
            var key = await _virtualKeyRepository.GetByKeyHashAsync(virtualKey);

            var effectiveStartDate = startDate ?? DateTime.UtcNow.AddDays(-30);
            var effectiveEndDate = endDate ?? DateTime.UtcNow;

            var operationBreakdown = await _repository.GetOperationBreakdownAsync(effectiveStartDate, effectiveEndDate, virtualKey);
            var providerBreakdown = await _repository.GetProviderBreakdownAsync(effectiveStartDate, effectiveEndDate, virtualKey);

            return new Interfaces.AudioKeyUsageDto
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
        public async Task<Interfaces.AudioProviderUsageDto> GetUsageByProviderAsync(string provider, DateTime? startDate = null, DateTime? endDate = null)
        {
            var logs = await _repository.GetByProviderAsync(provider, startDate, endDate);

            var effectiveStartDate = startDate ?? DateTime.UtcNow.AddDays(-30);
            var effectiveEndDate = endDate ?? DateTime.UtcNow;

            var operationBreakdown = await _repository.GetOperationBreakdownAsync(effectiveStartDate, effectiveEndDate);

            // Calculate daily trend
            var dailyTrend = logs
                .GroupBy(l => l.Timestamp.Date)
                .Select(g => new Interfaces.DailyUsageTrend
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

            return new Interfaces.AudioProviderUsageDto
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
            using var scope = _serviceProvider.CreateScope();
            var sessionStore = scope.ServiceProvider.GetService<IRealtimeSessionStore>();

            if (sessionStore == null)
            {
                _logger.LogWarning("Real-time session store not available");
                return new RealtimeSessionMetricsDto
                {
                    ActiveSessions = 0,
                    SessionsByProvider = new Dictionary<string, int>(),
                    AverageSessionDuration = 0,
                    TotalSessionTimeToday = 0,
                    TotalCostToday = 0,
                    PeakConcurrentSessions = 0,
                    SuccessRate = 100,
                    AverageTurnsPerSession = 0
                };
            }

            var sessions = await sessionStore.GetActiveSessionsAsync();
            var todaySessions = sessions.Where(s => s.CreatedAt.Date == DateTime.UtcNow.Date).ToList();

            // Calculate metrics
            var sessionsByProvider = sessions
                .GroupBy(s => s.Provider)
                .ToDictionary(g => g.Key, g => g.Count());

            var averageDuration = sessions.Any()
                ? sessions.Average(s => s.Statistics.Duration.TotalMinutes)
                : 0;

            var totalSessionTimeToday = todaySessions
                .Sum(s => s.Statistics.Duration.TotalMinutes);

            var successfulSessions = sessions.Count(s => s.Statistics.ErrorCount == 0);
            var successRate = sessions.Any()
                ? (successfulSessions / (double)sessions.Count) * 100
                : 100;

            var averageTurns = sessions.Any()
                ? sessions.Average(s => s.Statistics.TurnCount)
                : 0;

            // Calculate cost (simplified - would need proper cost calculation)
            var totalCostToday = todaySessions.Sum(s =>
            {
                var inputMinutes = s.Statistics.InputAudioDuration.TotalMinutes;
                var outputMinutes = s.Statistics.OutputAudioDuration.TotalMinutes;
                return (inputMinutes * 0.015) + (outputMinutes * 0.03); // Example rates
            });

            return new RealtimeSessionMetricsDto
            {
                ActiveSessions = sessions.Count,
                SessionsByProvider = sessionsByProvider,
                AverageSessionDuration = averageDuration,
                TotalSessionTimeToday = totalSessionTimeToday,
                TotalCostToday = (decimal)totalCostToday,
                PeakConcurrentSessions = sessions.Count, // Would need historical tracking
                SuccessRate = successRate,
                AverageTurnsPerSession = averageTurns
            };
        }

        /// <inheritdoc/>
        public async Task<List<RealtimeSessionDto>> GetActiveSessionsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var sessionStore = scope.ServiceProvider.GetService<IRealtimeSessionStore>();

            if (sessionStore == null)
            {
                _logger.LogWarning("Real-time session store not available");
                return new List<RealtimeSessionDto>();
            }

            var sessions = await sessionStore.GetActiveSessionsAsync();

            return sessions.Select(s => MapSessionToDto(s)).ToList();
        }

        /// <inheritdoc/>
        public async Task<RealtimeSessionDto?> GetSessionDetailsAsync(string sessionId)
        {
            using var scope = _serviceProvider.CreateScope();
            var sessionStore = scope.ServiceProvider.GetService<IRealtimeSessionStore>();

            if (sessionStore == null)
            {
                _logger.LogWarning("Real-time session store not available");
                return null;
            }

            var session = await sessionStore.GetSessionAsync(sessionId);

            return session != null ? MapSessionToDto(session) : null;
        }

        /// <inheritdoc/>
        public async Task<bool> TerminateSessionAsync(string sessionId)
        {
            using var scope = _serviceProvider.CreateScope();
            var sessionStore = scope.ServiceProvider.GetService<IRealtimeSessionStore>();

            if (sessionStore == null)
            {
                _logger.LogWarning("Real-time session store not available");
                return false;
            }

            var session = await sessionStore.GetSessionAsync(sessionId);
            if (session == null)
            {
                _logger.LogWarning("Session not found for termination {SessionId}", sessionId);
                return false;
            }

            // Update session state to closed
            session.State = SessionState.Closed;
            session.Statistics.Duration = DateTime.UtcNow - session.CreatedAt;

            await sessionStore.UpdateSessionAsync(session);

            // Remove from active sessions
            var removed = await sessionStore.RemoveSessionAsync(sessionId);

            if (removed)
            {
                _logger.LogInformation("Successfully terminated session {SessionId}", sessionId);
            }

            return removed;
        }

        /// <inheritdoc/>
        public async Task<string> ExportUsageDataAsync(AudioUsageQueryDto query, string format)
        {
            // Get all logs without pagination for export
            query.Page = 1;
            query.PageSize = int.MaxValue;
            var result = await _repository.GetPagedAsync(query);
            var logs = result.Items;

            format = format?.ToLowerInvariant() ?? "csv";

            return format switch
            {
                "csv" => await GenerateCsvExport(logs),
                "json" => await GenerateJsonExport(logs),
                _ => throw new ArgumentException("Unsupported export format", nameof(format))
            };
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

        private static RealtimeSessionDto MapSessionToDto(RealtimeSession session)
        {
            return new RealtimeSessionDto
            {
                SessionId = session.Id,
                VirtualKey = session.Metadata?.GetValueOrDefault("VirtualKey")?.ToString() ?? "unknown",
                Provider = session.Provider,
                State = session.State.ToString(),
                CreatedAt = session.CreatedAt,
                DurationSeconds = session.Statistics.Duration.TotalSeconds,
                TurnCount = session.Statistics.TurnCount,
                InputTokens = session.Statistics.InputTokens ?? 0,
                OutputTokens = session.Statistics.OutputTokens ?? 0,
                EstimatedCost = (decimal)CalculateSessionCost(session),
                IpAddress = session.Metadata?.GetValueOrDefault("IpAddress")?.ToString(),
                UserAgent = session.Metadata?.GetValueOrDefault("UserAgent")?.ToString(),
                Model = session.Config?.Model,
                Voice = session.Config?.Voice,
                Language = session.Config?.Language
            };
        }

        private static double CalculateSessionCost(RealtimeSession session)
        {
            // Simple cost calculation - should use actual provider rates
            var inputMinutes = session.Statistics.InputAudioDuration.TotalMinutes;
            var outputMinutes = session.Statistics.OutputAudioDuration.TotalMinutes;

            return session.Provider.ToLowerInvariant() switch
            {
                "openai" => (inputMinutes * 0.015) + (outputMinutes * 0.03),
                "ultravox" => (inputMinutes * 0.01) + (outputMinutes * 0.02),
                _ => (inputMinutes * 0.01) + (outputMinutes * 0.01)
            };
        }

        private async Task<string> GenerateCsvExport(List<Configuration.Entities.AudioUsageLog> logs)
        {
            using var stringWriter = new StringWriter();
            using var csv = new CsvWriter(stringWriter, CultureInfo.InvariantCulture);
            
            // Write header
            csv.WriteField("Timestamp");
            csv.WriteField("VirtualKey");
            csv.WriteField("Provider");
            csv.WriteField("Operation");
            csv.WriteField("Model");
            csv.WriteField("Duration");
            csv.WriteField("Cost");
            csv.WriteField("Status");
            csv.WriteField("Language");
            csv.WriteField("Voice");
            await csv.NextRecordAsync();

            // Write data
            foreach (var log in logs.OrderBy(l => l.Timestamp))
            {
                csv.WriteField(log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                csv.WriteField(log.VirtualKey);
                csv.WriteField(log.Provider);
                csv.WriteField(log.OperationType);
                csv.WriteField(log.Model);
                csv.WriteField(log.DurationSeconds);
                csv.WriteField(log.Cost.ToString("F4"));
                csv.WriteField(log.StatusCode);
                csv.WriteField(log.Language ?? "N/A");
                csv.WriteField(log.Voice ?? "N/A");
                await csv.NextRecordAsync();
            }

            await csv.FlushAsync();
            return stringWriter.ToString();
        }

        private async Task<string> GenerateJsonExport(List<Configuration.Entities.AudioUsageLog> logs)
        {
            var exportData = logs.OrderBy(l => l.Timestamp).Select(l => new
            {
                timestamp = l.Timestamp,
                virtualKey = l.VirtualKey,
                provider = l.Provider,
                operation = l.OperationType,
                model = l.Model,
                duration = l.DurationSeconds,
                cost = l.Cost,
                status = l.StatusCode,
                language = l.Language,
                voice = l.Voice,
                error = l.ErrorMessage
            });

            return await Task.FromResult(System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
    }
}

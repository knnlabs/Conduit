using System.Globalization;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Audio;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

using CsvHelper;

using ConduitLLM.Configuration.Interfaces;
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
        private readonly ConduitLLM.Core.Interfaces.ICostCalculationService _costCalculationService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminAudioUsageService"/> class.
        /// </summary>
        public AdminAudioUsageService(
            IAudioUsageLogRepository repository,
            IVirtualKeyRepository virtualKeyRepository,
            ILogger<AdminAudioUsageService> logger,
            IServiceProvider serviceProvider,
            ConduitLLM.Core.Interfaces.ICostCalculationService costCalculationService)
        {
            _repository = repository;
            _virtualKeyRepository = virtualKeyRepository;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _costCalculationService = costCalculationService;
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
        public async Task<AudioUsageSummaryDto> GetUsageSummaryAsync(DateTime startDate, DateTime endDate, string? virtualKey = null, int? providerId = null)
        {
            return await _repository.GetUsageSummaryAsync(startDate, endDate, virtualKey, providerId);
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
                KeyName = key?.KeyName ?? string.Empty,
                TotalOperations = logs.Count(),
                TotalCost = logs.Sum(l => l.Cost),
                TotalDurationSeconds = logs.Where(l => l.DurationSeconds.HasValue).Sum(l => l.DurationSeconds!.Value),
                LastUsed = logs.OrderByDescending(l => l.Timestamp).FirstOrDefault()?.Timestamp,
                SuccessRate = logs.Count() > 0 ? (logs.Count(l => l.StatusCode == null || (l.StatusCode >= 200 && l.StatusCode < 300)) / (double)logs.Count()) * 100 : 100
            };
        }

        /// <inheritdoc/>
        public async Task<AudioProviderUsageDto> GetUsageByProviderAsync(int providerId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var logs = await _repository.GetByProviderAsync(providerId, startDate, endDate);

            var successCount = logs.Count(l => l.StatusCode == null || (l.StatusCode >= 200 && l.StatusCode < 300));
            var totalDuration = logs.Where(l => l.DurationSeconds.HasValue).Sum(l => l.DurationSeconds!.Value);
            var avgResponseTime = logs.Count() > 0 ? (totalDuration / logs.Count()) * 1000 : 0; // Convert to ms

            // Count operations by type
            var transcriptionCount = logs.Count(l => l.OperationType?.ToLower() == "transcription");
            var ttsCount = logs.Count(l => l.OperationType?.ToLower() == "tts" || l.OperationType?.ToLower() == "text-to-speech");
            var realtimeCount = logs.Count(l => l.OperationType?.ToLower() == "realtime");

            // Find most used model
            var mostUsedModel = logs
                .Where(l => !string.IsNullOrEmpty(l.Model))
                .GroupBy(l => l.Model)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;

            // Get provider name from first log or use provider ID
            var providerName = logs.FirstOrDefault()?.Provider?.ProviderName ?? $"Provider {providerId}";

            return new AudioProviderUsageDto
            {
                ProviderId = providerId,
                ProviderName = providerName,
                TotalOperations = logs.Count,
                TranscriptionCount = transcriptionCount,
                TextToSpeechCount = ttsCount,
                RealtimeSessionCount = realtimeCount,
                TotalCost = logs.Sum(l => l.Cost),
                AverageResponseTime = avgResponseTime,
                SuccessRate = logs.Count() > 0 ? (successCount / (double)logs.Count) * 100 : 0,
                MostUsedModel = mostUsedModel
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

            var averageDuration = sessions.Count() > 0
                ? sessions.Average(s => s.Statistics.Duration.TotalMinutes)
                : 0;

            var totalSessionTimeToday = todaySessions
                .Sum(s => s.Statistics.Duration.TotalMinutes);

            var successfulSessions = sessions.Count(s => s.Statistics.ErrorCount == 0);
            var successRate = sessions.Count() > 0
                ? (successfulSessions / (double)sessions.Count) * 100
                : 100;

            var averageTurns = sessions.Count() > 0
                ? sessions.Average(s => s.Statistics.TurnCount)
                : 0;

            // Calculate cost using actual model costs from database
            var totalCostToday = await CalculateTotalSessionsCostAsync(todaySessions);

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

            var mappedSessions = new List<RealtimeSessionDto>();
            foreach (var session in sessions)
            {
                mappedSessions.Add(await MapSessionToDtoAsync(session));
            }
            return mappedSessions;
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

            return session != null ? await MapSessionToDtoAsync(session) : null;
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
                _logger.LogWarning("Session not found for termination {SessionId}", sessionId.Replace(Environment.NewLine, ""));
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
                _logger.LogInformation("Successfully terminated session {SessionId}", sessionId.Replace(Environment.NewLine, ""));
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
                ProviderId = log.ProviderId,
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

        private async Task<RealtimeSessionDto> MapSessionToDtoAsync(RealtimeSession session)
        {
            // Try to get ProviderId from metadata
            var providerId = 0;
            if (session.Metadata?.TryGetValue("ProviderId", out var idValue) == true && idValue != null)
            {
                int.TryParse(idValue.ToString(), out providerId);
            }
            
            return new RealtimeSessionDto
            {
                SessionId = session.Id,
                VirtualKey = session.Metadata?.GetValueOrDefault("VirtualKey")?.ToString() ?? "unknown",
                ProviderId = providerId,
                ProviderName = session.Provider,
                State = session.State.ToString(),
                CreatedAt = session.CreatedAt,
                DurationSeconds = session.Statistics.Duration.TotalSeconds,
                TurnCount = session.Statistics.TurnCount,
                InputTokens = session.Statistics.InputTokens ?? 0,
                OutputTokens = session.Statistics.OutputTokens ?? 0,
                EstimatedCost = (decimal)await CalculateSessionCostAsync(session),
                IpAddress = session.Metadata?.GetValueOrDefault("IpAddress")?.ToString(),
                UserAgent = session.Metadata?.GetValueOrDefault("UserAgent")?.ToString(),
                Model = session.Config?.Model,
                Voice = session.Config?.Voice,
                Language = session.Config?.Language
            };
        }

        private async Task<double> CalculateSessionCostAsync(RealtimeSession session)
        {
            // TODO: ICostCalculationService needs to be enhanced to support separate input/output audio durations
            // For now, we'll use total audio duration and log a warning about the limitation
            var totalAudioSeconds = (decimal)(session.Statistics.InputAudioDuration.TotalSeconds + 
                                              session.Statistics.OutputAudioDuration.TotalSeconds);
            
            if (string.IsNullOrEmpty(session.Config?.Model))
            {
                _logger.LogWarning("No model specified for realtime session {SessionId}, cannot calculate cost", 
                    session.Id);
                return 0;
            }

            var usage = new ConduitLLM.Core.Models.Usage
            {
                AudioDurationSeconds = totalAudioSeconds
            };

            try
            {
                var cost = await _costCalculationService.CalculateCostAsync(session.Config.Model, usage);
                return (double)cost;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate cost for session {SessionId} with model {Model}",
                    session.Id, session.Config.Model);
                return 0;
            }
        }
        
        private async Task<double> CalculateTotalSessionsCostAsync(IEnumerable<RealtimeSession> sessions)
        {
            var totalCost = 0.0;
            foreach (var session in sessions)
            {
                totalCost += await CalculateSessionCostAsync(session);
            }
            return totalCost;
        }

        private async Task<string> GenerateCsvExport(List<Configuration.Entities.AudioUsageLog> logs)
        {
            using var stringWriter = new StringWriter();
            using var csv = new CsvWriter(stringWriter, CultureInfo.InvariantCulture);
            
            // Write header
            csv.WriteField("Timestamp");
            csv.WriteField("VirtualKey");
            csv.WriteField("ProviderId");
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
                csv.WriteField(log.ProviderId);
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
                providerId = l.ProviderId,
                providerName = l.Provider?.ProviderName,
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

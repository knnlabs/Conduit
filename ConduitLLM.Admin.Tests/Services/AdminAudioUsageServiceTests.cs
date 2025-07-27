using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Audio;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Admin.Tests.Services
{
    /// <summary>
    /// Unit tests for the AdminAudioUsageService class.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "AudioUsage")]
    public class AdminAudioUsageServiceTests
    {
        private readonly Mock<IAudioUsageLogRepository> _mockRepository;
        private readonly Mock<IVirtualKeyRepository> _mockVirtualKeyRepository;
        private readonly Mock<ILogger<AdminAudioUsageService>> _mockLogger;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<IRealtimeSessionStore> _mockSessionStore;
        private readonly AdminAudioUsageService _service;
        private readonly ITestOutputHelper _output;

        public AdminAudioUsageServiceTests(ITestOutputHelper output)
        {
            _output = output;
            _mockRepository = new Mock<IAudioUsageLogRepository>();
            _mockVirtualKeyRepository = new Mock<IVirtualKeyRepository>();
            _mockLogger = new Mock<ILogger<AdminAudioUsageService>>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockSessionStore = new Mock<IRealtimeSessionStore>();
            
            // Setup service provider to return session store
            var mockScope = new Mock<IServiceScope>();
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockScopedProvider = new Mock<IServiceProvider>();
            
            mockScope.Setup(x => x.ServiceProvider).Returns(mockScopedProvider.Object);
            mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);
            _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(mockScopeFactory.Object);
            mockScopedProvider.Setup(x => x.GetService(typeof(IRealtimeSessionStore))).Returns(_mockSessionStore.Object);

            _service = new AdminAudioUsageService(
                _mockRepository.Object,
                _mockVirtualKeyRepository.Object,
                _mockLogger.Object,
                _mockServiceProvider.Object);
        }

        #region GetUsageLogsAsync Tests

        [Fact]
        public async Task GetUsageLogsAsync_WithValidQuery_ShouldReturnPagedResults()
        {
            // Arrange
            var query = new AudioUsageQueryDto
            {
                Page = 1,
                PageSize = 10,
                ProviderType = ProviderType.OpenAI
            };

            var logs = CreateSampleAudioUsageLogs(15);
            var pagedResult = new PagedResult<AudioUsageLog>
            {
                Items = logs.Take(10).ToList(),
                TotalCount = 15,
                Page = 1,
                PageSize = 10,
                TotalPages = 2
            };

            _mockRepository.Setup(x => x.GetPagedAsync(query))
                .ReturnsAsync(pagedResult);

            // Act
            var result = await _service.GetUsageLogsAsync(query);

            // Assert
            result.Should().NotBeNull();
            result.Items.Should().HaveCount(10);
            result.TotalCount.Should().Be(15);
            result.TotalPages.Should().Be(2);
            result.Items.First().ProviderType.Should().Be(ProviderType.OpenAI);
        }

        [Fact]
        public async Task GetUsageLogsAsync_WithDateRange_ShouldFilterResults()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;
            
            var query = new AudioUsageQueryDto
            {
                Page = 1,
                PageSize = 10,
                StartDate = startDate,
                EndDate = endDate
            };

            var logs = CreateSampleAudioUsageLogs(5);
            var pagedResult = new PagedResult<AudioUsageLog>
            {
                Items = logs,
                TotalCount = 5,
                Page = 1,
                PageSize = 10,
                TotalPages = 1
            };

            _mockRepository.Setup(x => x.GetPagedAsync(It.Is<AudioUsageQueryDto>(q => 
                q.StartDate == startDate && q.EndDate == endDate)))
                .ReturnsAsync(pagedResult);

            // Act
            var result = await _service.GetUsageLogsAsync(query);

            // Assert
            result.Items.Should().HaveCount(5);
            _mockRepository.Verify(x => x.GetPagedAsync(It.Is<AudioUsageQueryDto>(q => 
                q.StartDate == startDate && q.EndDate == endDate)), Times.Once);
        }

        #endregion

        #region GetUsageSummaryAsync Tests

        [Fact]
        public async Task GetUsageSummaryAsync_WithValidParameters_ShouldReturnSummary()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-30);
            var endDate = DateTime.UtcNow;
            var expectedSummary = new AudioUsageSummaryDto
            {
                TotalOperations = 100,
                TotalCost = 50.5m,
                TotalDurationSeconds = 3600,
                SuccessfulOperations = 95,
                FailedOperations = 5,
                TotalCharacters = 10000,
                TotalInputTokens = 5000,
                TotalOutputTokens = 4000
            };

            _mockRepository.Setup(x => x.GetUsageSummaryAsync(startDate, endDate, It.IsAny<string?>(), It.IsAny<ProviderType?>()))
                .ReturnsAsync(expectedSummary);

            // Act
            var result = await _service.GetUsageSummaryAsync(startDate, endDate);

            // Assert
            result.Should().BeEquivalentTo(expectedSummary);
        }

        [Fact]
        public async Task GetUsageSummaryAsync_WithVirtualKeyFilter_ShouldReturnFilteredSummary()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;
            var virtualKey = "test-key-hash";
            
            var expectedSummary = new AudioUsageSummaryDto
            {
                TotalOperations = 20,
                TotalCost = 10.5m,
                SuccessfulOperations = 20,
                FailedOperations = 0
            };

            _mockRepository.Setup(x => x.GetUsageSummaryAsync(startDate, endDate, virtualKey, It.IsAny<ProviderType?>()))
                .ReturnsAsync(expectedSummary);

            // Act
            var result = await _service.GetUsageSummaryAsync(startDate, endDate, virtualKey);

            // Assert
            result.TotalOperations.Should().Be(20);
            result.TotalCost.Should().Be(10.5m);
        }

        #endregion

        #region GetUsageByKeyAsync Tests

        [Fact]
        public async Task GetUsageByKeyAsync_WithValidKey_ShouldReturnKeyUsage()
        {
            // Arrange
            var virtualKey = "test-key-hash";
            var logs = CreateSampleAudioUsageLogs(10);
            var key = new VirtualKey
            {
                KeyHash = virtualKey,
                KeyName = "Test API Key"
            };

            _mockRepository.Setup(x => x.GetByVirtualKeyAsync(virtualKey, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(logs);
            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(virtualKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(key);
            _mockRepository.Setup(x => x.GetOperationBreakdownAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), virtualKey))
                .ReturnsAsync(new List<OperationTypeBreakdown> 
                { 
                    new() { OperationType = "transcription", Count = 6, TotalCost = 3.0m }, 
                    new() { OperationType = "tts", Count = 4, TotalCost = 2.0m } 
                });
            _mockRepository.Setup(x => x.GetProviderBreakdownAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), virtualKey))
                .ReturnsAsync(new List<ProviderBreakdown> 
                { 
                    new() { ProviderType = ProviderType.OpenAI, Count = 10, TotalCost = 5.0m, SuccessRate = 100 } 
                });

            // Act
            var result = await _service.GetUsageByKeyAsync(virtualKey);

            // Assert
            result.Should().NotBeNull();
            result.VirtualKey.Should().Be(virtualKey);
            result.KeyName.Should().Be("Test API Key");
            result.TotalOperations.Should().Be(10);
            result.TotalCost.Should().Be(logs.Sum(l => l.Cost));
            result.SuccessRate.Should().Be(90); // 9 out of 10 logs are successful (one has status 500)
        }

        [Fact]
        public async Task GetUsageByKeyAsync_WithDateRange_ShouldFilterResults()
        {
            // Arrange
            var virtualKey = "test-key-hash";
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;
            var logs = CreateSampleAudioUsageLogs(5);

            _mockRepository.Setup(x => x.GetByVirtualKeyAsync(virtualKey, startDate, endDate))
                .ReturnsAsync(logs);
            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(virtualKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync((VirtualKey?)null);

            // Act
            var result = await _service.GetUsageByKeyAsync(virtualKey, startDate, endDate);

            // Assert
            result.TotalOperations.Should().Be(5);
            result.KeyName.Should().BeEmpty();
        }

        #endregion

        #region GetUsageByProviderAsync Tests

        [Fact]
        public async Task GetUsageByProviderAsync_WithValidProvider_ShouldReturnProviderUsage()
        {
            // Arrange
            var provider = "openai";
            var logs = new List<AudioUsageLog>
            {
                CreateAudioUsageLog("transcription", "whisper-1", 200),
                CreateAudioUsageLog("tts", "tts-1", 200),
                CreateAudioUsageLog("realtime", "gpt-4o-realtime", 200),
                CreateAudioUsageLog("transcription", "whisper-1", 500) // Failed request
            };

            _mockRepository.Setup(x => x.GetByProviderAsync(ProviderType.OpenAI, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(logs);

            // Act
            var result = await _service.GetUsageByProviderAsync(provider);

            // Assert
            result.Should().NotBeNull();
            result.ProviderType.Should().Be(ProviderType.OpenAI);
            result.TotalOperations.Should().Be(4);
            result.TranscriptionCount.Should().Be(2);
            result.TextToSpeechCount.Should().Be(1);
            result.RealtimeSessionCount.Should().Be(1);
            result.SuccessRate.Should().Be(75); // 3 successful out of 4
            result.MostUsedModel.Should().Be("whisper-1");
        }

        [Fact]
        public async Task GetUsageByProviderAsync_WithNoLogs_ShouldReturnZeroMetrics()
        {
            // Arrange
            var provider = "AzureOpenAI";
            _mockRepository.Setup(x => x.GetByProviderAsync(ProviderType.AzureOpenAI, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(new List<AudioUsageLog>());

            // Act
            var result = await _service.GetUsageByProviderAsync(provider);

            // Assert
            result.TotalOperations.Should().Be(0);
            result.SuccessRate.Should().Be(0);
            result.MostUsedModel.Should().BeNull();
        }

        #endregion

        #region Realtime Session Tests

        [Fact]
        public async Task GetRealtimeSessionMetricsAsync_WithActiveSessions_ShouldReturnMetrics()
        {
            // Arrange
            var sessions = CreateSampleRealtimeSessions(5);
            _mockSessionStore.Setup(x => x.GetActiveSessionsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(sessions);

            // Act
            var result = await _service.GetRealtimeSessionMetricsAsync();

            // Assert
            result.Should().NotBeNull();
            result.ActiveSessions.Should().Be(5);
            result.SessionsByProvider.Should().ContainKey("openai");
            result.SessionsByProvider["openai"].Should().Be(3);
            result.SessionsByProvider["ultravox"].Should().Be(2);
            result.SuccessRate.Should().Be(80); // 4 successful out of 5
            result.AverageTurnsPerSession.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task GetRealtimeSessionMetricsAsync_WithNoSessionStore_ShouldReturnEmptyMetrics()
        {
            // Arrange
            var mockScopedProvider = new Mock<IServiceProvider>();
            mockScopedProvider.Setup(x => x.GetService(typeof(IRealtimeSessionStore)))
                .Returns(null);
            
            var mockScope = new Mock<IServiceScope>();
            mockScope.Setup(x => x.ServiceProvider).Returns(mockScopedProvider.Object);
            
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);
            
            _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
                .Returns(mockScopeFactory.Object);

            // Act
            var result = await _service.GetRealtimeSessionMetricsAsync();

            // Assert
            result.ActiveSessions.Should().Be(0);
            result.SessionsByProvider.Should().BeEmpty();
            result.SuccessRate.Should().Be(100);
        }

        [Fact]
        public async Task GetActiveSessionsAsync_ShouldReturnSessionDtos()
        {
            // Arrange
            var sessions = CreateSampleRealtimeSessions(3);
            _mockSessionStore.Setup(x => x.GetActiveSessionsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(sessions);

            // Act
            var result = await _service.GetActiveSessionsAsync();

            // Assert
            result.Should().HaveCount(3);
            result.First().SessionId.Should().Be("session-1");
            result.First().ProviderType.Should().Be(ProviderType.Ultravox); // First session (i=0) is ultravox based on CreateSampleRealtimeSessions logic
            result.First().State.Should().Be(SessionState.Connected.ToString());
        }

        [Fact]
        public async Task GetSessionDetailsAsync_WithValidSessionId_ShouldReturnSession()
        {
            // Arrange
            var sessionId = "session-123";
            var session = CreateRealtimeSession(sessionId, "openai");
            
            _mockSessionStore.Setup(x => x.GetSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);

            // Act
            var result = await _service.GetSessionDetailsAsync(sessionId);

            // Assert
            result.Should().NotBeNull();
            result!.SessionId.Should().Be(sessionId);
            result.ProviderType.Should().Be(ProviderType.OpenAI);
        }

        [Fact]
        public async Task GetSessionDetailsAsync_WithInvalidSessionId_ShouldReturnNull()
        {
            // Arrange
            var sessionId = "non-existent";
            _mockSessionStore.Setup(x => x.GetSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((RealtimeSession?)null);

            // Act
            var result = await _service.GetSessionDetailsAsync(sessionId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task TerminateSessionAsync_WithValidSession_ShouldTerminate()
        {
            // Arrange
            var sessionId = "session-to-terminate";
            var session = CreateRealtimeSession(sessionId, "openai");
            
            _mockSessionStore.Setup(x => x.GetSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);
            _mockSessionStore.Setup(x => x.UpdateSessionAsync(It.IsAny<RealtimeSession>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockSessionStore.Setup(x => x.RemoveSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.TerminateSessionAsync(sessionId);

            // Assert
            result.Should().BeTrue();
            _mockSessionStore.Verify(x => x.UpdateSessionAsync(It.Is<RealtimeSession>(s => 
                s.State == SessionState.Closed), It.IsAny<CancellationToken>()), Times.Once);
            _mockSessionStore.Verify(x => x.RemoveSessionAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TerminateSessionAsync_WithNonExistentSession_ShouldReturnFalse()
        {
            // Arrange
            var sessionId = "non-existent";
            _mockSessionStore.Setup(x => x.GetSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((RealtimeSession?)null);

            // Act
            var result = await _service.TerminateSessionAsync(sessionId);

            // Assert
            result.Should().BeFalse();
            _mockSessionStore.Verify(x => x.RemoveSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region Export Tests

        [Fact]
        public async Task ExportUsageDataAsync_AsCsv_ShouldReturnCsvData()
        {
            // Arrange
            var query = new AudioUsageQueryDto { Page = 1, PageSize = 10 };
            var logs = CreateSampleAudioUsageLogs(3);
            var pagedResult = new PagedResult<AudioUsageLog>
            {
                Items = logs,
                TotalCount = 3,
                Page = 1,
                PageSize = int.MaxValue,
                TotalPages = 1
            };

            _mockRepository.Setup(x => x.GetPagedAsync(It.IsAny<AudioUsageQueryDto>()))
                .ReturnsAsync(pagedResult);

            // Act
            var result = await _service.ExportUsageDataAsync(query, "csv");

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("Timestamp");
            result.Should().Contain("VirtualKey");
            result.Should().Contain("Provider");
            result.Should().Contain("OpenAI"); // ProviderType enum outputs PascalCase names
        }

        [Fact]
        public async Task ExportUsageDataAsync_AsJson_ShouldReturnJsonData()
        {
            // Arrange
            var query = new AudioUsageQueryDto { Page = 1, PageSize = 10 };
            var logs = CreateSampleAudioUsageLogs(2);
            var pagedResult = new PagedResult<AudioUsageLog>
            {
                Items = logs,
                TotalCount = 2,
                Page = 1,
                PageSize = int.MaxValue,
                TotalPages = 1
            };

            _mockRepository.Setup(x => x.GetPagedAsync(It.IsAny<AudioUsageQueryDto>()))
                .ReturnsAsync(pagedResult);

            // Act
            var result = await _service.ExportUsageDataAsync(query, "json");

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("\"virtualKey\"");
            result.Should().Contain("\"provider\"");
            result.Should().Contain("\"provider\": 1"); // ProviderType.OpenAI = 1 in JSON (with space)
            
            // Should be valid JSON
            var json = System.Text.Json.JsonDocument.Parse(result);
            json.RootElement.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        }

        [Fact]
        public async Task ExportUsageDataAsync_WithUnsupportedFormat_ShouldThrowException()
        {
            // Arrange
            var query = new AudioUsageQueryDto { Page = 1, PageSize = 10 };
            var logs = CreateSampleAudioUsageLogs(3);
            var pagedResult = new PagedResult<AudioUsageLog>
            {
                Items = logs,
                TotalCount = 3,
                Page = 1,
                PageSize = int.MaxValue,
                TotalPages = 1
            };

            _mockRepository.Setup(x => x.GetPagedAsync(It.IsAny<AudioUsageQueryDto>()))
                .ReturnsAsync(pagedResult);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.ExportUsageDataAsync(query, "xml"));
        }

        #endregion

        #region Cleanup Tests

        [Fact]
        public async Task CleanupOldLogsAsync_ShouldDeleteOldLogs()
        {
            // Arrange
            var retentionDays = 30;
            var expectedCutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            var deletedCount = 100;

            _mockRepository.Setup(x => x.DeleteOldLogsAsync(It.Is<DateTime>(d => 
                d.Date == expectedCutoffDate.Date)))
                .ReturnsAsync(deletedCount);

            // Act
            var result = await _service.CleanupOldLogsAsync(retentionDays);

            // Assert
            result.Should().Be(deletedCount);
            _mockRepository.Verify(x => x.DeleteOldLogsAsync(It.IsAny<DateTime>()), Times.Once);
        }

        #endregion

        #region Helper Methods

        private List<AudioUsageLog> CreateSampleAudioUsageLogs(int count)
        {
            var logs = new List<AudioUsageLog>();
            for (int i = 0; i < count; i++)
            {
                logs.Add(new AudioUsageLog
                {
                    Id = i + 1,
                    VirtualKey = $"key-{i % 3}",
                    Provider = i % 2 == 0 ? ProviderType.OpenAI : ProviderType.AzureOpenAI,
                    OperationType = i % 3 == 0 ? "transcription" : i % 3 == 1 ? "tts" : "realtime",
                    Model = i % 2 == 0 ? "whisper-1" : "tts-1",
                    RequestId = Guid.NewGuid().ToString(),
                    DurationSeconds = 10 + i,
                    Cost = 0.05m + (i * 0.01m),
                    StatusCode = i % 10 == 0 ? 500 : 200,
                    Timestamp = DateTime.UtcNow.AddHours(-i)
                });
            }
            return logs;
        }

        private AudioUsageLog CreateAudioUsageLog(string operationType, string model, int statusCode)
        {
            return new AudioUsageLog
            {
                Id = 1,
                VirtualKey = "test-key",
                Provider = ProviderType.OpenAI,
                OperationType = operationType,
                Model = model,
                RequestId = Guid.NewGuid().ToString(),
                DurationSeconds = 5,
                Cost = 0.10m,
                StatusCode = statusCode,
                Timestamp = DateTime.UtcNow
            };
        }

        private List<RealtimeSession> CreateSampleRealtimeSessions(int count)
        {
            var sessions = new List<RealtimeSession>();
            for (int i = 0; i < count; i++)
            {
                sessions.Add(CreateRealtimeSession($"session-{i + 1}", i % 3 == 0 ? "ultravox" : "openai", i == 0));
            }
            return sessions;
        }

        private RealtimeSession CreateRealtimeSession(string sessionId, string provider, bool hasErrors = false)
        {
            var config = new RealtimeSessionConfig
            {
                Model = provider == "openai" ? "gpt-4o-realtime" : "ultravox-v0.2",
                Voice = "alloy",
                Language = "en-US"
            };

            var session = new RealtimeSession
            {
                Id = sessionId,
                Provider = provider,
                Config = config,
                State = SessionState.Connected,
                CreatedAt = DateTime.UtcNow.AddMinutes(-30),
                Metadata = new Dictionary<string, object>
                {
                    { "VirtualKey", "test-key-hash" },
                    { "IpAddress", "192.168.1.1" },
                    { "UserAgent", "Mozilla/5.0" }
                }
            };

            session.Statistics.Duration = TimeSpan.FromMinutes(25);
            session.Statistics.TurnCount = 10;
            session.Statistics.InputTokens = 1000;
            session.Statistics.OutputTokens = 2000;
            session.Statistics.InputAudioDuration = TimeSpan.FromMinutes(5);
            session.Statistics.OutputAudioDuration = TimeSpan.FromMinutes(10);
            session.Statistics.ErrorCount = hasErrors ? 2 : 0;

            return session;
        }

        #endregion
    }
}
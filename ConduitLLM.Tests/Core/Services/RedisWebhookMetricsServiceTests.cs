using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;
using ConduitLLM.Core.Services;
using ConduitLLM.Configuration.DTOs.SignalR;

namespace ConduitLLM.Tests.Core.Services
{
    public class RedisWebhookMetricsServiceTests : IDisposable
    {
        private readonly Mock<IConnectionMultiplexer> _redisMock;
        private readonly Mock<IDatabase> _databaseMock;
        private readonly Mock<ITransaction> _transactionMock;
        private readonly Mock<IServer> _serverMock;
        private readonly Mock<ILogger<RedisWebhookMetricsService>> _loggerMock;
        private readonly RedisWebhookMetricsService _metricsService;
        
        public RedisWebhookMetricsServiceTests()
        {
            _redisMock = new Mock<IConnectionMultiplexer>();
            _databaseMock = new Mock<IDatabase>();
            _transactionMock = new Mock<ITransaction>();
            _serverMock = new Mock<IServer>();
            _loggerMock = new Mock<ILogger<RedisWebhookMetricsService>>();
            
            _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_databaseMock.Object);
            _redisMock.Setup(r => r.GetEndPoints(It.IsAny<bool>())).Returns(new[] { new System.Net.IPEndPoint(0, 0) });
            _redisMock.Setup(r => r.GetServer(It.IsAny<System.Net.EndPoint>(), It.IsAny<object>())).Returns(_serverMock.Object);
            
            _databaseMock.Setup(d => d.CreateTransaction(It.IsAny<object>())).Returns(_transactionMock.Object);
            _transactionMock.Setup(t => t.ExecuteAsync(It.IsAny<CommandFlags>())).ReturnsAsync(true);
            
            _metricsService = new RedisWebhookMetricsService(_redisMock.Object, _loggerMock.Object);
        }
        
        [Fact]
        public async Task RecordAttemptAsync_IncrementsAttemptCounter()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var taskId = "task-123";
            var taskType = "test";
            var eventType = "completion";
            
            // Act
            await _metricsService.RecordAttemptAsync(webhookUrl, taskId, taskType, eventType);
            
            // Assert
            _transactionMock.Verify(t => t.HashIncrementAsync(
                It.Is<RedisKey>(k => k.ToString()!.Contains("webhook:metrics:urls")),
                "total_attempts",
                1,
                It.IsAny<CommandFlags>()), Times.Once);
            
            _transactionMock.Verify(t => t.HashSetAsync(
                It.Is<RedisKey>(k => k.ToString()!.Contains("webhook:metrics:urls")),
                "last_attempt",
                It.IsAny<RedisValue>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()), Times.Once);
        }
        
        [Fact]
        public async Task RecordSuccessAsync_UpdatesSuccessMetrics()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var taskId = "task-123";
            var responseTimeMs = 250L;
            
            // Act
            await _metricsService.RecordSuccessAsync(webhookUrl, taskId, responseTimeMs);
            
            // Assert
            _transactionMock.Verify(t => t.HashIncrementAsync(
                It.Is<RedisKey>(k => k.ToString()!.Contains("webhook:metrics:urls")),
                "successes",
                1,
                It.IsAny<CommandFlags>()), Times.Once);
            
            _transactionMock.Verify(t => t.SortedSetAddAsync(
                It.Is<RedisKey>(k => k.ToString()!.Contains("webhook:metrics:response")),
                It.IsAny<RedisValue>(),
                responseTimeMs,
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()), Times.Once);
            
            _transactionMock.Verify(t => t.HashIncrementAsync(
                It.Is<RedisKey>(k => k.ToString()!.Contains("webhook:metrics:urls")),
                "total_response_time",
                responseTimeMs,
                It.IsAny<CommandFlags>()), Times.Once);
        }
        
        [Fact]
        public async Task RecordFailureAsync_UpdatesFailureMetrics()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var taskId = "task-123";
            var isPermanent = false;
            
            // Act
            await _metricsService.RecordFailureAsync(webhookUrl, taskId, isPermanent);
            
            // Assert
            _transactionMock.Verify(t => t.HashIncrementAsync(
                It.Is<RedisKey>(k => k.ToString()!.Contains("webhook:metrics:urls")),
                "failures",
                1,
                It.IsAny<CommandFlags>()), Times.Once);
            
            _transactionMock.Verify(t => t.HashIncrementAsync(
                It.Is<RedisKey>(k => k.ToString()!.Contains("webhook:metrics:urls")),
                "pending_retries",
                1,
                It.IsAny<CommandFlags>()), Times.Once);
        }
        
        [Fact]
        public async Task RecordFailureAsync_PermanentFailure_UpdatesPermanentFailures()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var taskId = "task-123";
            var isPermanent = true;
            
            // Act
            await _metricsService.RecordFailureAsync(webhookUrl, taskId, isPermanent);
            
            // Assert
            _transactionMock.Verify(t => t.HashIncrementAsync(
                It.Is<RedisKey>(k => k.ToString()!.Contains("webhook:metrics:urls")),
                "permanent_failures",
                1,
                It.IsAny<CommandFlags>()), Times.Once);
            
            _transactionMock.Verify(t => t.HashIncrementAsync(
                It.Is<RedisKey>(k => k.ToString()!.Contains("webhook:metrics:urls")),
                "pending_retries",
                It.IsAny<long>(),
                It.IsAny<CommandFlags>()), Times.Never);
        }
        
        [Fact]
        public async Task GetUrlStatisticsAsync_ReturnsCorrectStatistics()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var hashEntries = new HashEntry[]
            {
                new HashEntry("url", webhookUrl),
                new HashEntry("total_attempts", 100),
                new HashEntry("successes", 95),
                new HashEntry("failures", 5),
                new HashEntry("pending_retries", 2),
                new HashEntry("response_count", 95),
                new HashEntry("total_response_time", 23750) // 250ms avg
            };
            
            _databaseMock.Setup(d => d.HashGetAllAsync(
                It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(hashEntries);
            
            _databaseMock.Setup(d => d.SortedSetLengthAsync(
                It.IsAny<RedisKey>(), 
                It.IsAny<double>(), 
                It.IsAny<double>(), 
                It.IsAny<Exclude>(), 
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(95);
            
            // Act
            var stats = await _metricsService.GetUrlStatisticsAsync(webhookUrl);
            
            // Assert
            Assert.Equal(webhookUrl, stats.Url);
            Assert.Equal(100, stats.TotalDeliveries);
            Assert.Equal(95, stats.SuccessfulDeliveries);
            Assert.Equal(5, stats.FailedDeliveries);
            Assert.Equal(2, stats.PendingRetries);
            Assert.Equal(250, stats.AverageResponseTimeMs);
            Assert.Equal(95, stats.SuccessRate);
            Assert.True(stats.IsHealthy);
        }
        
        [Fact]
        public async Task GetStatisticsAsync_AggregatesMultipleUrls()
        {
            // Arrange
            var keys = new RedisKey[] 
            { 
                "webhook:metrics:urls:hash1",
                "webhook:metrics:urls:hash2"
            };
            
            _serverMock.Setup(s => s.Keys(
                It.IsAny<int>(), 
                It.IsAny<RedisValue>(), 
                It.IsAny<int>(), 
                It.IsAny<long>(), 
                It.IsAny<int>(), 
                It.IsAny<CommandFlags>()))
                .Returns(keys);
            
            var hashEntries1 = new HashEntry[]
            {
                new HashEntry("url", "https://example1.com/webhook"),
                new HashEntry("total_attempts", 50),
                new HashEntry("successes", 45),
                new HashEntry("failures", 5),
                new HashEntry("last_attempt", DateTime.UtcNow.ToString("O"))
            };
            
            var hashEntries2 = new HashEntry[]
            {
                new HashEntry("url", "https://example2.com/webhook"),
                new HashEntry("total_attempts", 100),
                new HashEntry("successes", 95),
                new HashEntry("failures", 5),
                new HashEntry("last_attempt", DateTime.UtcNow.ToString("O"))
            };
            
            _databaseMock.SetupSequence(d => d.HashGetAllAsync(
                It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(hashEntries1)
                .ReturnsAsync(hashEntries2);
            
            // Act
            var stats = await _metricsService.GetStatisticsAsync("last_hour");
            
            // Assert
            Assert.Equal("last_hour", stats.Period);
            Assert.Equal(2, stats.UrlStatistics.Count);
            Assert.Equal(150, stats.TotalDeliveries); // 50 + 100
            Assert.Equal(140, stats.SuccessfulDeliveries); // 45 + 95
            Assert.Equal(10, stats.FailedDeliveries); // 5 + 5
        }
        
        [Fact]
        public async Task AddRecentEventAsync_AddsEventToSortedSet()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var eventType = "success";
            var responseTimeMs = 250L;
            
            // Act
            await _metricsService.AddRecentEventAsync(webhookUrl, eventType, responseTimeMs);
            
            // Assert
            _transactionMock.Verify(t => t.SortedSetAddAsync(
                It.Is<RedisKey>(k => k.ToString() == "webhook:events:recent"),
                It.Is<RedisValue>(v => v.ToString().Contains(webhookUrl) && v.ToString().Contains(eventType)),
                It.IsAny<double>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()), Times.Once);
        }
        
        [Fact]
        public async Task RecordAttemptAsync_HandlesRedisException()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var taskId = "task-123";
            var taskType = "test";
            var eventType = "completion";
            
            _transactionMock.Setup(t => t.ExecuteAsync(It.IsAny<CommandFlags>()))
                .ThrowsAsync(new RedisException("Connection failed"));
            
            // Act
            await _metricsService.RecordAttemptAsync(webhookUrl, taskId, taskType, eventType);
            
            // Assert - Should not throw, just log
            _loggerMock.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error recording delivery attempt")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }
        
        [Fact]
        public async Task GetStatisticsAsync_HandlesRedisException_ReturnsEmptyStats()
        {
            // Arrange
            _serverMock.Setup(s => s.Keys(
                It.IsAny<int>(), 
                It.IsAny<RedisValue>(), 
                It.IsAny<int>(), 
                It.IsAny<long>(), 
                It.IsAny<int>(), 
                It.IsAny<CommandFlags>()))
                .Throws(new RedisException("Connection failed"));
            
            // Act
            var stats = await _metricsService.GetStatisticsAsync("last_hour");
            
            // Assert
            Assert.Equal("last_hour", stats.Period);
            Assert.Empty(stats.UrlStatistics);
            Assert.Equal(0, stats.TotalDeliveries);
            Assert.Equal(0, stats.SuccessfulDeliveries);
            Assert.Equal(0, stats.FailedDeliveries);
        }
        
        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}
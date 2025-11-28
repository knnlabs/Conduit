using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    public class RedisWebhookCircuitBreakerTests : IDisposable
    {
        private readonly Mock<IConnectionMultiplexer> _redisMock;
        private readonly Mock<IDatabase> _databaseMock;
        private readonly Mock<ITransaction> _transactionMock;
        private readonly Mock<ILogger<RedisWebhookCircuitBreaker>> _loggerMock;
        private readonly RedisWebhookCircuitBreaker _circuitBreaker;
        
        public RedisWebhookCircuitBreakerTests()
        {
            _redisMock = new Mock<IConnectionMultiplexer>();
            _databaseMock = new Mock<IDatabase>();
            _transactionMock = new Mock<ITransaction>();
            _loggerMock = new Mock<ILogger<RedisWebhookCircuitBreaker>>();
            
            _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_databaseMock.Object);
            _databaseMock.Setup(d => d.CreateTransaction(It.IsAny<object>())).Returns(_transactionMock.Object);

            // Setup StringSet for both 5-parameter and 6-parameter overloads
            _databaseMock.Setup(d => d.StringSet(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .Returns(true);
            _databaseMock.Setup(d => d.StringSet(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .Returns(true);

            // Setup StringSetAsync for transaction operations
            _transactionMock.Setup(t => t.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .Returns(Task.FromResult(true));
            _transactionMock.Setup(t => t.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .Returns(Task.FromResult(true));

            _circuitBreaker = new RedisWebhookCircuitBreaker(
                _redisMock.Object,
                _loggerMock.Object,
                failureThreshold: 3,
                openDuration: TimeSpan.FromMinutes(1),
                halfOpenTestInterval: TimeSpan.FromSeconds(10));
        }
        
        [Fact]
        public void IsOpen_WhenCircuitClosed_ReturnsFalse()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            _databaseMock.Setup(d => d.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .Returns(RedisValue.Null);
            
            // Act
            var result = _circuitBreaker.IsOpen(webhookUrl);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public void IsOpen_WhenCircuitOpen_ReturnsTrue()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var circuitState = @"{""State"":""Open"",""OpenedAt"":""" + DateTime.UtcNow.ToString("O") + @""",""FailureCount"":5}";
            _databaseMock.Setup(d => d.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .Returns(circuitState);
            
            // Act
            var result = _circuitBreaker.IsOpen(webhookUrl);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void RecordSuccess_ResetsFailureCount()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            _transactionMock.Setup(t => t.Execute(It.IsAny<CommandFlags>())).Returns(true);
            _databaseMock.Setup(d => d.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .Returns(RedisValue.Null);
            
            // Act
            _circuitBreaker.RecordSuccess(webhookUrl);
            
            // Assert
            _transactionMock.Verify(t => t.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString()!.Contains("failures")), 
                It.IsAny<CommandFlags>()), Times.Once);
            _transactionMock.Verify(t => t.StringIncrementAsync(
                It.Is<RedisKey>(k => k.ToString()!.Contains("success")), 
                1, It.IsAny<CommandFlags>()), Times.Once);
        }
        
        [Fact]
        public void RecordFailure_IncrementsFailureCount()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            _databaseMock.Setup(d => d.StringIncrement(
                It.IsAny<RedisKey>(), 1, It.IsAny<CommandFlags>()))
                .Returns(1);
            _databaseMock.Setup(d => d.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .Returns(RedisValue.Null);
            
            // Act
            _circuitBreaker.RecordFailure(webhookUrl);
            
            // Assert
            _databaseMock.Verify(d => d.StringIncrement(
                It.Is<RedisKey>(k => k.ToString()!.Contains("failures")), 
                1, It.IsAny<CommandFlags>()), Times.Once);
        }
        
        [Fact]
        public void RecordFailure_OpensCircuit_WhenThresholdReached()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            _databaseMock.Setup(d => d.StringIncrement(
                It.IsAny<RedisKey>(), 1, It.IsAny<CommandFlags>()))
                .Returns(3); // Threshold is 3
            _databaseMock.Setup(d => d.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .Returns(RedisValue.Null);
            
            // Act
            _circuitBreaker.RecordFailure(webhookUrl);
            
            // Assert - Verify StringSet was called with state key
            // Use invocation inspection since Moq overload resolution can be tricky
            var stateSetInvocations = _databaseMock.Invocations
                .Where(i => i.Method.Name == "StringSet" &&
                       i.Arguments.Count > 0 &&
                       i.Arguments[0].ToString()!.Contains("state"))
                .ToList();
            Assert.True(stateSetInvocations.Count >= 1, "StringSet should be called at least once with 'state' key");

            _loggerMock.Verify(l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Circuit breaker opened")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public void GetStats_ReturnsCorrectStatistics()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var batch = new Mock<IBatch>();
            
            var failureTask = Task.FromResult<RedisValue>(5);
            var successTask = Task.FromResult<RedisValue>(10);
            var lastFailureTask = Task.FromResult<RedisValue>(DateTime.UtcNow.AddMinutes(-5).ToString("O"));
            var stateTask = Task.FromResult<RedisValue>(@"{""State"":""Open"",""OpenedAt"":""" + 
                DateTime.UtcNow.AddMinutes(-2).ToString("O") + @""",""FailureCount"":5}");
            
            batch.Setup(b => b.StringGetAsync(It.Is<RedisKey>(k => k.ToString()!.Contains("failures")), It.IsAny<CommandFlags>()))
                .Returns(failureTask);
            batch.Setup(b => b.StringGetAsync(It.Is<RedisKey>(k => k.ToString()!.Contains("success")), It.IsAny<CommandFlags>()))
                .Returns(successTask);
            batch.Setup(b => b.StringGetAsync(It.Is<RedisKey>(k => k.ToString()!.Contains("lastfail")), It.IsAny<CommandFlags>()))
                .Returns(lastFailureTask);
            batch.Setup(b => b.StringGetAsync(It.Is<RedisKey>(k => k.ToString()!.Contains("state")), It.IsAny<CommandFlags>()))
                .Returns(stateTask);
            batch.Setup(b => b.Execute());
            
            _databaseMock.Setup(d => d.CreateBatch(It.IsAny<object>())).Returns(batch.Object);
            
            // Act
            var stats = _circuitBreaker.GetStats(webhookUrl);
            
            // Assert
            Assert.Equal(5, stats.FailureCount);
            Assert.Equal(10, stats.SuccessCount);
            Assert.True(stats.IsOpen);
            Assert.NotNull(stats.LastFailureTime);
            Assert.NotNull(stats.CircuitOpenedAt);
        }
        
        [Fact]
        public void IsOpen_TransitionsToHalfOpen_AfterOpenDuration()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var openedTime = DateTime.UtcNow.AddMinutes(-2); // Opened 2 minutes ago, open duration is 1 minute
            var circuitState = @"{""State"":""Open"",""OpenedAt"":""" + openedTime.ToString("O") + @""",""FailureCount"":5}";
            
            _databaseMock.Setup(d => d.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .Returns(circuitState);
            
            // Return value doesn't matter for mocking, transaction.AddCondition returns ConditionResult
            // We just need to set up the mock to not throw
            _transactionMock.Setup(t => t.AddCondition(It.IsAny<Condition>()));
            _transactionMock.Setup(t => t.Execute(It.IsAny<CommandFlags>())).Returns(true);
            
            // Act
            var result = _circuitBreaker.IsOpen(webhookUrl);

            // Assert
            Assert.False(result); // Should allow one test request in half-open state
            // Use invocation inspection since Moq overload resolution can be tricky
            var halfOpenSetInvocations = _transactionMock.Invocations
                .Where(i => i.Method.Name == "StringSetAsync" &&
                       i.Arguments.Count > 1 &&
                       i.Arguments[1].ToString()!.Contains("HalfOpen"))
                .ToList();
            Assert.True(halfOpenSetInvocations.Count >= 1, "StringSetAsync should be called with HalfOpen state");
        }
        
        [Fact]
        public void RecordFailure_InHalfOpenState_ImmediatelyOpensCircuit()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var circuitState = @"{""State"":""HalfOpen"",""OpenedAt"":""" + DateTime.UtcNow.AddMinutes(-1).ToString("O") + 
                @""",""HalfOpenTestAt"":""" + DateTime.UtcNow.ToString("O") + @""",""FailureCount"":3}";
            
            _databaseMock.Setup(d => d.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .Returns(circuitState);
            
            // Act
            _circuitBreaker.RecordFailure(webhookUrl);

            // Assert - Use invocation inspection since Moq overload resolution can be tricky
            var openStateInvocations = _databaseMock.Invocations
                .Where(i => i.Method.Name == "StringSet" &&
                       i.Arguments.Count > 1 &&
                       i.Arguments[0].ToString()!.Contains("state") &&
                       i.Arguments[1].ToString()!.Contains("Open"))
                .ToList();
            Assert.True(openStateInvocations.Count >= 1, "StringSet should be called with 'state' key and 'Open' value");
        }

        [Fact]
        public void IsOpen_HandlesRedisException_ReturnsFalse()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            _databaseMock.Setup(d => d.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .Throws(new RedisException("Connection failed"));
            
            // Act
            var result = _circuitBreaker.IsOpen(webhookUrl);
            
            // Assert
            Assert.False(result); // Should not block webhooks on Redis failure
            _loggerMock.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error checking circuit state")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }
        
        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}
using ConduitLLM.Core.Services;

using FluentAssertions;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Core.Services
{
    [Trait("Category", "Unit")]
    [Trait("Phase", "1")]
    [Trait("Component", "Core")]
    public class WebhookCircuitBreakerTests : TestBase
    {
        private readonly IMemoryCache _cache;
        private readonly Mock<ILogger<WebhookCircuitBreaker>> _loggerMock;
        private readonly WebhookCircuitBreaker _circuitBreaker;

        public WebhookCircuitBreakerTests(ITestOutputHelper output) : base(output)
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
            _loggerMock = CreateLogger<WebhookCircuitBreaker>();
            _circuitBreaker = new WebhookCircuitBreaker(_cache, _loggerMock.Object);
        }

        [Fact]
        public void Constructor_WithNullCache_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new WebhookCircuitBreaker(null, _loggerMock.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("cache");
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new WebhookCircuitBreaker(_cache, null);
            act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        }

        [Theory]
        [InlineData(3, 5, 10)]
        [InlineData(5, 10, 15)]
        [InlineData(10, 1, 30)]
        public void Constructor_WithCustomParameters_UsesProvidedValues(int failureThreshold, int openDurationMinutes, int resetDurationMinutes)
        {
            // Arrange
            var openDuration = TimeSpan.FromMinutes(openDurationMinutes);
            var resetDuration = TimeSpan.FromMinutes(resetDurationMinutes);
            var url = "https://example.com/webhook";

            // Act
            var customBreaker = new WebhookCircuitBreaker(
                _cache, 
                _loggerMock.Object, 
                failureThreshold,
                openDuration,
                resetDuration);

            // Record failures up to threshold
            for (int i = 0; i < failureThreshold; i++)
            {
                customBreaker.RecordFailure(url);
            }

            // Assert
            customBreaker.IsOpen(url).Should().BeTrue();
        }

        [Fact]
        public void IsOpen_NewUrl_ReturnsFalse()
        {
            // Arrange
            var url = "https://example.com/webhook";

            // Act
            var result = _circuitBreaker.IsOpen(url);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void RecordFailure_BelowThreshold_DoesNotOpenCircuit()
        {
            // Arrange
            var url = "https://example.com/webhook";

            // Act - Record 4 failures (below default threshold of 5)
            for (int i = 0; i < 4; i++)
            {
                _circuitBreaker.RecordFailure(url);
            }

            // Assert
            _circuitBreaker.IsOpen(url).Should().BeFalse();
        }

        [Fact]
        public void RecordFailure_ReachesThreshold_OpensCircuit()
        {
            // Arrange
            var url = "https://example.com/webhook";

            // Act - Record 5 failures (reaches default threshold)
            for (int i = 0; i < 5; i++)
            {
                _circuitBreaker.RecordFailure(url);
            }

            // Assert
            _circuitBreaker.IsOpen(url).Should().BeTrue();
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Circuit breaker opened")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void RecordFailure_AlreadyOpen_DoesNotLogAgain()
        {
            // Arrange
            var url = "https://example.com/webhook";
            
            // Open the circuit
            for (int i = 0; i < 5; i++)
            {
                _circuitBreaker.RecordFailure(url);
            }
            _loggerMock.Reset();

            // Act - Record another failure
            _circuitBreaker.RecordFailure(url);

            // Assert
            _circuitBreaker.IsOpen(url).Should().BeTrue();
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Circuit breaker opened")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Never);
        }

        [Fact]
        public void RecordSuccess_WithNoFailures_DoesNothing()
        {
            // Arrange
            var url = "https://example.com/webhook";

            // Act
            _circuitBreaker.RecordSuccess(url);

            // Assert
            _circuitBreaker.IsOpen(url).Should().BeFalse();
            var stats = _circuitBreaker.GetStats(url);
            stats.SuccessCount.Should().Be(1);
            stats.FailureCount.Should().Be(0);
        }

        [Fact]
        public void RecordSuccess_WithFailures_ResetsFailureCount()
        {
            // Arrange
            var url = "https://example.com/webhook";
            
            // Record some failures (but not enough to open)
            _circuitBreaker.RecordFailure(url);
            _circuitBreaker.RecordFailure(url);
            _circuitBreaker.RecordFailure(url);

            // Act
            _circuitBreaker.RecordSuccess(url);

            // Assert
            var stats = _circuitBreaker.GetStats(url);
            stats.FailureCount.Should().Be(0);
            stats.SuccessCount.Should().Be(1);
        }

        [Fact]
        public void RecordSuccess_WithOpenCircuit_ClosesCircuit()
        {
            // Arrange
            var url = "https://example.com/webhook";
            
            // Open the circuit
            for (int i = 0; i < 5; i++)
            {
                _circuitBreaker.RecordFailure(url);
            }
            _circuitBreaker.IsOpen(url).Should().BeTrue();
            _loggerMock.Reset();

            // Act
            _circuitBreaker.RecordSuccess(url);

            // Assert
            _circuitBreaker.IsOpen(url).Should().BeFalse();
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Circuit breaker closed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void GetStats_NewUrl_ReturnsZeroStats()
        {
            // Arrange
            var url = "https://example.com/webhook";

            // Act
            var stats = _circuitBreaker.GetStats(url);

            // Assert
            stats.Should().NotBeNull();
            stats.FailureCount.Should().Be(0);
            stats.SuccessCount.Should().Be(0);
            stats.LastFailureTime.Should().BeNull();
            stats.CircuitOpenedAt.Should().BeNull();
            stats.IsOpen.Should().BeFalse();
        }

        [Fact]
        public void GetStats_WithFailures_ReturnsCorrectStats()
        {
            // Arrange
            var url = "https://example.com/webhook";
            var beforeFailure = DateTime.UtcNow;
            
            // Record failures
            _circuitBreaker.RecordFailure(url);
            _circuitBreaker.RecordFailure(url);
            _circuitBreaker.RecordFailure(url);

            // Act
            var stats = _circuitBreaker.GetStats(url);

            // Assert
            stats.FailureCount.Should().Be(3);
            stats.SuccessCount.Should().Be(0);
            stats.LastFailureTime.Should().NotBeNull();
            stats.LastFailureTime.Value.Should().BeAfter(beforeFailure);
            stats.CircuitOpenedAt.Should().BeNull();
            stats.IsOpen.Should().BeFalse();
        }

        [Fact]
        public void GetStats_WithOpenCircuit_ReturnsOpenStats()
        {
            // Arrange
            var url = "https://example.com/webhook";
            var beforeOpen = DateTime.UtcNow;
            
            // Open circuit
            for (int i = 0; i < 5; i++)
            {
                _circuitBreaker.RecordFailure(url);
            }

            // Act
            var stats = _circuitBreaker.GetStats(url);

            // Assert
            stats.FailureCount.Should().Be(5);
            stats.SuccessCount.Should().Be(0);
            stats.LastFailureTime.Should().NotBeNull();
            stats.CircuitOpenedAt.Should().NotBeNull();
            stats.CircuitOpenedAt.Value.Should().BeAfter(beforeOpen);
            stats.IsOpen.Should().BeTrue();
        }

        [Fact]
        public void GetStats_WithMixedHistory_ReturnsCorrectCounts()
        {
            // Arrange
            var url = "https://example.com/webhook";
            
            // Record mixed results:
            // 1. Success - creates success count 1
            _circuitBreaker.RecordSuccess(url);
            // 2. Failure - adds failure count 1 
            _circuitBreaker.RecordFailure(url);
            // 3. Success - resets failure count to 0, success count 2
            _circuitBreaker.RecordSuccess(url);
            // 4. Success - success count 3
            _circuitBreaker.RecordSuccess(url);
            // 5. Failure - failure count 1
            _circuitBreaker.RecordFailure(url);

            // Act
            var stats = _circuitBreaker.GetStats(url);

            // Assert
            stats.FailureCount.Should().Be(1); // Only the last failure counts
            stats.SuccessCount.Should().Be(3); // Total success count
            stats.IsOpen.Should().BeFalse();
        }

        [Fact]
        public void MultipleUrls_MaintainSeparateState()
        {
            // Arrange
            var url1 = "https://example1.com/webhook";
            var url2 = "https://example2.com/webhook";
            
            // Act - Open circuit for url1 only
            for (int i = 0; i < 5; i++)
            {
                _circuitBreaker.RecordFailure(url1);
            }
            _circuitBreaker.RecordSuccess(url2);

            // Assert
            _circuitBreaker.IsOpen(url1).Should().BeTrue();
            _circuitBreaker.IsOpen(url2).Should().BeFalse();
            
            var stats1 = _circuitBreaker.GetStats(url1);
            var stats2 = _circuitBreaker.GetStats(url2);
            
            stats1.FailureCount.Should().Be(5);
            stats1.IsOpen.Should().BeTrue();
            
            stats2.SuccessCount.Should().Be(1);
            stats2.IsOpen.Should().BeFalse();
        }

        [Fact]
        public async Task CircuitOpen_ExpiresAfterDuration()
        {
            // Arrange
            var url = "https://example.com/webhook";
            var shortDuration = TimeSpan.FromMilliseconds(100);
            var breaker = new WebhookCircuitBreaker(
                _cache, 
                _loggerMock.Object, 
                failureThreshold: 2,
                openDuration: shortDuration);
            
            // Open circuit
            breaker.RecordFailure(url);
            breaker.RecordFailure(url);
            breaker.IsOpen(url).Should().BeTrue();

            // Act - Wait for expiration
            await Task.Delay(shortDuration.Add(TimeSpan.FromMilliseconds(50)));

            // Assert
            breaker.IsOpen(url).Should().BeFalse();
        }

        [Trait("Category", "Performance")]
        [Fact]
        public void RecordFailure_Performance_HandlesHighVolume()
        {
            // Arrange
            var urls = new string[100];
            for (int i = 0; i < urls.Length; i++)
            {
                urls[i] = $"https://example{i}.com/webhook";
            }
            var iterations = 1000;

            // Act
            var startTime = DateTime.UtcNow;
            for (int i = 0; i < iterations; i++)
            {
                var url = urls[i % urls.Length];
                _circuitBreaker.RecordFailure(url);
                if (i % 3 == 0) // Some successes
                {
                    _circuitBreaker.RecordSuccess(url);
                }
            }
            var duration = DateTime.UtcNow - startTime;

            // Assert
            duration.Should().BeLessThan(TimeSpan.FromMilliseconds(500));
            Log($"Processed {iterations} operations across {urls.Length} URLs in {duration.TotalMilliseconds:F2}ms");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cache?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
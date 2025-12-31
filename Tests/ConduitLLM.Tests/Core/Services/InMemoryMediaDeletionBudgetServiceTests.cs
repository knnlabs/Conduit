using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Unit tests for InMemoryMediaDeletionBudgetService.
    /// Tests the in-memory implementation of media deletion budget tracking.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "MediaLifecycle")]
    public class InMemoryMediaDeletionBudgetServiceTests
    {
        private readonly Mock<ILogger<InMemoryMediaDeletionBudgetService>> _mockLogger;
        private readonly InMemoryMediaDeletionBudgetService _service;

        public InMemoryMediaDeletionBudgetServiceTests()
        {
            _mockLogger = new Mock<ILogger<InMemoryMediaDeletionBudgetService>>();
            _service = new InMemoryMediaDeletionBudgetService(_mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new InMemoryMediaDeletionBudgetService(null!));
        }

        [Fact]
        public void Constructor_LogsWarningAboutInMemoryLimitations()
        {
            // Arrange & Act
            var logger = new Mock<ILogger<InMemoryMediaDeletionBudgetService>>();
            _ = new InMemoryMediaDeletionBudgetService(logger.Object);

            // Assert
            logger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) =>
                        o.ToString()!.Contains("in-memory") &&
                        o.ToString()!.Contains("persist")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region GetMonthlyDeleteCountAsync Tests

        [Fact]
        public async Task GetMonthlyDeleteCountAsync_WhenNoIncrements_ReturnsZero()
        {
            // Act
            var count = await _service.GetMonthlyDeleteCountAsync();

            // Assert
            count.Should().Be(0);
        }

        [Fact]
        public async Task GetMonthlyDeleteCountAsync_AfterIncrement_ReturnsCorrectCount()
        {
            // Arrange
            await _service.IncrementMonthlyDeleteCountAsync(50);

            // Act
            var count = await _service.GetMonthlyDeleteCountAsync();

            // Assert
            count.Should().Be(50);
        }

        [Fact]
        public async Task GetMonthlyDeleteCountAsync_WithCancellationToken_CompletesSuccessfully()
        {
            // Arrange
            using var cts = new CancellationTokenSource();

            // Act
            var count = await _service.GetMonthlyDeleteCountAsync(cts.Token);

            // Assert
            count.Should().Be(0);
        }

        #endregion

        #region IncrementMonthlyDeleteCountAsync Tests

        [Fact]
        public async Task IncrementMonthlyDeleteCountAsync_WithPositiveCount_ReturnsNewTotal()
        {
            // Act
            var result = await _service.IncrementMonthlyDeleteCountAsync(100);

            // Assert
            result.Should().Be(100);
        }

        [Fact]
        public async Task IncrementMonthlyDeleteCountAsync_MultipleIncrements_AccumulatesCorrectly()
        {
            // Act
            await _service.IncrementMonthlyDeleteCountAsync(50);
            await _service.IncrementMonthlyDeleteCountAsync(30);
            var result = await _service.IncrementMonthlyDeleteCountAsync(20);

            // Assert
            result.Should().Be(100);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public async Task IncrementMonthlyDeleteCountAsync_WithZeroOrNegativeCount_ReturnsCurrentCount(int count)
        {
            // Arrange
            await _service.IncrementMonthlyDeleteCountAsync(50);

            // Act
            var result = await _service.IncrementMonthlyDeleteCountAsync(count);

            // Assert
            result.Should().Be(50);
        }

        [Fact]
        public async Task IncrementMonthlyDeleteCountAsync_LogsDebugMessage()
        {
            // Act
            await _service.IncrementMonthlyDeleteCountAsync(25);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) =>
                        o.ToString()!.Contains("Incremented") &&
                        o.ToString()!.Contains("25")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region WouldExceedBudgetAsync Tests

        [Fact]
        public async Task WouldExceedBudgetAsync_WhenWithinBudget_ReturnsFalse()
        {
            // Arrange
            await _service.IncrementMonthlyDeleteCountAsync(400_000);

            // Act
            var result = await _service.WouldExceedBudgetAsync(50_000, 500_000);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task WouldExceedBudgetAsync_WhenExactlyAtBudget_ReturnsFalse()
        {
            // Arrange
            await _service.IncrementMonthlyDeleteCountAsync(400_000);

            // Act
            var result = await _service.WouldExceedBudgetAsync(100_000, 500_000);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task WouldExceedBudgetAsync_WhenWouldExceedBudget_ReturnsTrue()
        {
            // Arrange
            await _service.IncrementMonthlyDeleteCountAsync(400_000);

            // Act
            var result = await _service.WouldExceedBudgetAsync(100_001, 500_000);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task WouldExceedBudgetAsync_WhenAlreadyExceeded_ReturnsTrue()
        {
            // Arrange
            await _service.IncrementMonthlyDeleteCountAsync(600_000);

            // Act
            var result = await _service.WouldExceedBudgetAsync(1, 500_000);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task WouldExceedBudgetAsync_WithZeroProposedDeletions_ReturnsFalse()
        {
            // Arrange
            await _service.IncrementMonthlyDeleteCountAsync(499_999);

            // Act
            var result = await _service.WouldExceedBudgetAsync(0, 500_000);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region GetRemainingBudgetAsync Tests

        [Fact]
        public async Task GetRemainingBudgetAsync_WhenNoUsage_ReturnsFullBudget()
        {
            // Act
            var result = await _service.GetRemainingBudgetAsync(500_000);

            // Assert
            result.Should().Be(500_000);
        }

        [Fact]
        public async Task GetRemainingBudgetAsync_WithPartialUsage_ReturnsCorrectRemaining()
        {
            // Arrange
            await _service.IncrementMonthlyDeleteCountAsync(150_000);

            // Act
            var result = await _service.GetRemainingBudgetAsync(500_000);

            // Assert
            result.Should().Be(350_000);
        }

        [Fact]
        public async Task GetRemainingBudgetAsync_WhenExactlyAtBudget_ReturnsZero()
        {
            // Arrange
            await _service.IncrementMonthlyDeleteCountAsync(500_000);

            // Act
            var result = await _service.GetRemainingBudgetAsync(500_000);

            // Assert
            result.Should().Be(0);
        }

        [Fact]
        public async Task GetRemainingBudgetAsync_WhenOverBudget_ReturnsZero()
        {
            // Arrange
            await _service.IncrementMonthlyDeleteCountAsync(600_000);

            // Act
            var result = await _service.GetRemainingBudgetAsync(500_000);

            // Assert
            result.Should().Be(0);
        }

        #endregion

        #region Concurrency Tests

        [Fact]
        public async Task IncrementMonthlyDeleteCountAsync_ConcurrentIncrements_AreThreadSafe()
        {
            // Arrange
            const int concurrency = 100;
            const int incrementPerTask = 100;
            var tasks = new Task[concurrency];

            // Act
            for (int i = 0; i < concurrency; i++)
            {
                tasks[i] = _service.IncrementMonthlyDeleteCountAsync(incrementPerTask);
            }
            await Task.WhenAll(tasks);

            // Assert
            var total = await _service.GetMonthlyDeleteCountAsync();
            total.Should().Be(concurrency * incrementPerTask);
        }

        #endregion

        #region Interface Compliance Tests

        [Fact]
        public void Service_ImplementsIMediaDeletionBudgetService()
        {
            // Assert
            _service.Should().BeAssignableTo<IMediaDeletionBudgetService>();
        }

        #endregion
    }
}

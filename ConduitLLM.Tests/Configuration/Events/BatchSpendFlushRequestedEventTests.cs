using System;
using Xunit;
using ConduitLLM.Configuration.Events;

namespace ConduitLLM.Tests.Configuration.Events
{
    /// <summary>
    /// Unit tests for BatchSpendFlushRequestedEvent and related classes.
    /// Ensures proper initialization, validation, and serialization behavior.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "BatchSpendFlushEvent")]
    public class BatchSpendFlushRequestedEventTests
    {
        #region BatchSpendFlushRequestedEvent Tests

        [Fact]
        public void BatchSpendFlushRequestedEvent_DefaultConstructor_SetsDefaultValues()
        {
            // Act
            var batchEvent = new BatchSpendFlushRequestedEvent();

            // Assert
            Assert.NotNull(batchEvent.RequestId);
            Assert.NotEmpty(batchEvent.RequestId);
            Assert.Equal("System", batchEvent.RequestedBy);
            Assert.True(batchEvent.RequestedAt <= DateTime.UtcNow);
            Assert.True(batchEvent.RequestedAt >= DateTime.UtcNow.AddSeconds(-1)); // Should be very recent
            Assert.Null(batchEvent.Reason);
            Assert.Null(batchEvent.Source);
            Assert.Equal(FlushPriority.Normal, batchEvent.Priority);
            Assert.Null(batchEvent.TimeoutSeconds);
            Assert.True(batchEvent.IncludeStatistics);
        }

        [Fact]
        public void BatchSpendFlushRequestedEvent_RequestIdProperty_IsUniqueGuid()
        {
            // Arrange & Act
            var event1 = new BatchSpendFlushRequestedEvent();
            var event2 = new BatchSpendFlushRequestedEvent();

            // Assert
            Assert.NotEqual(event1.RequestId, event2.RequestId);
            Assert.True(Guid.TryParse(event1.RequestId, out _));
            Assert.True(Guid.TryParse(event2.RequestId, out _));
        }

        [Fact]
        public void BatchSpendFlushRequestedEvent_AllProperties_CanBeSetAndGet()
        {
            // Arrange
            var requestId = Guid.NewGuid().ToString();
            var requestedBy = "Admin";
            var requestedAt = DateTime.UtcNow.AddMinutes(-1);
            var reason = "Integration test flush";
            var source = "Test Suite";
            var priority = FlushPriority.High;
            var timeoutSeconds = 30;
            var includeStatistics = false;

            // Act
            var batchEvent = new BatchSpendFlushRequestedEvent
            {
                RequestId = requestId,
                RequestedBy = requestedBy,
                RequestedAt = requestedAt,
                Reason = reason,
                Source = source,
                Priority = priority,
                TimeoutSeconds = timeoutSeconds,
                IncludeStatistics = includeStatistics
            };

            // Assert
            Assert.Equal(requestId, batchEvent.RequestId);
            Assert.Equal(requestedBy, batchEvent.RequestedBy);
            Assert.Equal(requestedAt, batchEvent.RequestedAt);
            Assert.Equal(reason, batchEvent.Reason);
            Assert.Equal(source, batchEvent.Source);
            Assert.Equal(priority, batchEvent.Priority);
            Assert.Equal(timeoutSeconds, batchEvent.TimeoutSeconds);
            Assert.Equal(includeStatistics, batchEvent.IncludeStatistics);
        }

        #endregion

        #region FlushPriority Enum Tests

        [Fact]
        public void FlushPriority_HasCorrectValues()
        {
            // Assert
            Assert.Equal(0, (int)FlushPriority.Normal);
            Assert.Equal(1, (int)FlushPriority.High);
        }

        [Theory]
        [InlineData(FlushPriority.Normal)]
        [InlineData(FlushPriority.High)]
        public void FlushPriority_CanBeAssignedToEvent(FlushPriority priority)
        {
            // Arrange & Act
            var batchEvent = new BatchSpendFlushRequestedEvent { Priority = priority };

            // Assert
            Assert.Equal(priority, batchEvent.Priority);
        }

        #endregion

        #region BatchSpendFlushCompletedEvent Tests

        [Fact]
        public void BatchSpendFlushCompletedEvent_DefaultConstructor_SetsDefaultValues()
        {
            // Act
            var completedEvent = new BatchSpendFlushCompletedEvent();

            // Assert
            Assert.Empty(completedEvent.RequestId);
            Assert.False(completedEvent.Success);
            Assert.Equal(0, completedEvent.GroupsFlushed);
            Assert.Equal(0m, completedEvent.TotalAmountFlushed);
            Assert.Equal(TimeSpan.Zero, completedEvent.Duration);
            Assert.True(completedEvent.CompletedAt <= DateTime.UtcNow);
            Assert.True(completedEvent.CompletedAt >= DateTime.UtcNow.AddSeconds(-1));
            Assert.Null(completedEvent.ErrorMessage);
            Assert.Null(completedEvent.Statistics);
        }

        [Fact]
        public void BatchSpendFlushCompletedEvent_SuccessfulOperation_PropertiesSetCorrectly()
        {
            // Arrange
            var requestId = Guid.NewGuid().ToString();
            var duration = TimeSpan.FromMilliseconds(1500);
            var completedAt = DateTime.UtcNow;
            var statistics = new BatchSpendFlushStatistics
            {
                RedisKeysProcessed = 5,
                DatabaseTransactionsCreated = 5
            };

            // Act
            var completedEvent = new BatchSpendFlushCompletedEvent
            {
                RequestId = requestId,
                Success = true,
                GroupsFlushed = 5,
                TotalAmountFlushed = 12.34m,
                Duration = duration,
                CompletedAt = completedAt,
                Statistics = statistics
            };

            // Assert
            Assert.Equal(requestId, completedEvent.RequestId);
            Assert.True(completedEvent.Success);
            Assert.Equal(5, completedEvent.GroupsFlushed);
            Assert.Equal(12.34m, completedEvent.TotalAmountFlushed);
            Assert.Equal(duration, completedEvent.Duration);
            Assert.Equal(completedAt, completedEvent.CompletedAt);
            Assert.Null(completedEvent.ErrorMessage);
            Assert.NotNull(completedEvent.Statistics);
            Assert.Equal(5, completedEvent.Statistics.RedisKeysProcessed);
        }

        [Fact]
        public void BatchSpendFlushCompletedEvent_FailedOperation_ErrorMessageSet()
        {
            // Arrange
            var requestId = Guid.NewGuid().ToString();
            var errorMessage = "Service unavailable";

            // Act
            var completedEvent = new BatchSpendFlushCompletedEvent
            {
                RequestId = requestId,
                Success = false,
                ErrorMessage = errorMessage
            };

            // Assert
            Assert.Equal(requestId, completedEvent.RequestId);
            Assert.False(completedEvent.Success);
            Assert.Equal(errorMessage, completedEvent.ErrorMessage);
            Assert.Equal(0, completedEvent.GroupsFlushed);
            Assert.Equal(0m, completedEvent.TotalAmountFlushed);
        }

        #endregion

        #region BatchSpendFlushStatistics Tests

        [Fact]
        public void BatchSpendFlushStatistics_DefaultConstructor_SetsDefaultValues()
        {
            // Act
            var statistics = new BatchSpendFlushStatistics();

            // Assert
            Assert.Equal(0, statistics.RedisKeysProcessed);
            Assert.Equal(0, statistics.DatabaseTransactionsCreated);
            Assert.Equal(0, statistics.CacheInvalidationsTriggered);
            Assert.Equal(0.0, statistics.RedisOperationMs);
            Assert.Equal(0.0, statistics.DatabaseOperationMs);
            Assert.Null(statistics.Warnings);
        }

        [Fact]
        public void BatchSpendFlushStatistics_AllProperties_CanBeSetAndGet()
        {
            // Arrange
            var warnings = new[] { "Warning 1", "Warning 2" };

            // Act
            var statistics = new BatchSpendFlushStatistics
            {
                RedisKeysProcessed = 10,
                DatabaseTransactionsCreated = 8,
                CacheInvalidationsTriggered = 12,
                RedisOperationMs = 150.5,
                DatabaseOperationMs = 300.75,
                Warnings = warnings
            };

            // Assert
            Assert.Equal(10, statistics.RedisKeysProcessed);
            Assert.Equal(8, statistics.DatabaseTransactionsCreated);
            Assert.Equal(12, statistics.CacheInvalidationsTriggered);
            Assert.Equal(150.5, statistics.RedisOperationMs);
            Assert.Equal(300.75, statistics.DatabaseOperationMs);
            Assert.Equal(warnings, statistics.Warnings);
            Assert.Equal(2, statistics.Warnings.Length);
            Assert.Contains("Warning 1", statistics.Warnings);
            Assert.Contains("Warning 2", statistics.Warnings);
        }

        [Fact]
        public void BatchSpendFlushStatistics_WarningsProperty_CanBeNull()
        {
            // Act
            var statistics = new BatchSpendFlushStatistics
            {
                RedisKeysProcessed = 5,
                Warnings = null
            };

            // Assert
            Assert.Equal(5, statistics.RedisKeysProcessed);
            Assert.Null(statistics.Warnings);
        }

        [Fact]
        public void BatchSpendFlushStatistics_WarningsProperty_CanBeEmptyArray()
        {
            // Act
            var statistics = new BatchSpendFlushStatistics
            {
                RedisKeysProcessed = 3,
                Warnings = new string[0]
            };

            // Assert
            Assert.Equal(3, statistics.RedisKeysProcessed);
            Assert.NotNull(statistics.Warnings);
            Assert.Empty(statistics.Warnings);
        }

        #endregion

        #region Edge Cases and Validation Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void BatchSpendFlushRequestedEvent_RequestedBy_CanBeNullOrEmpty(string requestedBy)
        {
            // Act
            var batchEvent = new BatchSpendFlushRequestedEvent { RequestedBy = requestedBy };

            // Assert
            Assert.Equal(requestedBy, batchEvent.RequestedBy);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(300)]
        [InlineData(int.MaxValue)]
        public void BatchSpendFlushRequestedEvent_TimeoutSeconds_AcceptsAnyIntegerValue(int? timeout)
        {
            // Act
            var batchEvent = new BatchSpendFlushRequestedEvent { TimeoutSeconds = timeout };

            // Assert
            Assert.Equal(timeout, batchEvent.TimeoutSeconds);
        }

        [Fact]
        public void BatchSpendFlushCompletedEvent_Duration_CanBeNegative()
        {
            // This might happen in edge cases or testing scenarios
            // Act
            var completedEvent = new BatchSpendFlushCompletedEvent
            {
                Duration = TimeSpan.FromMilliseconds(-100)
            };

            // Assert
            Assert.Equal(TimeSpan.FromMilliseconds(-100), completedEvent.Duration);
        }

        [Theory]
        [InlineData(double.MinValue)]
        [InlineData(-1.0)]
        [InlineData(0.0)]
        [InlineData(1.5)]
        [InlineData(double.MaxValue)]
        public void BatchSpendFlushStatistics_TimingProperties_AcceptAnyDoubleValue(double value)
        {
            // Act
            var statistics = new BatchSpendFlushStatistics
            {
                RedisOperationMs = value,
                DatabaseOperationMs = value
            };

            // Assert
            Assert.Equal(value, statistics.RedisOperationMs);
            Assert.Equal(value, statistics.DatabaseOperationMs);
        }

        #endregion
    }
}
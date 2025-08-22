using ConduitLLM.Configuration.Options;

namespace ConduitLLM.Tests.Configuration.Options
{
    /// <summary>
    /// Focused unit tests for BatchSpendingOptions configuration validation
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "BatchSpendingOptions")]
    public class BatchSpendingOptionsTests
    {
        [Fact]
        public void DefaultValues_ShouldBeValid()
        {
            // Arrange & Act
            var options = new BatchSpendingOptions();
            
            // Assert
            Assert.Equal(30, options.FlushIntervalSeconds);
            Assert.Equal(1, options.MinimumIntervalSeconds);
            Assert.Equal(21600, options.MaximumIntervalSeconds); // 6 hours
            Assert.Equal(24, options.RedisTtlHours);
            
            // Should pass validation
            var validationResult = options.Validate();
            Assert.Null(validationResult);
        }

        [Fact]
        public void GetValidatedFlushInterval_WithValidValues_ShouldReturnCorrectTimeSpan()
        {
            // Arrange
            var options = new BatchSpendingOptions
            {
                FlushIntervalSeconds = 60,
                MinimumIntervalSeconds = 1,
                MaximumIntervalSeconds = 3600
            };
            
            // Act
            var result = options.GetValidatedFlushInterval();
            
            // Assert
            Assert.Equal(TimeSpan.FromSeconds(60), result);
        }

        [Fact]
        public void GetValidatedFlushInterval_WithTooLowValue_ShouldClampToMinimum()
        {
            // Arrange
            var options = new BatchSpendingOptions
            {
                FlushIntervalSeconds = 0, // Too low
                MinimumIntervalSeconds = 5,
                MaximumIntervalSeconds = 3600
            };
            
            // Act
            var result = options.GetValidatedFlushInterval();
            
            // Assert
            Assert.Equal(TimeSpan.FromSeconds(5), result); // Clamped to minimum
        }

        [Fact]
        public void GetValidatedFlushInterval_WithTooHighValue_ShouldClampToMaximum()
        {
            // Arrange
            var options = new BatchSpendingOptions
            {
                FlushIntervalSeconds = 7200, // 2 hours
                MinimumIntervalSeconds = 1,
                MaximumIntervalSeconds = 3600, // 1 hour max
                RedisTtlHours = 24
            };
            
            // Act
            var result = options.GetValidatedFlushInterval();
            
            // Assert
            Assert.Equal(TimeSpan.FromSeconds(3600), result); // Clamped to maximum
        }

        [Fact]
        public void GetValidatedFlushInterval_WithValueNearRedisTtl_ShouldClampToSafeValue()
        {
            // Arrange
            var options = new BatchSpendingOptions
            {
                FlushIntervalSeconds = 86400, // 24 hours (same as Redis TTL)
                MinimumIntervalSeconds = 1,
                MaximumIntervalSeconds = 86400,
                RedisTtlHours = 24
            };
            
            // Act
            var result = options.GetValidatedFlushInterval();
            
            // Assert
            var expectedMaxSafe = TimeSpan.FromHours(23); // 24 - 1 hour buffer
            Assert.Equal(expectedMaxSafe, result);
        }

        [Theory]
        [InlineData(1, 1, 3600, 24, true)]      // Valid configuration
        [InlineData(30, 1, 21600, 24, true)]   // Default configuration
        [InlineData(3600, 1, 3600, 24, true)]  // At maximum
        [InlineData(0, 1, 3600, 24, false)]    // Below minimum
        [InlineData(5, 10, 3600, 24, false)]   // Below minimum interval
        [InlineData(86400, 1, 86400, 24, false)] // At Redis TTL (unsafe)
        public void Validate_WithVariousConfigurations_ShouldReturnExpectedResult(
            int flushInterval, int minInterval, int maxInterval, int redisTtl, bool shouldBeValid)
        {
            // Arrange
            var options = new BatchSpendingOptions
            {
                FlushIntervalSeconds = flushInterval,
                MinimumIntervalSeconds = minInterval,
                MaximumIntervalSeconds = maxInterval,
                RedisTtlHours = redisTtl
            };
            
            // Act
            var validationResult = options.Validate();
            
            // Assert
            if (shouldBeValid)
            {
                Assert.Null(validationResult);
            }
            else
            {
                Assert.NotNull(validationResult);
                Assert.NotEmpty(validationResult.ErrorMessage);
            }
        }

        [Fact]
        public void Validate_FlushIntervalTooLow_ShouldReturnValidationError()
        {
            // Arrange
            var options = new BatchSpendingOptions
            {
                FlushIntervalSeconds = 0,
                MinimumIntervalSeconds = 1
            };
            
            // Act
            var result = options.Validate();
            
            // Assert
            Assert.NotNull(result);
            Assert.Contains("cannot be less than MinimumIntervalSeconds", result.ErrorMessage);
            Assert.Contains(nameof(BatchSpendingOptions.FlushIntervalSeconds), result.MemberNames);
        }

        [Fact]
        public void Validate_FlushIntervalTooHigh_ShouldReturnValidationError()
        {
            // Arrange
            var options = new BatchSpendingOptions
            {
                FlushIntervalSeconds = 7200, // 2 hours
                MaximumIntervalSeconds = 3600 // 1 hour max
            };
            
            // Act
            var result = options.Validate();
            
            // Assert
            Assert.NotNull(result);
            Assert.Contains("cannot be greater than MaximumIntervalSeconds", result.ErrorMessage);
            Assert.Contains(nameof(BatchSpendingOptions.FlushIntervalSeconds), result.MemberNames);
        }

        [Fact]
        public void Validate_FlushIntervalNearRedisTtl_ShouldReturnValidationError()
        {
            // Arrange
            var options = new BatchSpendingOptions
            {
                FlushIntervalSeconds = 86400, // 24 hours
                MaximumIntervalSeconds = 86400, // Allow this to pass max check
                RedisTtlHours = 24 // Same as flush interval
            };
            
            // Act
            var result = options.Validate();
            
            // Assert
            Assert.NotNull(result);
            Assert.Contains("prevent transaction loss", result.ErrorMessage);
            Assert.Contains(nameof(BatchSpendingOptions.FlushIntervalSeconds), result.MemberNames);
            Assert.Contains(nameof(BatchSpendingOptions.RedisTtlHours), result.MemberNames);
        }

        [Fact]
        public void GetRedisTtl_ShouldReturnCorrectTimeSpan()
        {
            // Arrange
            var options = new BatchSpendingOptions
            {
                RedisTtlHours = 48
            };
            
            // Act
            var result = options.GetRedisTtl();
            
            // Assert
            Assert.Equal(TimeSpan.FromHours(48), result);
        }

        [Fact]
        public void SectionName_ShouldBeCorrect()
        {
            // Act & Assert
            Assert.Equal("BatchSpending", BatchSpendingOptions.SectionName);
        }
    }
}
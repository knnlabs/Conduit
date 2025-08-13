using System;
using ConduitLLM.Core.Models;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class PerformanceMetricsServiceTests
    {
        [Fact]
        [Trait("Category", "TimingSensitive")]
        public void CalculateMetrics_NoUsageData_HandlesGracefully()
        {
            // Arrange
            var response = CreateTestResponse(usage: null);
            var elapsedTime = TimeSpan.FromSeconds(1);

            // Act
            var metrics = _service.CalculateMetrics(response, elapsedTime, "OpenAI", "gpt-4", false, 0);

            // Assert
            Assert.Equal(1000, metrics.TotalLatencyMs);
            Assert.Null(metrics.TokensPerSecond);
            Assert.Null(metrics.PromptTokensPerSecond);
            Assert.Null(metrics.CompletionTokensPerSecond);
        }

        [Fact]
        [Trait("Category", "TimingSensitive")]
        public void CalculateMetrics_ZeroElapsedTime_HandlesGracefully()
        {
            // Arrange
            var response = CreateTestResponse(usage: new Usage
            {
                PromptTokens = 100,
                CompletionTokens = 50,
                TotalTokens = 150
            });
            var elapsedTime = TimeSpan.Zero;

            // Act
            var metrics = _service.CalculateMetrics(response, elapsedTime, "OpenAI", "gpt-4", false, 0);

            // Assert
            Assert.Equal(0, metrics.TotalLatencyMs);
            Assert.Null(metrics.TokensPerSecond);
            Assert.Null(metrics.PromptTokensPerSecond);
        }

        [Fact]
        [Trait("Category", "TimingSensitive")]
        public void CalculateMetrics_ZeroCompletionTokens_HandlesGracefully()
        {
            // Arrange
            var response = CreateTestResponse(usage: new Usage
            {
                PromptTokens = 100,
                CompletionTokens = 0,
                TotalTokens = 100
            });
            var elapsedTime = TimeSpan.FromSeconds(1);

            // Act
            var metrics = _service.CalculateMetrics(response, elapsedTime, "OpenAI", "gpt-4", false, 0);

            // Assert
            Assert.Null(metrics.TokensPerSecond);
            Assert.NotNull(metrics.PromptTokensPerSecond);
        }
    }
}
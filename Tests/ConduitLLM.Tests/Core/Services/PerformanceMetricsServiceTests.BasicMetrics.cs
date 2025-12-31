using ConduitLLM.Core.Models;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class PerformanceMetricsServiceTests
    {
        [Fact]
        [Trait("Category", "TimingSensitive")]
        public void CalculateMetrics_BasicNonStreaming_ReturnsCorrectMetrics()
        {
            // Arrange
            var response = new ChatCompletionResponse
            {
                Id = "test-123",
                Model = "gpt-4",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Object = "chat.completion",
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new Message { Role = MessageRole.Assistant, Content = "Test response" },
                        FinishReason = FinishReason.Stop
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = 100,
                    CompletionTokens = 50,
                    TotalTokens = 150
                }
            };
            var elapsedTime = TimeSpan.FromSeconds(2);

            // Act
            var metrics = _service.CalculateMetrics(response, elapsedTime, "OpenAI", "gpt-4", false, 0);

            // Assert
            Assert.Equal(2000, metrics.TotalLatencyMs);
            Assert.Equal("OpenAI", metrics.Provider);
            Assert.Equal("gpt-4", metrics.Model);
            Assert.False(metrics.Streaming);
            Assert.Equal(0, metrics.RetryAttempts);
            Assert.NotNull(metrics.TokensPerSecond);
            Assert.Equal(25, metrics.TokensPerSecond); // 50 completion tokens / 2 seconds
        }

        [Fact]
        [Trait("Category", "TimingSensitive")]
        public void CalculateMetrics_StreamingResponse_CalculatesStreamingMetrics()
        {
            // Arrange
            var response = new ChatCompletionResponse
            {
                Id = "test-123",
                Model = "gpt-4",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Object = "chat.completion",
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new Message { Role = MessageRole.Assistant, Content = "Long streaming response" },
                        FinishReason = FinishReason.Stop
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = 100,
                    CompletionTokens = 200,
                    TotalTokens = 300
                }
            };
            var elapsedTime = TimeSpan.FromSeconds(5);

            // Act
            var metrics = _service.CalculateMetrics(response, elapsedTime, "OpenAI", "gpt-4", true, 0);

            // Assert
            Assert.True(metrics.Streaming);
            Assert.NotNull(metrics.TokensPerSecond);
            Assert.Equal(40, metrics.TokensPerSecond); // 200 completion tokens / 5 seconds
            Assert.NotNull(metrics.CompletionTokensPerSecond);
            Assert.True(metrics.CompletionTokensPerSecond > metrics.TokensPerSecond); // Should be higher due to 90% time allocation
        }

        [Fact]
        [Trait("Category", "TimingSensitive")]
        public void CalculateMetrics_WithRetryAttempts_RecordsRetries()
        {
            // Arrange
            var response = CreateTestResponse(usage: new Usage
            {
                PromptTokens = 10,
                CompletionTokens = 5,
                TotalTokens = 15
            });
            var elapsedTime = TimeSpan.FromSeconds(1);

            // Act
            var metrics = _service.CalculateMetrics(response, elapsedTime, "OpenAI", "gpt-4", false, 3);

            // Assert
            Assert.Equal(3, metrics.RetryAttempts);
        }

        [Fact]
        [Trait("Category", "TimingSensitive")]
        public void CalculateMetrics_PromptTokensPerSecond_CalculatesCorrectly()
        {
            // Arrange
            var response = CreateTestResponse(usage: new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 100,
                TotalTokens = 1100
            });
            var elapsedTime = TimeSpan.FromSeconds(10);

            // Act
            var metricsNonStreaming = _service.CalculateMetrics(response, elapsedTime, "OpenAI", "gpt-4", false, 0);
            var metricsStreaming = _service.CalculateMetrics(response, elapsedTime, "OpenAI", "gpt-4", true, 0);

            // Assert
            Assert.NotNull(metricsNonStreaming.PromptTokensPerSecond);
            Assert.NotNull(metricsStreaming.PromptTokensPerSecond);
            // Streaming should have higher prompt tokens/sec due to different time allocation
            Assert.True(metricsStreaming.PromptTokensPerSecond > metricsNonStreaming.PromptTokensPerSecond);
        }

        [Fact]
        [Trait("Category", "TimingSensitive")]
        public void CalculateMetrics_DifferentProviders_SetsCorrectly()
        {
            // Arrange
            var response = CreateTestResponse(model: "claude-3", usage: new Usage
            {
                PromptTokens = 100,
                CompletionTokens = 50,
                TotalTokens = 150
            });
            var elapsedTime = TimeSpan.FromSeconds(1);

            // Act
            var anthropicMetrics = _service.CalculateMetrics(response, elapsedTime, "Anthropic", "claude-3", false, 0);
            var openAiMetrics = _service.CalculateMetrics(response, elapsedTime, "OpenAI", "gpt-4", false, 0);

            // Assert
            Assert.Equal("Anthropic", anthropicMetrics.Provider);
            Assert.Equal("claude-3", anthropicMetrics.Model);
            Assert.Equal("OpenAI", openAiMetrics.Provider);
            Assert.Equal("gpt-4", openAiMetrics.Model);
        }

        [Theory]
        [Trait("Category", "TimingSensitive")]
        [InlineData(1, 10, 10)]
        [InlineData(2, 20, 10)]
        [InlineData(5, 50, 10)]
        [InlineData(10, 100, 10)]
        public void CalculateMetrics_VariousScenarios_CalculatesCorrectly(
            int elapsedSeconds, int completionTokens, double expectedTokensPerSecond)
        {
            // Arrange
            var response = CreateTestResponse(usage: new Usage
            {
                PromptTokens = 50,
                CompletionTokens = completionTokens,
                TotalTokens = 50 + completionTokens
            });
            var elapsedTime = TimeSpan.FromSeconds(elapsedSeconds);

            // Act
            var metrics = _service.CalculateMetrics(response, elapsedTime, "OpenAI", "gpt-4", false, 0);

            // Assert
            Assert.Equal(expectedTokensPerSecond, metrics.TokensPerSecond);
        }

        [Fact]
        [Trait("Category", "TimingSensitive")]
        public void CalculateMetrics_VeryLongElapsedTime_HandlesCorrectly()
        {
            // Arrange
            var response = CreateTestResponse(usage: new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 5000,
                TotalTokens = 6000
            });
            var elapsedTime = TimeSpan.FromMinutes(5); // 300 seconds

            // Act
            var metrics = _service.CalculateMetrics(response, elapsedTime, "OpenAI", "gpt-4", true, 0);

            // Assert
            Assert.Equal(300000, metrics.TotalLatencyMs);
            Assert.NotNull(metrics.TokensPerSecond);
            Assert.True(metrics.TokensPerSecond > 0);
            Assert.True(metrics.TokensPerSecond < 100); // Should be reasonable
        }
    }
}
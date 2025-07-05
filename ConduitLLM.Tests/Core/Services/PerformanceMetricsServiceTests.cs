using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public class PerformanceMetricsServiceTests
    {
        private readonly IPerformanceMetricsService _service;

        public PerformanceMetricsServiceTests()
        {
            _service = new PerformanceMetricsService();
        }

        private static ChatCompletionResponse CreateTestResponse(string model = "gpt-4", Usage? usage = null, List<Choice>? choices = null)
        {
            return new ChatCompletionResponse
            {
                Id = "test-123",
                Model = model,
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Object = "chat.completion",
                Choices = choices ?? new List<Choice>(),
                Usage = usage
            };
        }

        [Fact]
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

        [Fact]
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
        public void CreateStreamingTracker_CreatesValidTracker()
        {
            // Act
            var tracker = _service.CreateStreamingTracker("OpenAI", "gpt-4");

            // Assert
            Assert.NotNull(tracker);
            Assert.IsAssignableFrom<IStreamingMetricsTracker>(tracker);
        }

        [Fact]
        public void StreamingTracker_RecordFirstToken_RecordsTimeToFirstToken()
        {
            // Arrange
            var tracker = _service.CreateStreamingTracker("OpenAI", "gpt-4");
            Thread.Sleep(50);

            // Act
            tracker.RecordFirstToken();
            var metrics = tracker.GetMetrics();

            // Assert
            Assert.NotNull(metrics.TimeToFirstTokenMs);
            Assert.True(metrics.TimeToFirstTokenMs >= 50);
            Assert.Equal("OpenAI", metrics.Provider);
            Assert.Equal("gpt-4", metrics.Model);
            Assert.True(metrics.Streaming);
        }

        [Fact]
        public void StreamingTracker_RecordFirstToken_OnlyRecordsOnce()
        {
            // Arrange
            var tracker = _service.CreateStreamingTracker("OpenAI", "gpt-4");
            
            // Act
            tracker.RecordFirstToken();
            Thread.Sleep(100);
            tracker.RecordFirstToken(); // Should be ignored
            var metrics = tracker.GetMetrics();

            // Assert
            Assert.NotNull(metrics.TimeToFirstTokenMs);
            Assert.True(metrics.TimeToFirstTokenMs < 100);
        }

        [Fact]
        public void StreamingTracker_RecordToken_TracksTokens()
        {
            // Arrange
            var tracker = _service.CreateStreamingTracker("OpenAI", "gpt-4");
            tracker.RecordFirstToken();

            // Act
            for (int i = 0; i < 10; i++)
            {
                Thread.Sleep(10);
                tracker.RecordToken();
            }
            var metrics = tracker.GetMetrics();

            // Assert
            Assert.NotNull(metrics.TokensPerSecond);
            Assert.True(metrics.TokensPerSecond > 0);
        }

        [Fact]
        public void StreamingTracker_CalculatesInterTokenLatency()
        {
            // Arrange
            var tracker = _service.CreateStreamingTracker("OpenAI", "gpt-4");
            tracker.RecordFirstToken();

            // Act
            for (int i = 0; i < 5; i++)
            {
                Thread.Sleep(20);
                tracker.RecordToken();
            }
            var metrics = tracker.GetMetrics();

            // Assert
            Assert.NotNull(metrics.AvgInterTokenLatencyMs);
            Assert.True(metrics.AvgInterTokenLatencyMs >= 15); // Should be around 20ms
            Assert.True(metrics.AvgInterTokenLatencyMs <= 30);
        }

        [Fact]
        public void StreamingTracker_NoTokens_ReturnsBasicMetrics()
        {
            // Arrange
            var tracker = _service.CreateStreamingTracker("OpenAI", "gpt-4");

            // Act
            Thread.Sleep(10);
            var metrics = tracker.GetMetrics();

            // Assert
            Assert.True(metrics.TotalLatencyMs >= 10);
            Assert.Null(metrics.TimeToFirstTokenMs);
            Assert.Null(metrics.TokensPerSecond);
            Assert.Null(metrics.AvgInterTokenLatencyMs);
        }

        [Fact]
        public void StreamingTracker_WithUsageData_UsesActualTokenCounts()
        {
            // Arrange
            var tracker = _service.CreateStreamingTracker("OpenAI", "gpt-4");
            tracker.RecordFirstToken();
            tracker.RecordToken();
            tracker.RecordToken();

            var usage = new Usage
            {
                PromptTokens = 100,
                CompletionTokens = 50,
                TotalTokens = 150
            };

            // Act
            Thread.Sleep(100);
            var metrics = tracker.GetMetrics(usage);

            // Assert
            Assert.NotNull(metrics.TokensPerSecond);
            Assert.NotNull(metrics.CompletionTokensPerSecond);
            Assert.Equal(metrics.TokensPerSecond, metrics.CompletionTokensPerSecond);
            // Should use usage.CompletionTokens (50) not our count (3)
            Assert.True(metrics.TokensPerSecond > 100); // 50 tokens in ~100ms
        }

        [Fact]
        public void StreamingTracker_PromptTokensPerSecond_RequiresTimeToFirstToken()
        {
            // Arrange
            var tracker = _service.CreateStreamingTracker("OpenAI", "gpt-4");
            // Don't record first token
            
            var usage = new Usage
            {
                PromptTokens = 100,
                CompletionTokens = 50,
                TotalTokens = 150
            };

            // Act
            var metrics = tracker.GetMetrics(usage);

            // Assert
            Assert.Null(metrics.PromptTokensPerSecond);
            Assert.NotNull(metrics.TokensPerSecond); // Completion tokens should still work
        }

        [Fact]
        public void StreamingTracker_VeryFastTokenGeneration_HandlesHighThroughput()
        {
            // Arrange
            var tracker = _service.CreateStreamingTracker("OpenAI", "gpt-4");
            tracker.RecordFirstToken();

            // Act - Record many tokens very quickly
            for (int i = 0; i < 100; i++)
            {
                tracker.RecordToken();
            }
            Thread.Sleep(10); // Ensure some elapsed time
            var metrics = tracker.GetMetrics();

            // Assert
            Assert.NotNull(metrics.TokensPerSecond);
            Assert.True(metrics.TokensPerSecond > 1000); // Should be very high
        }

        [Theory]
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

        [Fact]
        public void StreamingTracker_SingleToken_NoInterTokenLatency()
        {
            // Arrange
            var tracker = _service.CreateStreamingTracker("OpenAI", "gpt-4");
            
            // Act
            tracker.RecordFirstToken();
            var metrics = tracker.GetMetrics();

            // Assert
            Assert.Null(metrics.AvgInterTokenLatencyMs);
        }

        [Fact]
        public void StreamingTracker_MultipleCalls_StopsTimerOnFirstGetMetrics()
        {
            // Arrange
            var tracker = _service.CreateStreamingTracker("OpenAI", "gpt-4");
            tracker.RecordFirstToken();

            // Act
            var firstMetrics = tracker.GetMetrics();
            Thread.Sleep(100); // Wait
            var secondMetrics = tracker.GetMetrics();

            // Assert
            Assert.Equal(firstMetrics.TotalLatencyMs, secondMetrics.TotalLatencyMs);
        }

        [Fact]
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

        [Fact(Skip = "StreamingMetricsTracker is not thread-safe by design - it's meant to be used from a single streaming context")]
        public async Task StreamingTracker_ConcurrentRecording_NotThreadSafe()
        {
            // This test documents that the tracker is not thread-safe
            // In real usage, tokens should be recorded from a single thread
            
            // Arrange
            var tracker = _service.CreateStreamingTracker("OpenAI", "gpt-4");
            tracker.RecordFirstToken();
            
            const int threadCount = 10;
            const int tokensPerThread = 100;
            var tasks = new Task[threadCount];

            // Act
            for (int i = 0; i < threadCount; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < tokensPerThread; j++)
                    {
                        tracker.RecordToken();
                    }
                });
            }
            
            await Task.WhenAll(tasks);
            var metrics = tracker.GetMetrics();

            // Assert
            // The actual token count might not match expected due to race conditions
            // This is expected behavior - the tracker is designed for single-threaded use
            Assert.NotNull(metrics.TokensPerSecond);
        }
    }
}
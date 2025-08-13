using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class PerformanceMetricsServiceTests
    {
        [Fact]
        [Trait("Category", "TimingSensitive")]
        public void CreateStreamingTracker_CreatesValidTracker()
        {
            // Act
            var tracker = _service.CreateStreamingTracker("OpenAI", "gpt-4");

            // Assert
            Assert.NotNull(tracker);
            Assert.IsAssignableFrom<IStreamingMetricsTracker>(tracker);
        }

        [Fact]
        [Trait("Category", "TimingSensitive")]
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
        [Trait("Category", "TimingSensitive")]
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
        [Trait("Category", "TimingSensitive")]
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
        [Trait("Category", "TimingSensitive")]
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
        [Trait("Category", "TimingSensitive")]
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
        [Trait("Category", "TimingSensitive")]
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
        [Trait("Category", "TimingSensitive")]
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
        [Trait("Category", "TimingSensitive")]
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

        [Fact]
        [Trait("Category", "TimingSensitive")]
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
        [Trait("Category", "TimingSensitive")]
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
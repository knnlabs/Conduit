using System.Diagnostics;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Service for calculating performance metrics for LLM operations.
    /// </summary>
    public class PerformanceMetricsService : IPerformanceMetricsService
    {
        /// <summary>
        /// Calculates performance metrics for a completed chat completion.
        /// </summary>
        public PerformanceMetrics CalculateMetrics(
            ChatCompletionResponse response,
            TimeSpan elapsedTime,
            string provider,
            string model,
            bool streaming = false,
            int retryAttempts = 0)
        {
            var metrics = new PerformanceMetrics
            {
                TotalLatencyMs = (long)elapsedTime.TotalMilliseconds,
                Provider = provider,
                Model = model,
                Streaming = streaming,
                RetryAttempts = retryAttempts
            };

            // Calculate tokens per second if usage data is available
            if (response.Usage != null && elapsedTime.TotalSeconds > 0)
            {
                var totalSeconds = elapsedTime.TotalSeconds;
                
                // Overall tokens per second (based on completion tokens)
                if (response.Usage.CompletionTokens > 0)
                {
                    metrics.TokensPerSecond = response.Usage.CompletionTokens / totalSeconds;
                }

                // Prompt processing speed (estimate based on total time)
                // Note: This is an approximation since we don't have separate timing for prompt processing
                if (response.Usage.PromptTokens > 0)
                {
                    // Assume prompt processing takes a small fraction of total time for non-streaming
                    // This could be refined with provider-specific data
                    var promptProcessingTime = streaming ? totalSeconds * 0.1 : totalSeconds * 0.3;
                    if (promptProcessingTime > 0)
                    {
                        metrics.PromptTokensPerSecond = response.Usage.PromptTokens / promptProcessingTime;
                    }
                }

                // Completion generation speed
                if (response.Usage.CompletionTokens > 0)
                {
                    if (streaming)
                    {
                        // For streaming, most of the time is spent generating tokens
                        var generationTime = totalSeconds * 0.9; // Assume 90% of time is generation
                        metrics.CompletionTokensPerSecond = response.Usage.CompletionTokens / generationTime;
                    }
                    else
                    {
                        // For non-streaming, assume 70% of time is generation (30% prompt processing)
                        var generationTime = totalSeconds * 0.7;
                        metrics.CompletionTokensPerSecond = response.Usage.CompletionTokens / generationTime;
                    }
                }
            }

            return metrics;
        }

        /// <summary>
        /// Creates a streaming metrics tracker.
        /// </summary>
        public IStreamingMetricsTracker CreateStreamingTracker(string provider, string model)
        {
            return new StreamingMetricsTracker(provider, model);
        }

        /// <summary>
        /// Implementation of streaming metrics tracker.
        /// </summary>
        private class StreamingMetricsTracker : IStreamingMetricsTracker
        {
            private readonly Stopwatch _stopwatch;
            private readonly string _provider;
            private readonly string _model;
            private long? _timeToFirstTokenMs;
            private int _tokenCount;
            private readonly List<long> _interTokenLatencies;
            private long _lastTokenTime;

            public StreamingMetricsTracker(string provider, string model)
            {
                _provider = provider;
                _model = model;
                _stopwatch = Stopwatch.StartNew();
                _interTokenLatencies = new List<long>();
                _tokenCount = 0;
            }

            public void RecordFirstToken()
            {
                if (!_timeToFirstTokenMs.HasValue)
                {
                    _timeToFirstTokenMs = _stopwatch.ElapsedMilliseconds;
                    _lastTokenTime = _stopwatch.ElapsedMilliseconds;
                    _tokenCount = 1;
                }
            }

            public void RecordToken()
            {
                var currentTime = _stopwatch.ElapsedMilliseconds;
                if (_lastTokenTime > 0)
                {
                    _interTokenLatencies.Add(currentTime - _lastTokenTime);
                }
                _lastTokenTime = currentTime;
                _tokenCount++;
            }

            public PerformanceMetrics GetMetrics(Usage? usage = null)
            {
                _stopwatch.Stop();
                
                var metrics = new PerformanceMetrics
                {
                    TotalLatencyMs = _stopwatch.ElapsedMilliseconds,
                    TimeToFirstTokenMs = _timeToFirstTokenMs,
                    Provider = _provider,
                    Model = _model,
                    Streaming = true
                };

                // Calculate average inter-token latency
                if (_interTokenLatencies.Count > 0)
                {
                    metrics.AvgInterTokenLatencyMs = _interTokenLatencies.Average();
                }

                // Calculate tokens per second
                var totalSeconds = _stopwatch.Elapsed.TotalSeconds;
                if (totalSeconds > 0)
                {
                    if (usage?.CompletionTokens != null && usage.CompletionTokens > 0)
                    {
                        // Use actual token count from usage if available
                        metrics.TokensPerSecond = usage.CompletionTokens / totalSeconds;
                        
                        // For CompletionTokensPerSecond, exclude prompt processing time
                        // Generation time = total time - prompt processing time
                        if (_timeToFirstTokenMs.HasValue)
                        {
                            var generationSeconds = totalSeconds - (_timeToFirstTokenMs.Value / 1000.0);
                            if (generationSeconds > 0)
                            {
                                metrics.CompletionTokensPerSecond = usage.CompletionTokens / generationSeconds;
                            }
                            else
                            {
                                // Fallback if generation time is too small
                                metrics.CompletionTokensPerSecond = usage.CompletionTokens / totalSeconds;
                            }
                        }
                        else
                        {
                            // No time to first token recorded, use total time
                            metrics.CompletionTokensPerSecond = usage.CompletionTokens / totalSeconds;
                        }
                    }
                    else if (_tokenCount > 0)
                    {
                        // Fall back to our counted tokens
                        metrics.TokensPerSecond = _tokenCount / totalSeconds;
                    }

                    // Estimate prompt processing speed if usage data is available
                    if (usage?.PromptTokens != null && usage.PromptTokens > 0 && _timeToFirstTokenMs.HasValue)
                    {
                        var promptProcessingSeconds = _timeToFirstTokenMs.Value / 1000.0;
                        if (promptProcessingSeconds > 0)
                        {
                            metrics.PromptTokensPerSecond = usage.PromptTokens / promptProcessingSeconds;
                        }
                    }
                }

                return metrics;
            }
        }
    }
}
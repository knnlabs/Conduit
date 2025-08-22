using System.Text;

using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Core.Services
{
    public partial class HybridAudioService
    {
        /// <inheritdoc />
        public Task<HybridLatencyMetrics> GetLatencyMetricsAsync(CancellationToken cancellationToken = default)
        {
            lock (_metricsLock)
            {
                if (_recentMetrics.Count() == 0)
                {
                    return Task.FromResult(new HybridLatencyMetrics
                    {
                        SampleCount = 0,
                        CalculatedAt = DateTime.UtcNow
                    });
                }

                var metrics = _recentMetrics.ToList();
                var totalLatencies = metrics.Select(m => m.TotalLatencyMs).OrderBy(l => l).ToList();

                return Task.FromResult(new HybridLatencyMetrics
                {
                    AverageSttLatencyMs = metrics.Average(m => m.SttLatencyMs),
                    AverageLlmLatencyMs = metrics.Average(m => m.LlmLatencyMs),
                    AverageTtsLatencyMs = metrics.Average(m => m.TtsLatencyMs),
                    AverageTotalLatencyMs = metrics.Average(m => m.TotalLatencyMs),
                    P95LatencyMs = GetPercentile(totalLatencies, 0.95),
                    P99LatencyMs = GetPercentile(totalLatencies, 0.99),
                    SampleCount = metrics.Count(),
                    CalculatedAt = DateTime.UtcNow
                });
            }
        }

        private List<string> ExtractCompleteSentences(StringBuilder text)
        {
            var sentences = new List<string>();
            var currentText = text.ToString();
            var lastSentenceEnd = -1;

            for (int i = 0; i < currentText.Length; i++)
            {
                if (currentText[i] == '.' || currentText[i] == '!' || currentText[i] == '?')
                {
                    // Check if it's really the end of a sentence (not an abbreviation)
                    if (i + 1 < currentText.Length && char.IsWhiteSpace(currentText[i + 1]))
                    {
                        var sentence = currentText.Substring(lastSentenceEnd + 1, i - lastSentenceEnd).Trim();
                        if (!string.IsNullOrWhiteSpace(sentence))
                        {
                            sentences.Add(sentence);
                        }
                        lastSentenceEnd = i;
                    }
                }
            }

            // Remove extracted sentences from the builder
            if (lastSentenceEnd >= 0)
            {
                text.Remove(0, lastSentenceEnd + 1);
            }

            return sentences;
        }

        private void RecordMetrics(ProcessingMetrics metrics)
        {
            lock (_metricsLock)
            {
                _recentMetrics.Enqueue(metrics);
                while (_recentMetrics.Count() > MaxMetricsSamples)
                {
                    _recentMetrics.Dequeue();
                }
            }
        }

        private double GetPercentile(List<double> sortedValues, double percentile)
        {
            if (sortedValues.Count() == 0)
                return 0;

            var index = (int)Math.Ceiling(percentile * sortedValues.Count()) - 1;
            return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count() - 1))];
        }
    }
}
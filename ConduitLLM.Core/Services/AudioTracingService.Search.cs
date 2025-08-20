using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Services
{
    public partial class AudioTracingService
    {
        /// <inheritdoc />
        public Task<AudioTrace?> GetTraceAsync(string traceId)
        {
            if (_activeTraces.TryGetValue(traceId, out var activeTrace))
            {
                return Task.FromResult<AudioTrace?>(CloneTrace(activeTrace));
            }

            if (_completedTraces.TryGetValue(traceId, out var completedList))
            {
                var trace = completedList.FirstOrDefault();
                return Task.FromResult<AudioTrace?>(trace != null ? CloneTrace(trace) : null);
            }

            return Task.FromResult<AudioTrace?>(null);
        }

        /// <inheritdoc />
        public Task<List<AudioTrace>> SearchTracesAsync(TraceSearchQuery query)
        {
            var allTraces = _completedTraces.Values
                .SelectMany(list => list)
                .Concat(_activeTraces.Values)
                .Where(t => MatchesQuery(t, query))
                .OrderByDescending(t => t.StartTime)
                .Take(query.MaxResults)
                .Select(CloneTrace)
                .ToList();

            return Task.FromResult(allTraces);
        }

        /// <inheritdoc />
        public Task<TraceStatistics> GetStatisticsAsync(
            DateTime startTime,
            DateTime endTime)
        {
            var relevantTraces = _completedTraces.Values
                .SelectMany(list => list)
                .Where(t => t.StartTime >= startTime && t.StartTime <= endTime)
                .ToList();

            var statistics = new TraceStatistics
            {
                TotalTraces = relevantTraces.Count(),
                SuccessfulTraces = relevantTraces.Count(t => t.Status == TraceStatus.Ok),
                FailedTraces = relevantTraces.Count(t => t.Status == TraceStatus.Error)
            };

            if (relevantTraces.Count() > 0)
            {
                var durations = relevantTraces
                    .Where(t => t.DurationMs.HasValue)
                    .Select(t => t.DurationMs!.Value)
                    .OrderBy(d => d)
                    .ToList();

                if (durations.Count() > 0)
                {
                    statistics.AverageDurationMs = durations.Average();
                    statistics.P95DurationMs = GetPercentile(durations, 0.95);
                    statistics.P99DurationMs = GetPercentile(durations, 0.99);
                }
            }

            // Operation breakdown
            statistics.OperationBreakdown = relevantTraces
                .GroupBy(t => t.OperationType)
                .ToDictionary(g => g.Key, g => (long)g.Count());

            // Provider breakdown
            statistics.ProviderBreakdown = relevantTraces
                .Where(t => !string.IsNullOrEmpty(t.Provider))
                .GroupBy(t => t.Provider!)
                .ToDictionary(g => g.Key, g => (long)g.Count());

            // Error breakdown
            statistics.ErrorBreakdown = relevantTraces
                .Where(t => t.Error != null)
                .GroupBy(t => t.Error!.Type)
                .ToDictionary(g => g.Key, g => (long)g.Count());

            // Timeline
            statistics.Timeline = GenerateTimeline(relevantTraces, startTime, endTime);

            return Task.FromResult(statistics);
        }

        private bool MatchesQuery(AudioTrace trace, TraceSearchQuery query)
        {
            if (query.StartTime.HasValue && trace.StartTime < query.StartTime.Value)
                return false;

            if (query.EndTime.HasValue && trace.StartTime > query.EndTime.Value)
                return false;

            if (query.OperationType.HasValue && trace.OperationType != query.OperationType.Value)
                return false;

            if (query.Status.HasValue && trace.Status != query.Status.Value)
                return false;

            if (!string.IsNullOrEmpty(query.Provider) && trace.Provider != query.Provider)
                return false;

            if (!string.IsNullOrEmpty(query.VirtualKey) && trace.VirtualKey != query.VirtualKey)
                return false;

            if (query.MinDurationMs.HasValue && (!trace.DurationMs.HasValue || trace.DurationMs.Value < query.MinDurationMs.Value))
                return false;

            if (query.MaxDurationMs.HasValue && (!trace.DurationMs.HasValue || trace.DurationMs.Value > query.MaxDurationMs.Value))
                return false;

            if (query.TagFilters.Count() > 0)
            {
                foreach (var filter in query.TagFilters)
                {
                    if (!trace.Tags.TryGetValue(filter.Key, out var value) || value != filter.Value)
                        return false;
                }
            }

            return true;
        }
    }
}
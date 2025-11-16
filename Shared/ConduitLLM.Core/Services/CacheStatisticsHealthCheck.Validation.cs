using System.Diagnostics;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Cache statistics health check - Accuracy validation functionality
    /// </summary>
    public partial class CacheStatisticsHealthCheck
    {
        private async Task<StatisticsAccuracyReport> PerformAccuracyValidationAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var report = new StatisticsAccuracyReport
            {
                CheckTimestamp = DateTime.UtcNow,
                IsAccurate = true
            };

            try
            {
                foreach (CacheRegion region in Enum.GetValues<CacheRegion>())
                {
                    // Get aggregated statistics
                    var aggregated = await _statisticsCollector.GetAggregatedStatisticsAsync(region, cancellationToken);
                    
                    // Get per-instance statistics
                    var perInstance = await _statisticsCollector.GetPerInstanceStatisticsAsync(region, cancellationToken);
                    
                    if (perInstance.Count() == 0) continue;

                    // Validate hit count
                    var sumHitCount = perInstance.Sum(kvp => kvp.Value.HitCount);
                    if (Math.Abs(sumHitCount - aggregated.HitCount) > 0)
                    {
                        var drift = CalculateDriftPercentage(aggregated.HitCount, sumHitCount);
                        if (drift > _alertThresholds.MaxDriftPercentage)
                        {
                            report.IsAccurate = false;
                            report.Discrepancies.Add(new RegionDiscrepancy
                            {
                                Region = region,
                                Type = DiscrepancyType.CountMismatch,
                                ExpectedValue = sumHitCount,
                                ActualValue = aggregated.HitCount,
                                DriftPercentage = drift,
                                AffectedInstances = perInstance.Keys.ToList()
                            });
                        }
                        report.MaxDriftPercentage = Math.Max(report.MaxDriftPercentage, drift);
                    }

                    // Validate miss count
                    var sumMissCount = perInstance.Sum(kvp => kvp.Value.MissCount);
                    if (Math.Abs(sumMissCount - aggregated.MissCount) > 0)
                    {
                        var drift = CalculateDriftPercentage(aggregated.MissCount, sumMissCount);
                        if (drift > _alertThresholds.MaxDriftPercentage)
                        {
                            report.IsAccurate = false;
                            report.Discrepancies.Add(new RegionDiscrepancy
                            {
                                Region = region,
                                Type = DiscrepancyType.CountMismatch,
                                ExpectedValue = sumMissCount,
                                ActualValue = aggregated.MissCount,
                                DriftPercentage = drift,
                                AffectedInstances = perInstance.Keys.ToList()
                            });
                        }
                        report.MaxDriftPercentage = Math.Max(report.MaxDriftPercentage, drift);
                    }

                    // Check for instances with suspiciously high variance
                    var avgHitCount = perInstance.Count() > 0 ? perInstance.Average(kvp => kvp.Value.HitCount) : 0;
                    var outliers = perInstance
                        .Where(kvp => Math.Abs(kvp.Value.HitCount - avgHitCount) > avgHitCount * 0.5) // 50% variance
                        .Select(kvp => kvp.Key)
                        .ToList();

                    if (outliers.Count() > 0)
                    {
                        report.InconsistentInstances.AddRange(outliers);
                    }
                }

                report.CheckDuration = stopwatch.Elapsed;
                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating statistics accuracy");
                report.IsAccurate = false;
                report.CheckDuration = stopwatch.Elapsed;
                return report;
            }
        }

        private async Task ProcessAccuracyReport(StatisticsAccuracyReport report)
        {
            if (!report.IsAccurate)
            {
                foreach (var discrepancy in report.Discrepancies)
                {
                    await TriggerAlertAsync(new StatisticsMonitoringAlert
                    {
                        Type = StatisticsAlertType.StatisticsDrift,
                        Severity = AlertSeverity.Warning,
                        Message = $"Statistics drift detected in {discrepancy.Region}: {discrepancy.DriftPercentage:F1}%",
                        TriggeredAt = DateTime.UtcNow,
                        CurrentValue = discrepancy.DriftPercentage,
                        ThresholdValue = _alertThresholds.MaxDriftPercentage,
                        Context = new Dictionary<string, object>
                        {
                            ["Region"] = discrepancy.Region.ToString(),
                            ["Expected"] = discrepancy.ExpectedValue,
                            ["Actual"] = discrepancy.ActualValue,
                            ["AffectedInstances"] = string.Join(", ", discrepancy.AffectedInstances)
                        }
                    });
                }
            }
        }

        private double CalculateDriftPercentage(long expected, long actual)
        {
            if (expected == 0) return actual > 0 ? 100.0 : 0.0;
            return Math.Abs((double)(actual - expected) / expected) * 100.0;
        }
    }
}
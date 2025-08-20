using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Costs;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Unified analytics service - Helper Methods
    /// </summary>
    public partial class AnalyticsService
    {
        #region Private Helper Methods

        private static LogRequestDto MapToLogRequestDto(RequestLog log)
        {
            return new LogRequestDto
            {
                Id = log.Id,
                VirtualKeyId = log.VirtualKeyId,
                ModelName = log.ModelName,
                RequestType = log.RequestType,
                InputTokens = log.InputTokens,
                OutputTokens = log.OutputTokens,
                Cost = log.Cost,
                ResponseTimeMs = log.ResponseTimeMs,
                UserId = log.UserId,
                ClientIp = log.ClientIp,
                RequestPath = log.RequestPath,
                StatusCode = log.StatusCode,
                Timestamp = log.Timestamp
            };
        }

        private static string NormalizeTimeframe(string timeframe)
        {
            return timeframe.ToLower() switch
            {
                "daily" => "daily",
                "weekly" => "weekly",
                "monthly" => "monthly",
                _ => "daily"
            };
        }

        private static List<(DateTime Date, decimal Cost)> CalculateDailyCosts(IEnumerable<RequestLog> logs)
        {
            return logs
                .GroupBy(l => l.Timestamp.Date)
                .Select(g => (Date: g.Key, Cost: g.Sum(l => l.Cost)))
                .OrderBy(d => d.Date)
                .ToList();
        }

        private static List<ModelCostDetail> CalculateModelBreakdown(IEnumerable<RequestLog> logs)
        {
            return logs
                .GroupBy(l => l.ModelName)
                .Select(g => new ModelCostDetail
                {
                    ModelName = g.Key,
                    TotalCost = g.Sum(l => l.Cost),
                    RequestCount = g.Count(),
                    InputTokens = g.Sum(l => (long)l.InputTokens),
                    OutputTokens = g.Sum(l => (long)l.OutputTokens),
                    AverageCostPerRequest = g.Average(l => l.Cost),
                    CostPercentage = 0 // Will be calculated later
                })
                .OrderByDescending(m => m.TotalCost)
                .ToList();
        }

        private static List<ProviderCostDetail> CalculateProviderBreakdown(IEnumerable<RequestLog> logs)
        {
            return logs
                .GroupBy(l => ExtractProviderFromModel(l.ModelName))
                .Select(g => new ProviderCostDetail
                {
                    ProviderName = g.Key,
                    TotalCost = g.Sum(l => l.Cost),
                    RequestCount = g.Count(),
                    AverageCostPerRequest = g.Average(l => l.Cost),
                    CostPercentage = 0 // Will be calculated later
                })
                .OrderByDescending(p => p.TotalCost)
                .ToList();
        }

        private static List<VirtualKeyCostDetail> CalculateVirtualKeyBreakdown(IEnumerable<RequestLog> logs)
        {
            return logs
                .GroupBy(l => l.VirtualKeyId)
                .Select(g => new VirtualKeyCostDetail
                {
                    VirtualKeyId = g.Key,
                    KeyName = $"Key #{g.Key}", // Will be enriched with actual name
                    TotalCost = g.Sum(l => l.Cost),
                    RequestCount = g.Count(),
                    AverageCostPerRequest = g.Average(l => l.Cost),
                    LastUsed = g.Max(l => l.Timestamp),
                    UniqueModels = g.Select(l => l.ModelName).Distinct().Count()
                })
                .OrderByDescending(v => v.TotalCost)
                .ToList();
        }

        private static string ExtractProviderFromModel(string modelName)
        {
            // Extract provider from model name (e.g., "openai/gpt-4" -> "openai")
            var parts = modelName.Split('/');
            return parts.Length > 1 ? parts[0] : "unknown";
        }

        private static decimal CalculateLast24HoursCost(IEnumerable<RequestLog> logs)
        {
            var cutoff = DateTime.UtcNow.AddDays(-1);
            return logs.Where(l => l.Timestamp >= cutoff).Sum(l => l.Cost);
        }

        private static decimal CalculateLast7DaysCost(IEnumerable<RequestLog> logs)
        {
            var cutoff = DateTime.UtcNow.AddDays(-7);
            return logs.Where(l => l.Timestamp >= cutoff).Sum(l => l.Cost);
        }

        private static decimal CalculateLast30DaysCost(IEnumerable<RequestLog> logs)
        {
            var cutoff = DateTime.UtcNow.AddDays(-30);
            return logs.Where(l => l.Timestamp >= cutoff).Sum(l => l.Cost);
        }

        private static decimal CalculateAverageDailyCost(List<(DateTime Date, decimal Cost)> dailyCosts)
        {
            return dailyCosts.Any() ? dailyCosts.Average(d => d.Cost) : 0;
        }

        private static List<(DateTime Date, decimal Cost)> AggregateByTimeframe(
            List<(DateTime Date, decimal Cost)> dailyCosts,
            string timeframe)
        {
            return timeframe switch
            {
                "weekly" => AggregateByWeek(dailyCosts),
                "monthly" => AggregateByMonth(dailyCosts),
                _ => dailyCosts
            };
        }

        private static List<(DateTime Date, decimal Cost)> AggregateByWeek(List<(DateTime Date, decimal Cost)> dailyCosts)
        {
            return dailyCosts
                .GroupBy(d => GetStartOfWeek(d.Date))
                .Select(g => (Date: g.Key, Cost: g.Sum(d => d.Cost)))
                .OrderBy(w => w.Date)
                .ToList();
        }

        private static List<(DateTime Date, decimal Cost)> AggregateByMonth(List<(DateTime Date, decimal Cost)> dailyCosts)
        {
            return dailyCosts
                .GroupBy(d => new DateTime(d.Date.Year, d.Date.Month, 1))
                .Select(g => (Date: g.Key, Cost: g.Sum(d => d.Cost)))
                .OrderBy(m => m.Date)
                .ToList();
        }

        private static DateTime GetStartOfWeek(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-1 * diff).Date;
        }

        private List<CostTrendPoint> CalculateCostTrends(IEnumerable<RequestLog> logs, string period)
        {
            var grouped = period switch
            {
                "weekly" => logs.GroupBy(l => GetStartOfWeek(l.Timestamp.Date)),
                "monthly" => logs.GroupBy(l => new DateTime(l.Timestamp.Year, l.Timestamp.Month, 1)),
                _ => logs.GroupBy(l => l.Timestamp.Date)
            };

            return grouped
                .Select(g => new CostTrendPoint
                {
                    Date = g.Key,
                    Cost = g.Sum(l => l.Cost),
                    RequestCount = g.Count(),
                    AverageRequestCost = g.Average(l => l.Cost)
                })
                .OrderBy(t => t.Date)
                .ToList();
        }

        private List<DailyStatistics> CalculateDailyStatistics(IEnumerable<RequestLog> logs, string timeframe)
        {
            var grouped = timeframe switch
            {
                "weekly" => logs.GroupBy(l => GetStartOfWeek(l.Timestamp.Date)),
                "monthly" => logs.GroupBy(l => new DateTime(l.Timestamp.Year, l.Timestamp.Month, 1)),
                _ => logs.GroupBy(l => l.Timestamp.Date)
            };

            return grouped
                .Select(g => new DailyStatistics
                {
                    Date = g.Key,
                    RequestCount = g.Count(),
                    Cost = g.Sum(l => l.Cost),
                    InputTokens = g.Sum(l => (long)l.InputTokens),
                    OutputTokens = g.Sum(l => (long)l.OutputTokens),
                    AverageResponseTime = g.Average(l => l.ResponseTimeMs),
                    ErrorCount = g.Count(l => l.StatusCode >= 400)
                })
                .OrderBy(s => s.Date)
                .ToList();
        }

        private async Task<PeriodComparison> CalculatePreviousPeriodComparison(DateTime startDate, DateTime endDate)
        {
            var periodLength = endDate - startDate;
            var previousStart = startDate - periodLength;
            var previousEnd = startDate;

            var currentLogs = await _requestLogRepository.GetByDateRangeAsync(startDate, endDate);
            var previousLogs = await _requestLogRepository.GetByDateRangeAsync(previousStart, previousEnd);

            var currentCost = currentLogs.Sum(l => l.Cost);
            var previousCost = previousLogs.Sum(l => l.Cost);
            var currentRequests = currentLogs.Count;
            var previousRequests = previousLogs.Count;

            return new PeriodComparison
            {
                CostChange = currentCost - previousCost,
                CostChangePercentage = previousCost > 0 ? ((currentCost - previousCost) / previousCost * 100) : 0,
                RequestChange = currentRequests - previousRequests,
                RequestChangePercentage = previousRequests > 0 ? ((decimal)(currentRequests - previousRequests) / previousRequests * 100) : 0,
                ResponseTimeChange = currentLogs.Any() && previousLogs.Any() 
                    ? currentLogs.Average(l => l.ResponseTimeMs) - previousLogs.Average(l => l.ResponseTimeMs) 
                    : 0,
                ErrorRateChange = CalculateErrorRateChange(currentLogs, previousLogs)
            };
        }

        private static double CalculateErrorRateChange(IList<RequestLog> currentLogs, IList<RequestLog> previousLogs)
        {
            var currentErrorRate = currentLogs.Any() 
                ? currentLogs.Count(l => l.StatusCode >= 400) * 100.0 / currentLogs.Count 
                : 0;
            var previousErrorRate = previousLogs.Any() 
                ? previousLogs.Count(l => l.StatusCode >= 400) * 100.0 / previousLogs.Count 
                : 0;
            return currentErrorRate - previousErrorRate;
        }

        private static byte[] ExportToCsv(IList<RequestLog> logs)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Timestamp,VirtualKeyId,Model,RequestType,InputTokens,OutputTokens,Cost,ResponseTime,StatusCode");
            
            foreach (var log in logs)
            {
                csv.AppendLine($"{log.Timestamp:yyyy-MM-dd HH:mm:ss},{log.VirtualKeyId},{log.ModelName},{log.RequestType}," +
                              $"{log.InputTokens},{log.OutputTokens},{log.Cost:F6},{log.ResponseTimeMs:F2},{log.StatusCode}");
            }
            
            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        private static byte[] ExportToJson(IList<RequestLog> logs)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(logs.Select(MapToLogRequestDto), new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            return Encoding.UTF8.GetBytes(json);
        }

        private class CostTrendPoint
        {
            public DateTime Date { get; set; }
            public decimal Cost { get; set; }
            public int RequestCount { get; set; }
            public decimal AverageRequestCost { get; set; }
        }

        #endregion
    }
}
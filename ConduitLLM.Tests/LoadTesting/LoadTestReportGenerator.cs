using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ConduitLLM.Tests.LoadTesting
{
    /// <summary>
    /// Generates detailed reports from load test results.
    /// </summary>
    public class LoadTestReportGenerator
    {
        /// <summary>
        /// Generates an HTML report from load test results.
        /// </summary>
        public static string GenerateHtmlReport(string testName, List<LoadTestResult> results, Dictionary<string, object>? metadata = null)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine($"<title>Load Test Report - {testName}</title>");
            sb.AppendLine("<style>");
            sb.AppendLine(GetCssStyles());
            sb.AppendLine("</style>");
            sb.AppendLine("<script src=\"https://cdn.jsdelivr.net/npm/chart.js\"></script>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            
            // Header
            sb.AppendLine($"<h1>Load Test Report: {testName}</h1>");
            sb.AppendLine($"<p class='timestamp'>Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>");
            
            // Metadata
            if (metadata != null && metadata.Any())
            {
                sb.AppendLine("<div class='metadata'>");
                sb.AppendLine("<h2>Test Configuration</h2>");
                sb.AppendLine("<table>");
                foreach (var kvp in metadata)
                {
                    sb.AppendLine($"<tr><td class='label'>{kvp.Key}:</td><td>{kvp.Value}</td></tr>");
                }
                sb.AppendLine("</table>");
                sb.AppendLine("</div>");
            }
            
            // Summary
            if (results.Any())
            {
                var totalOps = results.Sum(r => r.TotalOperations);
                var totalDuration = results.Sum(r => r.Duration.TotalSeconds);
                var avgThroughput = totalOps / totalDuration;
                var avgErrorRate = results.Average(r => r.ErrorRate);
                
                sb.AppendLine("<div class='summary'>");
                sb.AppendLine("<h2>Summary</h2>");
                sb.AppendLine("<div class='summary-cards'>");
                sb.AppendLine($"<div class='card'><h3>Total Operations</h3><p class='value'>{totalOps:N0}</p></div>");
                sb.AppendLine($"<div class='card'><h3>Avg Throughput</h3><p class='value'>{avgThroughput:F1} ops/sec</p></div>");
                sb.AppendLine($"<div class='card'><h3>Avg Error Rate</h3><p class='value {(avgErrorRate > 0.05 ? "error" : "success")}'>{avgErrorRate:P1}</p></div>");
                sb.AppendLine($"<div class='card'><h3>Total Duration</h3><p class='value'>{TimeSpan.FromSeconds(totalDuration):hh\\:mm\\:ss}</p></div>");
                sb.AppendLine("</div>");
                sb.AppendLine("</div>");
            }
            
            // Detailed Results
            sb.AppendLine("<div class='results'>");
            sb.AppendLine("<h2>Detailed Results</h2>");
            
            foreach (var result in results)
            {
                sb.AppendLine("<div class='test-result'>");
                sb.AppendLine($"<h3>Test Run {results.IndexOf(result) + 1}</h3>");
                
                // Metrics table
                sb.AppendLine("<table class='metrics-table'>");
                sb.AppendLine("<thead>");
                sb.AppendLine("<tr>");
                sb.AppendLine("<th>Operation</th>");
                sb.AppendLine("<th>Total</th>");
                sb.AppendLine("<th>Success</th>");
                sb.AppendLine("<th>Failed</th>");
                sb.AppendLine("<th>Success Rate</th>");
                sb.AppendLine("<th>Avg Latency</th>");
                sb.AppendLine("<th>P50</th>");
                sb.AppendLine("<th>P95</th>");
                sb.AppendLine("<th>P99</th>");
                sb.AppendLine("</tr>");
                sb.AppendLine("</thead>");
                sb.AppendLine("<tbody>");
                
                foreach (var kvp in result.OperationMetrics.Where(m => m.Value.TotalCount > 0))
                {
                    var metrics = kvp.Value;
                    var successRate = (double)metrics.SuccessCount / metrics.TotalCount;
                    
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td>{kvp.Key}</td>");
                    sb.AppendLine($"<td>{metrics.TotalCount:N0}</td>");
                    sb.AppendLine($"<td>{metrics.SuccessCount:N0}</td>");
                    sb.AppendLine($"<td class='{(metrics.FailureCount > 0 ? "error" : "")}'>{metrics.FailureCount:N0}</td>");
                    sb.AppendLine($"<td class='{(successRate < 0.95 ? "warning" : "success")}'>{successRate:P1}</td>");
                    sb.AppendLine($"<td>{metrics.AverageLatencyMs:F1} ms</td>");
                    sb.AppendLine($"<td>{metrics.P50LatencyMs:F1} ms</td>");
                    sb.AppendLine($"<td>{metrics.P95LatencyMs:F1} ms</td>");
                    sb.AppendLine($"<td>{metrics.P99LatencyMs:F1} ms</td>");
                    sb.AppendLine("</tr>");
                }
                
                sb.AppendLine("</tbody>");
                sb.AppendLine("</table>");
                
                // Error breakdown if any
                var errors = result.OperationMetrics
                    .SelectMany(m => m.Value.ErrorBreakdown)
                    .GroupBy(e => e.Key)
                    .ToDictionary(g => g.Key, g => g.Sum(e => e.Value));
                    
                if (errors.Any())
                {
                    sb.AppendLine("<div class='error-breakdown'>");
                    sb.AppendLine("<h4>Error Breakdown</h4>");
                    sb.AppendLine("<ul>");
                    foreach (var error in errors.OrderByDescending(e => e.Value))
                    {
                        sb.AppendLine($"<li><span class='error-type'>{error.Key}:</span> {error.Value} occurrences</li>");
                    }
                    sb.AppendLine("</ul>");
                    sb.AppendLine("</div>");
                }
                
                sb.AppendLine("</div>");
            }
            
            sb.AppendLine("</div>");
            
            // Charts
            sb.AppendLine("<div class='charts'>");
            sb.AppendLine("<h2>Performance Charts</h2>");
            
            // Throughput chart
            sb.AppendLine("<div class='chart-container'>");
            sb.AppendLine("<canvas id='throughputChart'></canvas>");
            sb.AppendLine("</div>");
            
            // Latency chart
            sb.AppendLine("<div class='chart-container'>");
            sb.AppendLine("<canvas id='latencyChart'></canvas>");
            sb.AppendLine("</div>");
            
            // Error rate chart
            sb.AppendLine("<div class='chart-container'>");
            sb.AppendLine("<canvas id='errorChart'></canvas>");
            sb.AppendLine("</div>");
            
            sb.AppendLine("</div>");
            
            // JavaScript for charts
            sb.AppendLine("<script>");
            sb.AppendLine(GenerateChartScripts(results));
            sb.AppendLine("</script>");
            
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            
            return sb.ToString();
        }

        /// <summary>
        /// Generates a Markdown report from load test results.
        /// </summary>
        public static string GenerateMarkdownReport(string testName, List<LoadTestResult> results, Dictionary<string, object>? metadata = null)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine($"# Load Test Report: {testName}");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
            
            // Metadata
            if (metadata != null && metadata.Any())
            {
                sb.AppendLine("## Test Configuration");
                sb.AppendLine();
                sb.AppendLine("| Parameter | Value |");
                sb.AppendLine("|-----------|-------|");
                foreach (var kvp in metadata)
                {
                    sb.AppendLine($"| {kvp.Key} | {kvp.Value} |");
                }
                sb.AppendLine();
            }
            
            // Summary
            if (results.Any())
            {
                var totalOps = results.Sum(r => r.TotalOperations);
                var totalDuration = results.Sum(r => r.Duration.TotalSeconds);
                var avgThroughput = totalOps / totalDuration;
                var avgErrorRate = results.Average(r => r.ErrorRate);
                
                sb.AppendLine("## Summary");
                sb.AppendLine();
                sb.AppendLine($"- **Total Operations**: {totalOps:N0}");
                sb.AppendLine($"- **Average Throughput**: {avgThroughput:F1} ops/sec");
                sb.AppendLine($"- **Average Error Rate**: {avgErrorRate:P1}");
                sb.AppendLine($"- **Total Duration**: {TimeSpan.FromSeconds(totalDuration):hh\\:mm\\:ss}");
                sb.AppendLine();
            }
            
            // Detailed Results
            sb.AppendLine("## Detailed Results");
            sb.AppendLine();
            
            foreach (var result in results)
            {
                sb.AppendLine($"### Test Run {results.IndexOf(result) + 1}");
                sb.AppendLine();
                sb.AppendLine($"Duration: {result.Duration} | Throughput: {result.Throughput:F1} ops/sec | Error Rate: {result.ErrorRate:P1}");
                sb.AppendLine();
                
                // Operation metrics table
                sb.AppendLine("| Operation | Total | Success | Failed | Success Rate | Avg Latency | P50 | P95 | P99 |");
                sb.AppendLine("|-----------|-------|---------|---------|--------------|-------------|-----|-----|-----|");
                
                foreach (var kvp in result.OperationMetrics.Where(m => m.Value.TotalCount > 0))
                {
                    var metrics = kvp.Value;
                    var successRate = (double)metrics.SuccessCount / metrics.TotalCount;
                    
                    sb.AppendLine($"| {kvp.Key} | {metrics.TotalCount:N0} | {metrics.SuccessCount:N0} | " +
                                $"{metrics.FailureCount:N0} | {successRate:P1} | {metrics.AverageLatencyMs:F1} ms | " +
                                $"{metrics.P50LatencyMs:F1} ms | {metrics.P95LatencyMs:F1} ms | {metrics.P99LatencyMs:F1} ms |");
                }
                sb.AppendLine();
                
                // Error breakdown
                var errors = result.OperationMetrics
                    .SelectMany(m => m.Value.ErrorBreakdown)
                    .GroupBy(e => e.Key)
                    .ToDictionary(g => g.Key, g => g.Sum(e => e.Value));
                    
                if (errors.Any())
                {
                    sb.AppendLine("#### Error Breakdown");
                    sb.AppendLine();
                    foreach (var error in errors.OrderByDescending(e => e.Value))
                    {
                        sb.AppendLine($"- **{error.Key}**: {error.Value} occurrences");
                    }
                    sb.AppendLine();
                }
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Generates a CSV report from load test results.
        /// </summary>
        public static string GenerateCsvReport(List<LoadTestResult> results)
        {
            var sb = new StringBuilder();
            
            // Header
            sb.AppendLine("TestRun,Operation,TotalCount,SuccessCount,FailureCount,SuccessRate,AvgLatencyMs,P50Ms,P95Ms,P99Ms,MinMs,MaxMs");
            
            // Data rows
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                foreach (var kvp in result.OperationMetrics.Where(m => m.Value.TotalCount > 0))
                {
                    var metrics = kvp.Value;
                    var successRate = (double)metrics.SuccessCount / metrics.TotalCount;
                    
                    sb.AppendLine($"{i + 1},{kvp.Key},{metrics.TotalCount},{metrics.SuccessCount}," +
                                $"{metrics.FailureCount},{successRate:F4},{metrics.AverageLatencyMs:F2}," +
                                $"{metrics.P50LatencyMs:F2},{metrics.P95LatencyMs:F2},{metrics.P99LatencyMs:F2}," +
                                $"{metrics.MinLatencyMs:F2},{metrics.MaxLatencyMs:F2}");
                }
            }
            
            return sb.ToString();
        }

        private static string GetCssStyles()
        {
            return @"
body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    line-height: 1.6;
    color: #333;
    max-width: 1200px;
    margin: 0 auto;
    padding: 20px;
    background-color: #f5f5f5;
}

h1, h2, h3, h4 {
    color: #2c3e50;
}

.timestamp {
    color: #7f8c8d;
    font-size: 0.9em;
}

.metadata, .summary, .results, .charts {
    background: white;
    padding: 20px;
    margin: 20px 0;
    border-radius: 8px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

.summary-cards {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
    gap: 20px;
    margin-top: 20px;
}

.card {
    background: #f8f9fa;
    padding: 20px;
    border-radius: 6px;
    text-align: center;
}

.card h3 {
    margin: 0 0 10px 0;
    font-size: 0.9em;
    color: #6c757d;
    text-transform: uppercase;
}

.card .value {
    font-size: 2em;
    font-weight: bold;
    margin: 0;
}

.success { color: #27ae60; }
.warning { color: #f39c12; }
.error { color: #e74c3c; }

table {
    width: 100%;
    border-collapse: collapse;
    margin: 20px 0;
}

th, td {
    padding: 12px;
    text-align: left;
    border-bottom: 1px solid #dee2e6;
}

th {
    background-color: #f8f9fa;
    font-weight: 600;
    color: #495057;
}

tr:hover {
    background-color: #f8f9fa;
}

.metrics-table {
    font-size: 0.9em;
}

.label {
    font-weight: 600;
    color: #6c757d;
}

.error-breakdown {
    margin-top: 20px;
    padding: 15px;
    background-color: #fee;
    border-radius: 6px;
}

.error-breakdown h4 {
    margin-top: 0;
    color: #c0392b;
}

.error-type {
    font-weight: 600;
    color: #e74c3c;
}

.chart-container {
    position: relative;
    height: 400px;
    margin: 30px 0;
}

.test-result {
    margin: 30px 0;
    padding: 20px;
    background-color: #f8f9fa;
    border-radius: 6px;
}";
        }

        private static string GenerateChartScripts(List<LoadTestResult> results)
        {
            var sb = new StringBuilder();
            
            // Prepare data
            var labels = results.Select((r, i) => $"Run {i + 1}").ToList();
            var throughputData = results.Select(r => r.Throughput).ToList();
            var errorRateData = results.Select(r => r.ErrorRate * 100).ToList();
            
            // Get average latencies per operation type
            var operationTypes = Enum.GetValues<AudioOperationType>().ToList();
            var latencyDatasets = new List<string>();
            
            foreach (var opType in operationTypes)
            {
                var latencies = results.Select(r =>
                    r.OperationMetrics.ContainsKey(opType) && r.OperationMetrics[opType].TotalCount > 0
                        ? r.OperationMetrics[opType].P95LatencyMs
                        : 0
                ).ToList();
                
                if (latencies.Any(l => l > 0))
                {
                    latencyDatasets.Add($@"{{
                        label: '{opType} P95',
                        data: [{string.Join(", ", latencies)}],
                        borderColor: '{GetColorForOperation(opType)}',
                        tension: 0.1
                    }}");
                }
            }
            
            // Throughput chart
            sb.AppendLine(@"
const throughputCtx = document.getElementById('throughputChart').getContext('2d');
new Chart(throughputCtx, {
    type: 'line',
    data: {
        labels: [" + string.Join(", ", labels.Select(l => $"'{l}'")) + @"],
        datasets: [{
            label: 'Throughput (ops/sec)',
            data: [" + string.Join(", ", throughputData) + @"],
            borderColor: '#3498db',
            backgroundColor: 'rgba(52, 152, 219, 0.1)',
            tension: 0.1
        }]
    },
    options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            title: {
                display: true,
                text: 'Throughput Over Time'
            }
        }
    }
});");

            // Latency chart
            sb.AppendLine(@"
const latencyCtx = document.getElementById('latencyChart').getContext('2d');
new Chart(latencyCtx, {
    type: 'line',
    data: {
        labels: [" + string.Join(", ", labels.Select(l => $"'{l}'")) + @"],
        datasets: [" + string.Join(",\n", latencyDatasets) + @"]
    },
    options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            title: {
                display: true,
                text: 'P95 Latency by Operation Type'
            }
        },
        scales: {
            y: {
                title: {
                    display: true,
                    text: 'Latency (ms)'
                }
            }
        }
    }
});");

            // Error rate chart
            sb.AppendLine(@"
const errorCtx = document.getElementById('errorChart').getContext('2d');
new Chart(errorCtx, {
    type: 'bar',
    data: {
        labels: [" + string.Join(", ", labels.Select(l => $"'{l}'")) + @"],
        datasets: [{
            label: 'Error Rate (%)',
            data: [" + string.Join(", ", errorRateData) + @"],
            backgroundColor: 'rgba(231, 76, 60, 0.5)',
            borderColor: '#e74c3c',
            borderWidth: 1
        }]
    },
    options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            title: {
                display: true,
                text: 'Error Rate Over Time'
            }
        },
        scales: {
            y: {
                beginAtZero: true,
                title: {
                    display: true,
                    text: 'Error Rate (%)'
                }
            }
        }
    }
});");
            
            return sb.ToString();
        }
        
        private static string GetColorForOperation(AudioOperationType operation)
        {
            return operation switch
            {
                AudioOperationType.Transcription => "#3498db",
                AudioOperationType.TextToSpeech => "#2ecc71",
                AudioOperationType.RealtimeSession => "#e74c3c",
                AudioOperationType.HybridConversation => "#f39c12",
                _ => "#95a5a6"
            };
        }

        /// <summary>
        /// Saves a report to file.
        /// </summary>
        public static void SaveReport(string content, string filename, string directory = "LoadTestReports")
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var fullPath = Path.Combine(directory, filename);
            File.WriteAllText(fullPath, content);
            Console.WriteLine($"Report saved to: {fullPath}");
        }
    }
}
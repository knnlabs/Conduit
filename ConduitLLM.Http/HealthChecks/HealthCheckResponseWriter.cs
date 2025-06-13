using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConduitLLM.Http.HealthChecks
{
    /// <summary>
    /// Writes health check responses in a consistent JSON format.
    /// </summary>
    public static class HealthCheckResponseWriter
    {
        /// <summary>
        /// Writes the health check report as JSON.
        /// </summary>
        public static async Task WriteResponse(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json; charset=utf-8";

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var response = new HealthCheckResponse
            {
                Status = report.Status.ToString(),
                TotalDuration = report.TotalDuration.TotalMilliseconds,
                Results = report.Entries.Select(entry => new HealthCheckResult
                {
                    Name = entry.Key,
                    Status = entry.Value.Status.ToString(),
                    Description = entry.Value.Description,
                    Duration = entry.Value.Duration.TotalMilliseconds,
                    Exception = entry.Value.Exception?.Message,
                    Data = entry.Value.Data.Count > 0 ? entry.Value.Data : null,
                    Tags = entry.Value.Tags.ToList()
                }).ToList()
            };

            var json = JsonSerializer.Serialize(response, options);
            await context.Response.WriteAsync(json);
        }
    }

    /// <summary>
    /// Health check response model.
    /// </summary>
    public class HealthCheckResponse
    {
        /// <summary>
        /// Gets or sets the overall health status.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total duration of all health checks in milliseconds.
        /// </summary>
        public double TotalDuration { get; set; }

        /// <summary>
        /// Gets or sets the individual health check results.
        /// </summary>
        public List<HealthCheckResult> Results { get; set; } = new();
    }

    /// <summary>
    /// Individual health check result.
    /// </summary>
    public class HealthCheckResult
    {
        /// <summary>
        /// Gets or sets the health check name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the health check status.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the health check description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the health check duration in milliseconds.
        /// </summary>
        public double Duration { get; set; }

        /// <summary>
        /// Gets or sets the exception message if the health check failed.
        /// </summary>
        public string? Exception { get; set; }

        /// <summary>
        /// Gets or sets additional data from the health check.
        /// </summary>
        public IReadOnlyDictionary<string, object>? Data { get; set; }

        /// <summary>
        /// Gets or sets the health check tags.
        /// </summary>
        public List<string> Tags { get; set; } = new();
    }
}
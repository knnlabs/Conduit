using System.Text;
using System.Text.Json;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing and querying billing audit events
    /// </summary>
    [ApiController]
    [Route("api/audit/billing")]
    [Authorize]
    public class BillingAuditController : ControllerBase
    {
        private readonly IBillingAuditService _billingAuditService;
        private readonly ILogger<BillingAuditController> _logger;
        
        // Metrics for billing audit API operations
        private static readonly Counter BillingAuditQueries = Prometheus.Metrics
            .CreateCounter("conduit_admin_billing_audit_queries_total", "Total billing audit queries",
                new CounterConfiguration
                {
                    LabelNames = new[] { "endpoint", "status" }
                });
        
        private static readonly Histogram BillingAuditQueryDuration = Prometheus.Metrics
            .CreateHistogram("conduit_admin_billing_audit_query_duration_seconds", "Billing audit query duration",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "endpoint" },
                    Buckets = Histogram.ExponentialBuckets(0.01, 2, 10) // 10ms to ~10s
                });
        
        private static readonly Counter BillingAuditExports = Prometheus.Metrics
            .CreateCounter("conduit_admin_billing_audit_exports_total", "Total billing audit exports",
                new CounterConfiguration
                {
                    LabelNames = new[] { "format", "status" }
                });
        
        private static readonly Gauge BillingAnomaliesDetected = Prometheus.Metrics
            .CreateGauge("conduit_admin_billing_anomalies_detected", "Number of billing anomalies detected",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "severity" }
                });

        /// <summary>
        /// Initializes a new instance of the BillingAuditController
        /// </summary>
        public BillingAuditController(
            IBillingAuditService billingAuditService,
            ILogger<BillingAuditController> logger)
        {
            _billingAuditService = billingAuditService ?? throw new ArgumentNullException(nameof(billingAuditService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Query billing audit events with filtering and pagination
        /// </summary>
        /// <param name="request">Query parameters</param>
        /// <returns>Paginated list of audit events</returns>
        [HttpPost("query")]
        [ProducesResponseType(typeof(BillingAuditResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> QueryAuditEvents([FromBody] BillingAuditQueryRequest request)
        {
            if (request.From > request.To)
            {
                return BadRequest("From date must be before or equal to To date");
            }

            if (request.PageSize > 1000)
            {
                return BadRequest("Page size cannot exceed 1000");
            }

            try
            {
                using var timer = BillingAuditQueryDuration.WithLabels("query").NewTimer();
                
                var (events, totalCount) = await _billingAuditService.GetAuditEventsAsync(
                    request.From,
                    request.To,
                    request.EventType,
                    request.VirtualKeyId,
                    request.PageNumber,
                    request.PageSize);

                var response = new BillingAuditResponse
                {
                    Events = events.Select(e => MapToDto(e)).ToList(),
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                BillingAuditQueries.WithLabels("query", "success").Inc();
                return Ok(response);
            }
            catch (Exception ex)
            {
                BillingAuditQueries.WithLabels("query", "error").Inc();
                _logger.LogError(ex, "Error querying billing audit events");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while querying audit events");
            }
        }

        /// <summary>
        /// Get summary statistics for billing audit events
        /// </summary>
        /// <param name="from">Start date</param>
        /// <param name="to">End date</param>
        /// <param name="virtualKeyId">Optional virtual key ID filter</param>
        /// <returns>Summary statistics</returns>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(BillingAuditSummary), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSummary(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] int? virtualKeyId = null)
        {
            if (from > to)
            {
                return BadRequest("From date must be before or equal to To date");
            }

            try
            {
                using var timer = BillingAuditQueryDuration.WithLabels("summary").NewTimer();
                
                var summary = await _billingAuditService.GetAuditSummaryAsync(from, to, virtualKeyId);
                
                BillingAuditQueries.WithLabels("summary", "success").Inc();
                return Ok(summary);
            }
            catch (Exception ex)
            {
                BillingAuditQueries.WithLabels("summary", "error").Inc();
                _logger.LogError(ex, "Error getting billing audit summary");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while getting audit summary");
            }
        }

        /// <summary>
        /// Detect anomalies in billing patterns
        /// </summary>
        /// <param name="from">Start date</param>
        /// <param name="to">End date</param>
        /// <returns>List of detected anomalies</returns>
        [HttpGet("anomalies")]
        [ProducesResponseType(typeof(List<BillingAnomaly>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DetectAnomalies(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to)
        {
            if (from > to)
            {
                return BadRequest("From date must be before or equal to To date");
            }

            try
            {
                using var timer = BillingAuditQueryDuration.WithLabels("anomalies").NewTimer();
                
                var anomalies = await _billingAuditService.DetectAnomaliesAsync(from, to);
                
                // Update anomaly gauge metrics
                var anomalyGroups = anomalies.GroupBy(a => a.Severity ?? "unknown");
                foreach (var group in anomalyGroups)
                {
                    BillingAnomaliesDetected.WithLabels(group.Key).Set(group.Count());
                }
                
                BillingAuditQueries.WithLabels("anomalies", "success").Inc();
                return Ok(anomalies);
            }
            catch (Exception ex)
            {
                BillingAuditQueries.WithLabels("anomalies", "error").Inc();
                _logger.LogError(ex, "Error detecting billing anomalies");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while detecting anomalies");
            }
        }

        /// <summary>
        /// Get potential revenue loss from billing failures
        /// </summary>
        /// <param name="from">Start date</param>
        /// <param name="to">End date</param>
        /// <returns>Potential revenue loss amount</returns>
        [HttpGet("revenue-loss")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetRevenueLoss(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to)
        {
            if (from > to)
            {
                return BadRequest("From date must be before or equal to To date");
            }

            try
            {
                using var timer = BillingAuditQueryDuration.WithLabels("revenue-loss").NewTimer();
                
                var loss = await _billingAuditService.GetPotentialRevenueLossAsync(from, to);
                
                BillingAuditQueries.WithLabels("revenue-loss", "success").Inc();
                return Ok(new { potentialRevenueLoss = loss, currency = "USD" });
            }
            catch (Exception ex)
            {
                BillingAuditQueries.WithLabels("revenue-loss", "error").Inc();
                _logger.LogError(ex, "Error calculating revenue loss");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while calculating revenue loss");
            }
        }

        /// <summary>
        /// Export billing audit events in various formats
        /// </summary>
        /// <param name="request">Export parameters</param>
        /// <returns>Exported data file</returns>
        [HttpPost("export")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ExportAuditEvents([FromBody] BillingAuditExportRequest request)
        {
            if (request.From > request.To)
            {
                return BadRequest("From date must be before or equal to To date");
            }

            try
            {
                using var timer = BillingAuditQueryDuration.WithLabels("export").NewTimer();
                
                // Get all events for the period (no pagination for export)
                var (events, _) = await _billingAuditService.GetAuditEventsAsync(
                    request.From,
                    request.To,
                    request.EventType,
                    request.VirtualKeyId,
                    pageNumber: 1,
                    pageSize: int.MaxValue);

                switch (request.Format)
                {
                    case ExportFormat.Json:
                        BillingAuditExports.WithLabels("json", "success").Inc();
                        return ExportAsJson(events);
                    
                    case ExportFormat.Csv:
                        BillingAuditExports.WithLabels("csv", "success").Inc();
                        return ExportAsCsv(events);
                    
                    case ExportFormat.Excel:
                        BillingAuditExports.WithLabels("excel", "not_implemented").Inc();
                        return BadRequest("Excel export not yet implemented");
                    
                    default:
                        BillingAuditExports.WithLabels(request.Format.ToString(), "unsupported").Inc();
                        return BadRequest($"Unsupported export format: {request.Format}");
                }
            }
            catch (Exception ex)
            {
                BillingAuditExports.WithLabels(request.Format.ToString(), "error").Inc();
                _logger.LogError(ex, "Error exporting billing audit events");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while exporting audit events");
            }
        }

        /// <summary>
        /// Get distinct event types
        /// </summary>
        /// <returns>List of event types with descriptions</returns>
        [HttpGet("event-types")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public IActionResult GetEventTypes()
        {
            var eventTypes = Enum.GetValues<BillingAuditEventType>()
                .Select(e => new
                {
                    Value = e,
                    Name = e.ToString(),
                    Description = GetEventTypeDescription(e)
                })
                .OrderBy(e => e.Name)
                .ToList();

            return Ok(eventTypes);
        }

        private BillingAuditEventDto MapToDto(BillingAuditEvent entity)
        {
            var dto = new BillingAuditEventDto
            {
                Id = entity.Id,
                Timestamp = entity.Timestamp,
                EventType = entity.EventType.ToString(),
                VirtualKeyId = entity.VirtualKeyId,
                VirtualKeyName = entity.VirtualKey?.KeyName,
                Model = entity.Model,
                RequestId = entity.RequestId,
                CalculatedCost = entity.CalculatedCost,
                FailureReason = entity.FailureReason,
                ProviderType = entity.ProviderType,
                HttpStatusCode = entity.HttpStatusCode,
                RequestPath = entity.RequestPath,
                IsEstimated = entity.IsEstimated
            };

            // Parse usage JSON if present
            if (!string.IsNullOrEmpty(entity.UsageJson))
            {
                try
                {
                    var usage = JsonSerializer.Deserialize<UsageDto>(entity.UsageJson);
                    dto.Usage = usage;
                }
                catch (JsonException)
                {
                    // Log but don't fail
                    _logger.LogWarning("Failed to parse usage JSON for audit event {Id}", entity.Id);
                }
            }

            // Parse metadata JSON if present
            if (!string.IsNullOrEmpty(entity.MetadataJson))
            {
                try
                {
                    var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.MetadataJson);
                    dto.Metadata = metadata;
                }
                catch (JsonException)
                {
                    _logger.LogWarning("Failed to parse metadata JSON for audit event {Id}", entity.Id);
                }
            }

            return dto;
        }

        private IActionResult ExportAsJson(List<BillingAuditEvent> events)
        {
            var json = JsonSerializer.Serialize(events.Select(e => MapToDto(e)), new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return File(Encoding.UTF8.GetBytes(json), "application/json", $"billing-audit-{DateTime.UtcNow:yyyyMMddHHmmss}.json");
        }

        private IActionResult ExportAsCsv(List<BillingAuditEvent> events)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Id,Timestamp,EventType,VirtualKeyId,Model,RequestId,CalculatedCost,FailureReason,ProviderType,HttpStatusCode,RequestPath,IsEstimated");

            foreach (var e in events)
            {
                csv.AppendLine($"{e.Id},{e.Timestamp:yyyy-MM-dd HH:mm:ss},{e.EventType},{e.VirtualKeyId},{e.Model},{e.RequestId},{e.CalculatedCost},{EscapeCsv(e.FailureReason)},{e.ProviderType},{e.HttpStatusCode},{e.RequestPath},{e.IsEstimated}");
            }

            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"billing-audit-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
        }

        private string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        private string GetEventTypeDescription(BillingAuditEventType eventType)
        {
            return eventType switch
            {
                BillingAuditEventType.UsageTracked => "Successful usage tracking and billing",
                BillingAuditEventType.UsageEstimated => "Usage was estimated due to missing data",
                BillingAuditEventType.ZeroCostSkipped => "Zero cost calculated, no billing occurred",
                BillingAuditEventType.MissingCostConfig => "Model has no cost configuration",
                BillingAuditEventType.MissingUsageData => "No usage data in response",
                BillingAuditEventType.SpendUpdateFailed => "Failed to update spend (Redis/DB)",
                BillingAuditEventType.ErrorResponseSkipped => "Error response not billed (4xx/5xx)",
                BillingAuditEventType.StreamingUsageMissing => "Streaming response missing usage data",
                BillingAuditEventType.NoVirtualKey => "No virtual key found for request",
                BillingAuditEventType.JsonParseError => "JSON parsing error prevented tracking",
                BillingAuditEventType.UnexpectedError => "Unexpected error during tracking",
                _ => "Unknown event type"
            };
        }
    }
}
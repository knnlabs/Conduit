using ConduitLLM.Configuration.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

namespace ConduitLLM.Admin.Controllers
{
    public partial class PricingController
    {
        /// <summary>
        /// Query pricing audit events
        /// </summary>
        [HttpPost("audit/query")]
        [ProducesResponseType(typeof(PricingAuditQueryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public Task<IActionResult> QueryPricingAuditEvents([FromBody] PricingAuditQueryRequest request)
        {
            if (request.From > request.To)
            {
                return Task.FromResult<IActionResult>(BadRequest("From date must be before or equal to To date"));
            }

            if (request.PageSize > 1000)
            {
                return Task.FromResult<IActionResult>(BadRequest("Page size cannot exceed 1000"));
            }

            return ExecuteAsync(
                async () =>
                {
                    using var timer = PricingOperationDuration.WithLabels("audit_query").NewTimer();

                    var (events, totalCount) = await _pricingAuditService.GetAuditEventsAsync(
                        request.From,
                        request.To,
                        request.VirtualKeyId,
                        request.ModelId,
                        request.PricingType,
                        request.PageNumber,
                        request.PageSize);

                    LogAdminAudit("Queried", "PricingAudit", detail: $"From: {request.From:O}, To: {request.To:O}, Results: {totalCount}");
                    return new PricingAuditQueryResponse
                    {
                        Events = events.Select(e => new PricingAuditEventDto
                        {
                            Id = e.Id,
                            Timestamp = e.Timestamp,
                            VirtualKeyId = e.VirtualKeyId,
                            ModelId = e.ModelId,
                            ModelCostId = e.ModelCostId,
                            PricingType = e.PricingType,
                            InputParameters = e.InputParameters,
                            MatchedRule = e.MatchedRule,
                            UsedDefaultRate = e.UsedDefaultRate,
                            AppliedRate = e.AppliedRate,
                            Quantity = e.Quantity,
                            CalculatedCost = e.CalculatedCost,
                            RequestId = e.RequestId
                        }).ToList(),
                        TotalCount = totalCount,
                        PageNumber = request.PageNumber,
                        PageSize = request.PageSize
                    };
                },
                Ok,
                "QueryPricingAuditEvents");
        }

        /// <summary>
        /// Get pricing audit summary
        /// </summary>
        [HttpGet("audit/summary")]
        [ProducesResponseType(typeof(PricingAuditSummary), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public Task<IActionResult> GetPricingAuditSummary(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] int? virtualKeyId = null)
        {
            if (from > to)
            {
                return Task.FromResult<IActionResult>(BadRequest("From date must be before or equal to To date"));
            }

            return ExecuteAsync(
                async () =>
                {
                    using var timer = PricingOperationDuration.WithLabels("audit_summary").NewTimer();

                    return await _pricingAuditService.GetSummaryAsync(from, to, virtualKeyId);
                },
                Ok,
                "GetPricingAuditSummary");
        }

        /// <summary>
        /// Get pricing audit events by request ID
        /// </summary>
        [HttpGet("audit/request/{requestId}")]
        [ProducesResponseType(typeof(IEnumerable<PricingAuditEventDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IActionResult> GetPricingAuditByRequestId(string requestId)
        {
            return ExecuteAsync(
                async () =>
                {
                    var events = await _pricingAuditService.GetByRequestIdAsync(requestId);

                    if (!events.Any())
                    {
                        throw new KeyNotFoundException($"No pricing audit events found for request {requestId}");
                    }

                    return events.Select(e => new PricingAuditEventDto
                    {
                        Id = e.Id,
                        Timestamp = e.Timestamp,
                        VirtualKeyId = e.VirtualKeyId,
                        ModelId = e.ModelId,
                        ModelCostId = e.ModelCostId,
                        PricingType = e.PricingType,
                        InputParameters = e.InputParameters,
                        MatchedRule = e.MatchedRule,
                        UsedDefaultRate = e.UsedDefaultRate,
                        AppliedRate = e.AppliedRate,
                        Quantity = e.Quantity,
                        CalculatedCost = e.CalculatedCost,
                        RequestId = e.RequestId
                    });
                },
                Ok,
                "GetPricingAuditByRequestId",
                new { RequestId = requestId });
        }
    }
}

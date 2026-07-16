using ConduitLLM.Admin.Extensions;
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
        public async Task<IActionResult> QueryPricingAuditEvents([FromBody] PricingAuditQueryRequest request)
        {
            if (ControllerErrorExtensions.ValidateDateRange(request.From, request.To) is { } dateError)
            {
                return dateError;
            }

            if (request.PageSize > 1000)
            {
                return BadRequest("Page size cannot exceed 1000");
            }

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
            return Ok(new PricingAuditQueryResponse
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
            });
        }

        /// <summary>
        /// Get pricing audit summary
        /// </summary>
        [HttpGet("audit/summary")]
        [ProducesResponseType(typeof(PricingAuditSummary), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetPricingAuditSummary(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] int? virtualKeyId = null)
        {
            if (ControllerErrorExtensions.ValidateDateRange(from, to) is { } dateError)
            {
                return dateError;
            }

            using var timer = PricingOperationDuration.WithLabels("audit_summary").NewTimer();

            var summary = await _pricingAuditService.GetSummaryAsync(from, to, virtualKeyId);
            return Ok(summary);
        }

        /// <summary>
        /// Get pricing audit events by request ID
        /// </summary>
        [HttpGet("audit/request/{requestId}")]
        [ProducesResponseType(typeof(IEnumerable<PricingAuditEventDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPricingAuditByRequestId(string requestId)
        {
            var events = await _pricingAuditService.GetByRequestIdAsync(requestId);

            if (!events.Any())
            {
                throw new KeyNotFoundException($"No pricing audit events found for request {requestId}");
            }

            return Ok(events.Select(e => new PricingAuditEventDto
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
            }));
        }
    }
}

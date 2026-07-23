using ConduitLLM.Admin.Extensions;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Functions.Utilities;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

namespace ConduitLLM.Admin.Endpoints
{
    public partial class PricingEndpoints
    {
        /// <summary>
        /// Query pricing audit events
        /// </summary>
        public async Task<IResult> QueryPricingAuditEvents(PricingAuditQueryRequest request)
        {
            if (request.From > request.To)
            {
                return AdminResults.BadRequest("From date must be before or equal to To date");
            }

            if (request.PageSize is < 1 or > 100)
            {
                return AdminResults.BadRequest("Page size must be between 1 and 100");
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
            return Results.Ok(new PagedResult<PricingAuditEventDto>
            {
                Data = events.Select(e => new PricingAuditEventDto
                {
                    Id = e.Id,
                    Timestamp = e.Timestamp,
                    VirtualKeyId = e.VirtualKeyId,
                    ModelId = e.ModelId,
                    ModelCostId = e.ModelCostId,
                    PricingType = e.PricingType,
                    InputParameters = StructuredJson.ParseObject(e.InputParameters) ?? new(),
                    MatchedRule = e.MatchedRule,
                    UsedDefaultRate = e.UsedDefaultRate,
                    AppliedRate = e.AppliedRate,
                    Quantity = e.Quantity,
                    CalculatedCost = e.CalculatedCost,
                    RequestId = e.RequestId
                }).ToList(),
                Pagination = new PaginationMetadata
                {
                    Page = request.PageNumber,
                    PageSize = request.PageSize,
                    TotalItems = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
                }
            });
        }

        /// <summary>
        /// Get pricing audit summary
        /// </summary>
        public async Task<IResult> GetPricingAuditSummary(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] int? virtualKeyId = null)
        {
            if (from > to)
            {
                return AdminResults.BadRequest("From date must be before or equal to To date");
            }

            using var timer = PricingOperationDuration.WithLabels("audit_summary").NewTimer();

            var summary = await _pricingAuditService.GetSummaryAsync(from, to, virtualKeyId);
            return Results.Ok(summary);
        }

        /// <summary>
        /// Get pricing audit events by request ID
        /// </summary>
        public async Task<IResult> GetPricingAuditByRequestId(string requestId)
        {
            var events = await _pricingAuditService.GetByRequestIdAsync(requestId);

            if (!events.Any())
            {
                throw new KeyNotFoundException($"No pricing audit events found for request {requestId}");
            }

            return Results.Ok(events.Select(e => new PricingAuditEventDto
            {
                Id = e.Id,
                Timestamp = e.Timestamp,
                VirtualKeyId = e.VirtualKeyId,
                ModelId = e.ModelId,
                ModelCostId = e.ModelCostId,
                PricingType = e.PricingType,
                InputParameters = StructuredJson.ParseObject(e.InputParameters) ?? new(),
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

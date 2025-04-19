using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Services;
using ConduitLLM.WebUI.DTOs;
using ConduitLLM.WebUI.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LogsController : ControllerBase
    {
        private readonly IRequestLogService _requestLogService;
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly ILogger<LogsController> _logger;

        public LogsController(
            IRequestLogService requestLogService,
            IVirtualKeyService virtualKeyService,
            ILogger<LogsController> logger)
        {
            _requestLogService = requestLogService;
            _virtualKeyService = virtualKeyService;
            _logger = logger;
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchLogs(
            [FromQuery] int? virtualKeyId = null,
            [FromQuery] string? modelFilter = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int? statusCode = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100; // Prevent excessive page sizes

                // Default date range if not provided (last 24 hours)
                if (!startDate.HasValue)
                    startDate = DateTime.UtcNow.AddDays(-1);
                if (!endDate.HasValue)
                    endDate = DateTime.UtcNow;

                var result = await _requestLogService.SearchLogsAsync(
                    virtualKeyId,
                    modelFilter,
                    startDate.Value,
                    endDate.Value,
                    statusCode,
                    page,
                    pageSize);

                // Map to DTO
                var logDtos = result.Logs.Select(l => new ConduitLLM.WebUI.DTOs.RequestLogDto
                {
                    Id = l.Id,
                    VirtualKeyId = l.VirtualKeyId,
                    VirtualKeyName = l.VirtualKey?.KeyName ?? "Unknown",
                    ModelName = l.ModelName,
                    RequestType = l.RequestType,
                    InputTokens = l.InputTokens,
                    OutputTokens = l.OutputTokens,
                    Cost = l.Cost,
                    ResponseTimeMs = l.ResponseTimeMs,
                    Timestamp = l.Timestamp,
                    UserId = l.UserId,
                    ClientIp = l.ClientIp,
                    RequestPath = l.RequestPath,
                    StatusCode = l.StatusCode
                }).ToList();

                return Ok(new ConduitLLM.WebUI.DTOs.PagedResult<ConduitLLM.WebUI.DTOs.RequestLogDto>
                {
                    Items = logDtos,
                    TotalCount = result.TotalCount,
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(result.TotalCount / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching request logs");
                return StatusCode(500, "An error occurred while searching request logs");
            }
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetLogsSummary(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                // Default date range if not provided (last 7 days)
                if (!startDate.HasValue)
                    startDate = DateTime.UtcNow.AddDays(-7);
                if (!endDate.HasValue)
                    endDate = DateTime.UtcNow;

                var summary = await _requestLogService.GetLogsSummaryAsync(startDate.Value, endDate.Value);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting request logs summary");
                return StatusCode(500, "An error occurred while getting request logs summary");
            }
        }

        [HttpGet("keys")]
        public async Task<IActionResult> GetVirtualKeys()
        {
            try
            {
                var keys = await _virtualKeyService.GetAllVirtualKeysAsync();
                return Ok(keys.Select(k => new { k.Id, k.KeyName }).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting virtual keys");
                return StatusCode(500, "An error occurred while getting virtual keys");
            }
        }

        [HttpGet("models")]
        public async Task<IActionResult> GetDistinctModels()
        {
            try
            {
                var models = await _requestLogService.GetDistinctModelsAsync();
                return Ok(models);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting distinct models");
                return StatusCode(500, "An error occurred while getting distinct models");
            }
        }
    }
}

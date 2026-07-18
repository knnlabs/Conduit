using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConduitLLM.Admin.Filters;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Configuration.Messaging;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing provider tools and their costs.
    /// </summary>
    [ApiController]
    [Route("api/admin/provider-tools")]
    [ServiceFilter(typeof(OperationLoggingFilter))]
    public class ProviderToolsController : AdminControllerBase
    {
        private readonly ConduitDbContext _context;
        private readonly IEventBus? _eventBus;

        /// <summary>
        /// Initializes a new instance of the ProviderToolsController.
        /// </summary>
        public ProviderToolsController(
            ConduitDbContext context,
            ILogger<ProviderToolsController> logger,
            IEventBus? eventBus = null)
            : base(eventBus, logger)
        {
            _context = context;
            _eventBus = eventBus;
        }

        /// <summary>
        /// Gets all provider tools.
        /// </summary>
        /// <param name="provider">Optional provider type filter</param>
        /// <param name="isActive">Optional active status filter</param>
        /// <returns>List of provider tools</returns>
        [HttpGet]
        public async Task<IActionResult> GetProviderTools(
            [FromQuery] ProviderType? provider = null,
            [FromQuery] bool? isActive = null)
        {
            var query = _context.ProviderTools.AsQueryable();

            if (provider.HasValue)
            {
                query = query.Where(pt => pt.Provider == provider.Value);
            }

            if (isActive.HasValue)
            {
                query = query.Where(pt => pt.IsActive == isActive.Value);
            }

            var tools = await query
                .OrderBy(pt => pt.Provider)
                .ThenBy(pt => pt.ToolName)
                .ToListAsync();

            return Ok(tools.Select(ProviderToolDto.FromEntity));
        }

        /// <summary>
        /// Gets a specific provider tool by ID.
        /// </summary>
        /// <param name="id">Tool ID</param>
        /// <returns>Provider tool details</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProviderTool(int id)
        {
            var tool = await _context.ProviderTools.FindAsync(id);
            if (tool == null)
            {
                throw new KeyNotFoundException($"Provider tool with ID '{id}' not found");
            }

            return Ok(ProviderToolDto.FromEntity(tool));
        }

        /// <summary>
        /// Creates a new provider tool.
        /// </summary>
        /// <param name="dto">Provider tool creation data</param>
        /// <returns>Created provider tool</returns>
        [HttpPost]
        public async Task<IActionResult> CreateProviderTool([FromBody] CreateProviderToolDto dto)
        {
            // Validate billing unit
            ValidateBillingUnit(dto.BillingUnit);

            // Check if tool already exists for this provider
            var existingTool = await _context.ProviderTools
                .FirstOrDefaultAsync(pt => pt.Provider == dto.Provider && pt.ToolName == dto.ToolName);

            if (existingTool != null)
            {
                throw new InvalidOperationException($"Tool '{dto.ToolName}' already exists for provider {dto.Provider}");
            }

            var tool = new ProviderTool
            {
                Provider = dto.Provider,
                ToolName = dto.ToolName,
                ToolParameters = dto.ToolParameters,
                CostPerUnit = dto.CostPerUnit,
                BillingUnit = dto.BillingUnit,
                CostDescription = dto.CostDescription,
                IsActive = dto.IsActive,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ProviderTools.Add(tool);
            await _context.SaveChangesAsync();

            LogAdminAudit("Created", "ProviderTool", tool.Id,
                $"ToolName: {LoggingSanitizer.S(tool.ToolName)}, Provider: {tool.Provider}");

            await PublishToolChangedEventAsync(tool, "Created");

            var result = ProviderToolDto.FromEntity(tool);
            return CreatedAtAction(nameof(GetProviderTool), new { id = result.Id }, result);
        }

        /// <summary>
        /// Updates an existing provider tool.
        /// </summary>
        /// <param name="id">Tool ID</param>
        /// <param name="dto">Updated tool data</param>
        /// <returns>Updated provider tool</returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProviderTool(int id, [FromBody] UpdateProviderToolDto dto)
        {
            // Validate billing unit
            ValidateBillingUnit(dto.BillingUnit);

            var tool = await _context.ProviderTools.FindAsync(id);
            if (tool == null)
            {
                throw new KeyNotFoundException($"Provider tool with ID '{id}' not found");
            }

            tool.IsActive = dto.IsActive;
            tool.ToolParameters = dto.ToolParameters;
            tool.CostPerUnit = dto.CostPerUnit;
            tool.BillingUnit = dto.BillingUnit;
            tool.CostDescription = dto.CostDescription;
            tool.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            LogAdminAudit("Updated", "ProviderTool", id,
                $"ToolName: {LoggingSanitizer.S(tool.ToolName)}, Provider: {tool.Provider}");

            await PublishToolChangedEventAsync(tool, "Updated");

            return Ok(ProviderToolDto.FromEntity(tool));
        }

        /// <summary>
        /// Deletes a provider tool.
        /// </summary>
        /// <param name="id">Tool ID</param>
        /// <returns>Success status</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProviderTool(int id)
        {
            var tool = await _context.ProviderTools.FindAsync(id);
            if (tool == null)
            {
                throw new KeyNotFoundException($"Provider tool with ID '{id}' not found");
            }

            _context.ProviderTools.Remove(tool);
            await _context.SaveChangesAsync();

            LogAdminAudit("Deleted", "ProviderTool", id,
                $"ToolName: {LoggingSanitizer.S(tool.ToolName)}, Provider: {tool.Provider}");

            await PublishToolChangedEventAsync(tool, "Deleted");

            return NoContent();
        }

        /// <summary>
        /// Gets available provider types that support tools.
        /// </summary>
        /// <returns>List of provider types with tool support</returns>
        [HttpGet("providers")]
        public ActionResult<IEnumerable<object>> GetToolProviders()
        {
            // Define which providers support tools
            var toolProviders = new[]
            {
                new { Value = (int)ProviderType.Groq, Name = "Groq", Description = "Supports code_interpreter, browser tools" },
                new { Value = (int)ProviderType.OpenAI, Name = "OpenAI", Description = "Function calling (client-side tools)" },
                new { Value = (int)ProviderType.Fireworks, Name = "Fireworks", Description = "May support tools" },
                new { Value = (int)ProviderType.OpenAICompatible, Name = "OpenAI Compatible", Description = "Depends on implementation" }
            };

            return Ok(toolProviders);
        }

        /// <summary>
        /// Gets available billing units.
        /// </summary>
        /// <returns>List of billing unit options</returns>
        [HttpGet("billing-units")]
        public ActionResult<IEnumerable<string>> GetBillingUnits()
        {
            return Ok(ProviderToolBillingUnits.All);
        }

        /// <summary>
        /// Bulk import provider tools from a JSON array.
        /// </summary>
        /// <param name="tools">Array of provider tools to import</param>
        /// <returns>Import results</returns>
        [HttpPost("import")]
        public async Task<IActionResult> ImportProviderTools([FromBody] List<CreateProviderToolDto> tools)
        {
            var imported = 0;
            var skipped = 0;
            var errors = new List<string>();
            var affectedProviders = new HashSet<ProviderType>();

            foreach (var dto in tools)
            {
                try
                {
                    // Validate billing unit
                    if (!ProviderToolBillingUnits.IsValid(dto.BillingUnit))
                    {
                        errors.Add($"Tool '{dto.ToolName}': Invalid billing unit '{dto.BillingUnit}'. " +
                            $"Must be one of: {string.Join(", ", ProviderToolBillingUnits.All)}");
                        skipped++;
                        continue;
                    }

                    // Check if tool already exists
                    var exists = await _context.ProviderTools
                        .AnyAsync(pt => pt.Provider == dto.Provider && pt.ToolName == dto.ToolName);

                    if (exists)
                    {
                        skipped++;
                        errors.Add($"Tool '{dto.ToolName}' already exists for {dto.Provider}");
                        continue;
                    }

                    var tool = new ProviderTool
                    {
                        Provider = dto.Provider,
                        ToolName = dto.ToolName,
                        ToolParameters = dto.ToolParameters,
                        CostPerUnit = dto.CostPerUnit,
                        BillingUnit = dto.BillingUnit,
                        CostDescription = dto.CostDescription,
                        IsActive = dto.IsActive,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.ProviderTools.Add(tool);
                    imported++;
                    affectedProviders.Add(dto.Provider);
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to import {dto.ToolName}: {ex.Message}");
                }
            }

            if (imported > 0)
            {
                await _context.SaveChangesAsync();

                // Publish events for each affected provider
                foreach (var provider in affectedProviders)
                {
                    await PublishToolChangedEventAsync(provider, "BulkImport");
                }
            }

            LogAdminAudit("Imported", "ProviderTool",
                detail: $"Imported: {imported}, Skipped: {skipped}, Total: {tools.Count}");

            return Ok(new
            {
                imported,
                skipped,
                total = tools.Count,
                errors = errors.Count > 0 ? errors : null
            });
        }

        /// <summary>
        /// Exports all provider tools as JSON.
        /// </summary>
        /// <returns>JSON array of all provider tools</returns>
        [HttpGet("export")]
        public async Task<IActionResult> ExportProviderTools()
        {
            var tools = await _context.ProviderTools
                .OrderBy(pt => pt.Provider)
                .ThenBy(pt => pt.ToolName)
                .ToListAsync();

            var dtos = tools.Select(ProviderToolDto.FromEntity);

            Response.Headers.Append("Content-Disposition", "attachment; filename=provider-tools.json");
            return Ok(dtos);
        }

        /// <summary>
        /// Validates that the billing unit is a recognized value.
        /// </summary>
        private static void ValidateBillingUnit(string? billingUnit)
        {
            if (!ProviderToolBillingUnits.IsValid(billingUnit))
            {
                throw new ArgumentException(
                    $"Invalid billing unit '{billingUnit}'. Must be one of: {string.Join(", ", ProviderToolBillingUnits.All)}");
            }
        }

        /// <summary>
        /// Publishes a ProviderToolChanged event for cache invalidation.
        /// </summary>
        private async Task PublishToolChangedEventAsync(ProviderTool tool, string changeType)
        {
            if (_eventBus == null) return;

            try
            {
                await _eventBus.PublishAsync(new ProviderToolChanged
                {
                    ProviderToolId = tool.Id,
                    ToolName = tool.ToolName,
                    ProviderType = tool.Provider?.ToString() ?? "Unknown",
                    ChangeType = changeType,
                    CorrelationId = Guid.NewGuid().ToString()
                });
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to publish ProviderToolChanged event for {ToolName} — operation completed but cache may be stale",
                    tool.ToolName);
            }
        }

        /// <summary>
        /// Publishes a ProviderToolChanged event for a provider type (bulk operations).
        /// </summary>
        private async Task PublishToolChangedEventAsync(ProviderType providerType, string changeType)
        {
            if (_eventBus == null) return;

            try
            {
                await _eventBus.PublishAsync(new ProviderToolChanged
                {
                    ProviderToolId = 0,
                    ToolName = "*",
                    ProviderType = providerType.ToString(),
                    ChangeType = changeType,
                    CorrelationId = Guid.NewGuid().ToString()
                });
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to publish ProviderToolChanged event for {ProviderType} — operation completed but cache may be stale",
                    providerType);
            }
        }
    }
}

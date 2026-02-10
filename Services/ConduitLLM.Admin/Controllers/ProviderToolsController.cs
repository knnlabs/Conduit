using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing provider tools and their costs.
    /// </summary>
    [ApiController]
    [Route("api/admin/provider-tools")]
    public class ProviderToolsController : AdminControllerBase
    {
        private readonly ConduitDbContext _context;

        /// <summary>
        /// Initializes a new instance of the ProviderToolsController.
        /// </summary>
        public ProviderToolsController(ConduitDbContext context, ILogger<ProviderToolsController> logger)
            : base(logger)
        {
            _context = context;
        }

        /// <summary>
        /// Gets all provider tools.
        /// </summary>
        /// <param name="provider">Optional provider type filter</param>
        /// <param name="isActive">Optional active status filter</param>
        /// <returns>List of provider tools</returns>
        [HttpGet]
        public Task<IActionResult> GetProviderTools(
            [FromQuery] ProviderType? provider = null,
            [FromQuery] bool? isActive = null)
        {
            return ExecuteAsync(
                async () =>
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

                    return tools.Select(ProviderToolDto.FromEntity);
                },
                result => Ok(result),
                "GetProviderTools");
        }

        /// <summary>
        /// Gets a specific provider tool by ID.
        /// </summary>
        /// <param name="id">Tool ID</param>
        /// <returns>Provider tool details</returns>
        [HttpGet("{id}")]
        public Task<IActionResult> GetProviderTool(int id)
        {
            return ExecuteAsync(
                async () =>
                {
                    var tool = await _context.ProviderTools.FindAsync(id);
                    if (tool == null)
                    {
                        throw new KeyNotFoundException($"Provider tool with ID '{id}' not found");
                    }

                    return ProviderToolDto.FromEntity(tool);
                },
                result => Ok(result),
                "GetProviderTool",
                new { Id = id });
        }

        /// <summary>
        /// Creates a new provider tool.
        /// </summary>
        /// <param name="dto">Provider tool creation data</param>
        /// <returns>Created provider tool</returns>
        [HttpPost]
        public Task<IActionResult> CreateProviderTool([FromBody] CreateProviderToolDto dto)
        {
            return ExecuteAsync(
                async () =>
                {
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

                    Logger.LogInformation("Created provider tool {ToolName} for {Provider}", tool.ToolName, tool.Provider);

                    return ProviderToolDto.FromEntity(tool);
                },
                result => CreatedAtAction(nameof(GetProviderTool), new { id = result.Id }, result),
                "CreateProviderTool");
        }

        /// <summary>
        /// Updates an existing provider tool.
        /// </summary>
        /// <param name="id">Tool ID</param>
        /// <param name="dto">Updated tool data</param>
        /// <returns>Updated provider tool</returns>
        [HttpPut("{id}")]
        public Task<IActionResult> UpdateProviderTool(int id, [FromBody] UpdateProviderToolDto dto)
        {
            return ExecuteAsync(
                async () =>
                {
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

                    Logger.LogInformation("Updated provider tool {Id} ({ToolName} for {Provider})",
                        id, tool.ToolName, tool.Provider);

                    return ProviderToolDto.FromEntity(tool);
                },
                result => Ok(result),
                "UpdateProviderTool",
                new { Id = id });
        }

        /// <summary>
        /// Deletes a provider tool.
        /// </summary>
        /// <param name="id">Tool ID</param>
        /// <returns>Success status</returns>
        [HttpDelete("{id}")]
        public Task<IActionResult> DeleteProviderTool(int id)
        {
            return ExecuteAsync(
                async () =>
                {
                    var tool = await _context.ProviderTools.FindAsync(id);
                    if (tool == null)
                    {
                        throw new KeyNotFoundException($"Provider tool with ID '{id}' not found");
                    }

                    _context.ProviderTools.Remove(tool);
                    await _context.SaveChangesAsync();

                    Logger.LogInformation("Deleted provider tool {Id} ({ToolName} for {Provider})",
                        id, tool.ToolName, tool.Provider);
                },
                NoContent(),
                "DeleteProviderTool",
                new { Id = id });
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
            var billingUnits = new[]
            {
                "requests",
                "hours",
                "minutes",
                "searches",
                "executions",
                "characters",
                "tokens"
            };

            return Ok(billingUnits);
        }

        /// <summary>
        /// Bulk import provider tools from a JSON array.
        /// </summary>
        /// <param name="tools">Array of provider tools to import</param>
        /// <returns>Import results</returns>
        [HttpPost("import")]
        public Task<IActionResult> ImportProviderTools([FromBody] List<CreateProviderToolDto> tools)
        {
            return ExecuteAsync(
                async () =>
                {
                    var imported = 0;
                    var skipped = 0;
                    var errors = new List<string>();

                    foreach (var dto in tools)
                    {
                        try
                        {
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
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Failed to import {dto.ToolName}: {ex.Message}");
                        }
                    }

                    if (imported > 0)
                    {
                        await _context.SaveChangesAsync();
                    }

                    Logger.LogInformation("Imported {Imported} provider tools, skipped {Skipped}", imported, skipped);

                    return new
                    {
                        imported,
                        skipped,
                        total = tools.Count,
                        errors = errors.Any() ? errors : null
                    };
                },
                result => Ok(result),
                "ImportProviderTools");
        }

        /// <summary>
        /// Exports all provider tools as JSON.
        /// </summary>
        /// <returns>JSON array of all provider tools</returns>
        [HttpGet("export")]
        public Task<IActionResult> ExportProviderTools()
        {
            return ExecuteAsync(
                async () =>
                {
                    var tools = await _context.ProviderTools
                        .OrderBy(pt => pt.Provider)
                        .ThenBy(pt => pt.ToolName)
                        .ToListAsync();

                    var dtos = tools.Select(ProviderToolDto.FromEntity);

                    Response.Headers.Append("Content-Disposition", "attachment; filename=provider-tools.json");
                    return dtos;
                },
                result => Ok(result),
                "ExportProviderTools");
        }
    }
}

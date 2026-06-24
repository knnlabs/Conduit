using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Filters;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Functions.DTOs;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers;

/// <summary>
/// Controller for managing function costs and pricing configurations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "MasterKeyPolicy")]
[ServiceFilter(typeof(OperationLoggingFilter))]
public class FunctionCostsController : AdminControllerBase
{
    private readonly IFunctionCostService _functionCostService;

    /// <summary>
    /// Initializes a new instance of the FunctionCostsController.
    /// </summary>
    public FunctionCostsController(
        IFunctionCostService functionCostService,
        ILogger<FunctionCostsController> logger)
        : base(logger)
    {
        _functionCostService = functionCostService ?? throw new ArgumentNullException(nameof(functionCostService));
    }

    /// <summary>
    /// Gets all function costs.
    /// </summary>
    /// <returns>List of all function costs</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllFunctionCosts()
    {
        var functionCosts = await _functionCostService.ListCostsAsync();
        return Ok(functionCosts.Select(e => e.ToDto()).ToList());
    }

    /// <summary>
    /// Gets a function cost by ID.
    /// </summary>
    /// <param name="id">The ID of the function cost</param>
    /// <returns>The function cost</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFunctionCostById(int id)
    {
        var functionCost = await _functionCostService.GetCostByIdAsync(id);
        var dto = functionCost?.ToDto();
        if (dto == null)
        {
            return this.NotFoundEntity("Function cost", id);
        }
        return Ok(dto);
    }

    /// <summary>
    /// Gets the active cost for a function configuration.
    /// </summary>
    /// <param name="functionConfigurationId">The function configuration ID</param>
    /// <returns>The active function cost</returns>
    [HttpGet("configuration/{functionConfigurationId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCostForConfiguration(int functionConfigurationId)
    {
        var functionCost = await _functionCostService.GetCostForConfigurationAsync(
            functionConfigurationId);
        var dto = functionCost?.ToDto();
        if (dto == null)
        {
            return this.NotFoundEntity("Function cost for configuration", functionConfigurationId);
        }
        return Ok(dto);
    }

    /// <summary>
    /// Creates a new function cost.
    /// </summary>
    /// <param name="createDto">The function cost to create</param>
    /// <returns>The created function cost</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateFunctionCost(
        [FromBody] CreateFunctionCostDto createDto)
    {
        if (createDto == null)
        {
            return BadRequest(new ErrorResponseDto("Function cost data is required"));
        }

        var entity = MapToEntity(createDto);
        int id = await _functionCostService.CreateCostAsync(entity);

        // Fetch the created entity to return as DTO
        var created = await _functionCostService.GetCostByIdAsync(id);
        var dto = created?.ToDto();

        LogAdminAudit("Created", "FunctionCost", id, $"CostName: {LoggingSanitizer.S(createDto.CostName)}");
        return CreatedAtAction(
            nameof(GetFunctionCostById),
            new { id },
            dto);
    }

    /// <summary>
    /// Updates an existing function cost.
    /// </summary>
    /// <param name="id">The ID of the function cost to update</param>
    /// <param name="updateDto">The updated function cost data</param>
    /// <returns>The updated function cost</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateFunctionCost(
        int id,
        [FromBody] UpdateFunctionCostDto updateDto)
    {
        if (updateDto == null)
        {
            return BadRequest(new ErrorResponseDto("Function cost data is required"));
        }

        if (id != updateDto.Id)
        {
            return BadRequest(new ErrorResponseDto("ID mismatch"));
        }

        // Get existing entity to preserve fields not in update DTO
        var existing = await _functionCostService.GetCostByIdAsync(id);
        if (existing == null)
        {
            throw new KeyNotFoundException();
        }

        // Map update DTO to entity, preserving ProviderType from existing
        var entity = MapToEntity(updateDto, existing);
        await _functionCostService.UpdateCostAsync(entity);

        // Fetch the updated entity to return
        var updated = await _functionCostService.GetCostByIdAsync(id);
        LogAdminAudit("Updated", "FunctionCost", id, $"CostName: {LoggingSanitizer.S(updateDto.CostName)}");
        return Ok(updated?.ToDto());
    }

    /// <summary>
    /// Deletes a function cost.
    /// </summary>
    /// <param name="id">The ID of the function cost to delete</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFunctionCost(int id)
    {
        var existing = await _functionCostService.GetCostByIdAsync(id);
        await _functionCostService.DeleteCostAsync(id);
        LogAdminAudit("Deleted", "FunctionCost", id, existing != null ? $"CostName: {LoggingSanitizer.S(existing.CostName)}" : null);
        return NoContent();
    }

    /// <summary>
    /// Clears the function cost cache.
    /// </summary>
    /// <returns>Success message</returns>
    [HttpPost("cache/clear")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ClearCache()
    {
        await _functionCostService.ClearCacheAsync();
        LogAdminAudit("Cleared", "FunctionCostCache");
        return Ok(new { message = "Function cost cache cleared successfully" });
    }

    // Mapping methods

    private static FunctionCost MapToEntity(CreateFunctionCostDto dto)
    {
        return new FunctionCost
        {
            CostName = dto.CostName,
            ProviderType = dto.ProviderType,
            Purpose = dto.Purpose,
            Description = dto.Description,
            BaseCost = dto.BaseCost,
            PricingModel = dto.PricingModel,
            PricingConfiguration = dto.PricingConfiguration,
            IsActive = dto.IsActive,
            Priority = dto.Priority,
            EffectiveDate = dto.EffectiveDate,
            ExpiryDate = dto.ExpiryDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static FunctionCost MapToEntity(UpdateFunctionCostDto dto, FunctionCost existing)
    {
        existing.CostName = dto.CostName;
        existing.Purpose = dto.Purpose;
        existing.Description = dto.Description;
        existing.BaseCost = dto.BaseCost;
        existing.PricingModel = dto.PricingModel;
        existing.PricingConfiguration = dto.PricingConfiguration;
        existing.IsActive = dto.IsActive;
        existing.Priority = dto.Priority;
        existing.EffectiveDate = dto.EffectiveDate;
        existing.ExpiryDate = dto.ExpiryDate;
        existing.UpdatedAt = DateTime.UtcNow;
        // Note: ProviderType is not updated as it's set on creation and shouldn't change
        return existing;
    }
}

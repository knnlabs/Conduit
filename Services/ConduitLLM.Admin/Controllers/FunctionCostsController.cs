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
public class FunctionCostsController : ControllerBase
{
    private readonly IFunctionCostService _functionCostService;
    private readonly ILogger<FunctionCostsController> _logger;

    /// <summary>
    /// Initializes a new instance of the FunctionCostsController.
    /// </summary>
    public FunctionCostsController(
        IFunctionCostService functionCostService,
        ILogger<FunctionCostsController> logger)
    {
        _functionCostService = functionCostService ?? throw new ArgumentNullException(nameof(functionCostService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all function costs.
    /// </summary>
    /// <returns>List of all function costs</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllFunctionCosts()
    {
        try
        {
            var functionCosts = await _functionCostService.ListCostsAsync();
            var dtos = functionCosts.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all function costs");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Gets a function cost by ID.
    /// </summary>
    /// <param name="id">The ID of the function cost</param>
    /// <returns>The function cost</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetFunctionCostById(int id)
    {
        try
        {
            var functionCost = await _functionCostService.GetCostByIdAsync(id);

            if (functionCost == null)
            {
                return NotFound(new ErrorResponseDto("Function cost not found"));
            }

            var dto = MapToDto(functionCost);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function cost with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Gets the active cost for a function configuration.
    /// </summary>
    /// <param name="functionConfigurationId">The function configuration ID</param>
    /// <returns>The active function cost</returns>
    [HttpGet("configuration/{functionConfigurationId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCostForConfiguration(int functionConfigurationId)
    {
        try
        {
            var functionCost = await _functionCostService.GetCostForConfigurationAsync(
                functionConfigurationId);

            if (functionCost == null)
            {
                return NotFound(new ErrorResponseDto(
                    $"No active cost found for function configuration {functionConfigurationId}"));
            }

            var dto = MapToDto(functionCost);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting function cost for configuration {FunctionConfigurationId}",
                functionConfigurationId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Creates a new function cost.
    /// </summary>
    /// <param name="createDto">The function cost to create</param>
    /// <returns>The created function cost</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateFunctionCost(
        [FromBody] CreateFunctionCostDto createDto)
    {
        try
        {
            if (createDto == null)
            {
                return BadRequest(new ErrorResponseDto("Function cost data is required"));
            }

            var entity = MapToEntity(createDto);
            int id = await _functionCostService.CreateCostAsync(entity);

            // Fetch the created entity to return as DTO
            var created = await _functionCostService.GetCostByIdAsync(id);
            var dto = created != null ? MapToDto(created) : null;

            return CreatedAtAction(
                nameof(GetFunctionCostById),
                new { id },
                dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating function cost");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
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
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateFunctionCost(
        int id,
        [FromBody] UpdateFunctionCostDto updateDto)
    {
        try
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
                return NotFound(new ErrorResponseDto("Function cost not found"));
            }

            // Map update DTO to entity, preserving ProviderType from existing
            var entity = MapToEntity(updateDto, existing);
            await _functionCostService.UpdateCostAsync(entity);

            // Fetch the updated entity to return
            var updated = await _functionCostService.GetCostByIdAsync(id);
            var dto = updated != null ? MapToDto(updated) : null;

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating function cost with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Deletes a function cost.
    /// </summary>
    /// <param name="id">The ID of the function cost to delete</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteFunctionCost(int id)
    {
        try
        {
            await _functionCostService.DeleteCostAsync(id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting function cost with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Clears the function cost cache.
    /// </summary>
    /// <returns>Success message</returns>
    [HttpPost("cache/clear")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ClearCache()
    {
        try
        {
            await _functionCostService.ClearCacheAsync();

            return Ok(new { message = "Function cost cache cleared successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing function cost cache");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    // Mapping methods

    private static FunctionCostDto MapToDto(FunctionCost entity)
    {
        return new FunctionCostDto
        {
            Id = entity.Id,
            CostName = entity.CostName,
            ProviderType = entity.ProviderType,
            Purpose = entity.Purpose,
            Description = entity.Description,
            BaseCost = entity.BaseCost,
            PricingModel = entity.PricingModel,
            CostPerExecution = entity.CostPerExecution,
            CostPerResult = entity.CostPerResult,
            CostPerToken = entity.CostPerToken,
            CostPerMinute = entity.CostPerMinute,
            TieredPricing = entity.TieredPricing,
            PricingConfiguration = entity.PricingConfiguration,
            IsActive = entity.IsActive,
            EffectiveDate = entity.EffectiveDate,
            ExpiryDate = entity.ExpiryDate,
            Priority = entity.Priority,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

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

using ConduitLLM.Configuration.Interfaces;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers;

/// <summary>
/// Controller for managing model provider mappings
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "MasterKeyPolicy")]
public class ModelProviderMappingController : ControllerBase
{
    private readonly IAdminModelProviderMappingService _mappingService;
    private readonly IProviderService _providerService;
    private readonly ILogger<ModelProviderMappingController> _logger;

    /// <summary>
    /// Initializes a new instance of the ModelProviderMappingController
    /// </summary>
    /// <param name="mappingService">The model provider mapping service</param>
    /// <param name="providerService">The provider service</param>
    /// <param name="logger">The logger</param>
    public ModelProviderMappingController(
        IAdminModelProviderMappingService mappingService,
        IProviderService providerService,
        ILogger<ModelProviderMappingController> logger)
    {
        _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
        _providerService = providerService ?? throw new ArgumentNullException(nameof(providerService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all model provider mappings
    /// </summary>
    /// <returns>A list of all model provider mappings</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ModelProviderMappingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllMappings()
    {
        try
        {
            var mappings = await _mappingService.GetAllMappingsAsync();
            var dtos = mappings.Select(m => m.ToDto());
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all model provider mappings");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving model provider mappings");
        }
    }

    /// <summary>
    /// Gets a specific model provider mapping by ID
    /// </summary>
    /// <param name="id">The ID of the mapping to retrieve</param>
    /// <returns>The model provider mapping</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ModelProviderMappingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMappingById(int id)
    {
        try
        {
            var mapping = await _mappingService.GetMappingByIdAsync(id);

            if (mapping == null)
            {
                return NotFound(new ErrorResponseDto("Model provider mapping not found"));
            }

            return Ok(mapping.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting model provider mapping with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the model provider mapping");
        }
    }

    /// <summary>
    /// Creates a new model provider mapping
    /// </summary>
    /// <param name="mappingDto">The mapping to create</param>
    /// <returns>The created mapping</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ModelProviderMappingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateMapping([FromBody] ModelProviderMappingDto mappingDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Check if a mapping with the same model alias already exists
            var existingMappings = await _mappingService.GetAllMappingsAsync();
            var existingMapping = existingMappings.FirstOrDefault(m => m.ModelAlias.Equals(mappingDto.ModelAlias, StringComparison.OrdinalIgnoreCase));
            if (existingMapping != null)
            {
                return Conflict(new ErrorResponseDto($"A mapping for model alias '{mappingDto.ModelAlias}' already exists"));
            }

            var mapping = mappingDto.ToEntity();
            var success = await _mappingService.AddMappingAsync(mapping);

            if (!success)
            {
                return BadRequest(new ErrorResponseDto("Failed to create model provider mapping. Please check the provider ID."));
            }

            var createdMapping = await _mappingService.GetMappingByIdAsync(mapping.Id);
            return CreatedAtAction(nameof(GetMappingById), new { id = createdMapping?.Id }, createdMapping?.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating model provider mapping");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the model provider mapping");
        }
    }

    /// <summary>
    /// Updates an existing model provider mapping
    /// </summary>
    /// <param name="id">The ID of the mapping to update</param>
    /// <param name="mappingDto">The updated mapping data</param>
    /// <returns>No content on success</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateMapping(int id, [FromBody] ModelProviderMappingDto mappingDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != mappingDto.Id)
            {
                return BadRequest(new ErrorResponseDto("ID mismatch"));
            }

            var existingMapping = await _mappingService.GetMappingByIdAsync(id);
            if (existingMapping == null)
            {
                return NotFound(new ErrorResponseDto("Model provider mapping not found"));
            }

            existingMapping.UpdateFromDto(mappingDto);
            var success = await _mappingService.UpdateMappingAsync(existingMapping);

            if (!success)
            {
                return BadRequest(new ErrorResponseDto("Failed to update model provider mapping"));
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating model provider mapping with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the model provider mapping");
        }
    }

    /// <summary>
    /// Deletes a model provider mapping
    /// </summary>
    /// <param name="id">The ID of the mapping to delete</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteMapping(int id)
    {
        try
        {
            var existingMapping = await _mappingService.GetMappingByIdAsync(id);
            if (existingMapping == null)
            {
                return NotFound(new ErrorResponseDto("Model provider mapping not found"));
            }

            var success = await _mappingService.DeleteMappingAsync(id);

            if (!success)
            {
                return BadRequest(new ErrorResponseDto("Failed to delete model provider mapping"));
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting model provider mapping with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the model provider mapping");
        }
    }

    /// <summary>
    /// Gets all available providers
    /// </summary>
    /// <returns>List of providers with IDs and names</returns>
    [HttpGet("providers")]
    [ProducesResponseType(typeof(IEnumerable<Provider>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProviders()
    {
        try
        {
            var providers = await _mappingService.GetProvidersAsync();
            return Ok(providers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting providers");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving providers");
        }
    }

    /// <summary>
    /// Creates multiple model provider mappings in a single operation
    /// </summary>
    /// <param name="mappingDtos">The mappings to create</param>
    /// <returns>The bulk mapping response with results</returns>
    [HttpPost("bulk")]
    [ProducesResponseType(typeof(BulkMappingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateBulkMappings([FromBody] List<ModelProviderMappingDto> mappingDtos)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (mappingDtos == null || !mappingDtos.Any())
            {
                return BadRequest(new ErrorResponseDto("No mappings provided"));
            }

            var mappings = mappingDtos.Select(dto => dto.ToEntity()).ToList();
            var (created, errors) = await _mappingService.CreateBulkMappingsAsync(mappings);

            var result = new BulkMappingResult
            {
                Created = created.Select(m => m.ToDto()).ToList(),
                Errors = errors.ToList(),
                TotalProcessed = mappingDtos.Count(),
                SuccessCount = created.Count(),
                FailureCount = errors.Count()
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bulk model provider mappings");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating bulk model provider mappings");
        }
    }

    /// <summary>
    /// Deletes multiple model provider mappings in a single operation
    /// </summary>
    /// <param name="ids">The IDs of the mappings to delete</param>
    /// <returns>The bulk delete response with results</returns>
    [HttpPost("bulk/delete")]
    [ProducesResponseType(typeof(BulkDeleteResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteBulkMappings([FromBody] List<int> ids)
    {
        try
        {
            if (ids == null || ids.Count == 0)
            {
                return BadRequest(new ErrorResponseDto("No mapping IDs provided"));
            }

            var deleted = new List<int>();
            var errors = new List<string>();

            foreach (var id in ids)
            {
                try
                {
                    var existingMapping = await _mappingService.GetMappingByIdAsync(id);
                    if (existingMapping == null)
                    {
                        errors.Add($"Mapping with ID {id} not found");
                        continue;
                    }

                    var success = await _mappingService.DeleteMappingAsync(id);
                    if (success)
                    {
                        deleted.Add(id);
                    }
                    else
                    {
                        errors.Add($"Failed to delete mapping with ID {id}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting mapping with ID {Id}", id);
                    errors.Add($"Error deleting mapping with ID {id}: {ex.Message}");
                }
            }

            var result = new BulkDeleteResult
            {
                DeletedIds = deleted,
                Errors = errors,
                TotalProcessed = ids.Count,
                SuccessCount = deleted.Count,
                FailureCount = errors.Count
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting bulk model provider mappings");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting bulk model provider mappings");
        }
    }

    /// <summary>
    /// Enables multiple model provider mappings in a single operation
    /// </summary>
    /// <param name="ids">The IDs of the mappings to enable</param>
    /// <returns>The bulk update response with results</returns>
    [HttpPost("bulk/enable")]
    [ProducesResponseType(typeof(BulkUpdateResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> EnableBulkMappings([FromBody] List<int> ids)
    {
        return await UpdateBulkMappingsStatus(ids, true);
    }

    /// <summary>
    /// Disables multiple model provider mappings in a single operation
    /// </summary>
    /// <param name="ids">The IDs of the mappings to disable</param>
    /// <returns>The bulk update response with results</returns>
    [HttpPost("bulk/disable")]
    [ProducesResponseType(typeof(BulkUpdateResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DisableBulkMappings([FromBody] List<int> ids)
    {
        return await UpdateBulkMappingsStatus(ids, false);
    }

    private async Task<IActionResult> UpdateBulkMappingsStatus(List<int> ids, bool isEnabled)
    {
        try
        {
            if (ids == null || ids.Count == 0)
            {
                return BadRequest(new ErrorResponseDto("No mapping IDs provided"));
            }

            var updated = new List<ModelProviderMappingDto>();
            var errors = new List<string>();

            foreach (var id in ids)
            {
                try
                {
                    var existingMapping = await _mappingService.GetMappingByIdAsync(id);
                    if (existingMapping == null)
                    {
                        errors.Add($"Mapping with ID {id} not found");
                        continue;
                    }

                    existingMapping.IsEnabled = isEnabled;
                    var success = await _mappingService.UpdateMappingAsync(existingMapping);
                    
                    if (success)
                    {
                        updated.Add(existingMapping.ToDto());
                    }
                    else
                    {
                        errors.Add($"Failed to update mapping with ID {id}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating mapping with ID {Id}", id);
                    errors.Add($"Error updating mapping with ID {id}: {ex.Message}");
                }
            }

            var result = new BulkUpdateResult
            {
                Updated = updated,
                Errors = errors,
                TotalProcessed = ids.Count,
                SuccessCount = updated.Count,
                FailureCount = errors.Count
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating bulk model provider mappings status");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating bulk model provider mappings");
        }
    }

}

/// <summary>
/// Result of a bulk mapping operation
/// </summary>
public class BulkMappingResult
{
    /// <summary>
    /// Successfully created mappings
    /// </summary>
    public List<ModelProviderMappingDto> Created { get; set; } = new();

    /// <summary>
    /// Error messages for failed mappings
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Total number of mappings processed
    /// </summary>
    public int TotalProcessed { get; set; }

    /// <summary>
    /// Number of successful mappings
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed mappings
    /// </summary>
    public int FailureCount { get; set; }
}

/// <summary>
/// Result of a bulk delete operation
/// </summary>
public class BulkDeleteResult
{
    /// <summary>
    /// IDs of successfully deleted mappings
    /// </summary>
    public List<int> DeletedIds { get; set; } = new();

    /// <summary>
    /// Error messages for failed deletions
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Total number of mappings processed
    /// </summary>
    public int TotalProcessed { get; set; }

    /// <summary>
    /// Number of successful deletions
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed deletions
    /// </summary>
    public int FailureCount { get; set; }
}

/// <summary>
/// Result of a bulk update operation
/// </summary>
public class BulkUpdateResult
{
    /// <summary>
    /// Successfully updated mappings
    /// </summary>
    public List<ModelProviderMappingDto> Updated { get; set; } = new();

    /// <summary>
    /// Error messages for failed updates
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Total number of mappings processed
    /// </summary>
    public int TotalProcessed { get; set; }

    /// <summary>
    /// Number of successful updates
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed updates
    /// </summary>
    public int FailureCount { get; set; }
}

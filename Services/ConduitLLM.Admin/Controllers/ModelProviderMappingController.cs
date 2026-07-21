using ConduitLLM.Configuration.Interfaces;

using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Filters;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Core.Extensions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers;

/// <summary>
/// Controller for managing model provider mappings
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "MasterKeyPolicy")]
[ServiceFilter(typeof(OperationLoggingFilter))]
public class ModelProviderMappingController : AdminControllerBase
{
    private readonly IAdminModelProviderMappingService _mappingService;
    private readonly IProviderService _providerService;

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
        : base(logger)
    {
        _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
        _providerService = providerService ?? throw new ArgumentNullException(nameof(providerService));
    }

    /// <summary>
    /// Gets all model provider mappings
    /// </summary>
    /// <returns>A list of all model provider mappings</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ModelProviderMappingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllMappings()
    {
        var mappings = await _mappingService.GetAllMappingsAsync();
        var result = mappings.Select(m => m.ToDto());
        return Ok(result);
    }

    /// <summary>
    /// Gets a specific model provider mapping by ID
    /// </summary>
    /// <param name="id">The ID of the mapping to retrieve</param>
    /// <returns>The model provider mapping</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ModelProviderMappingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMappingById(int id)
    {
        var mapping = await _mappingService.GetMappingByIdAsync(id);
        if (mapping == null) { return this.NotFoundEntity("Model provider mapping", id); }
        return Ok(mapping.ToDto());
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
    public async Task<IActionResult> CreateMapping([FromBody] CreateModelProviderMappingDto mappingDto)
    {
        // An alias may have multiple providers, but never duplicate an alias/provider pair.
        var existingMappings = await _mappingService.GetAllMappingsAsync();
        var existingMapping = existingMappings.FirstOrDefault(m =>
            m.ModelAlias.Equals(mappingDto.ModelAlias, StringComparison.OrdinalIgnoreCase) &&
            m.ProviderId == mappingDto.ProviderId);
        if (existingMapping != null)
        {
            return Conflict(new ErrorResponseDto($"A mapping for alias '{mappingDto.ModelAlias}' and provider {mappingDto.ProviderId} already exists"));
        }

        var optionsError = ValidateProviderOptions(mappingDto.ProviderOptions);
        if (optionsError != null)
        {
            return BadRequest(new ErrorResponseDto(optionsError));
        }

        var mapping = mappingDto.ToEntity();
        var success = await _mappingService.AddMappingAsync(mapping);

        if (!success)
        {
            return BadRequest(new ErrorResponseDto("Failed to create model provider mapping. Please check the provider ID."));
        }

        var createdMapping = await _mappingService.GetMappingByIdAsync(mapping.Id);

        LogAdminAudit("Created", "ModelProviderMapping", createdMapping?.Id,
            $"ModelAlias: {LoggingSanitizer.S(mappingDto.ModelAlias)}, ProviderId: {mappingDto.ProviderId}");
        AdminOperationsMetricsService.RecordModelMappingOperation("create", "success");
        AdminOperationsMetricsService.RecordConfigurationChange("modelmapping", "create");

        return CreatedAtAction(nameof(GetMappingById), new { id = createdMapping?.Id }, createdMapping?.ToDto());
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
    public async Task<IActionResult> UpdateMapping(int id, [FromBody] UpdateModelProviderMappingDto mappingDto)
    {
        var existingMapping = await _mappingService.GetMappingByIdAsync(id);
        if (existingMapping == null)
        {
            throw new KeyNotFoundException($"Model provider mapping with ID '{id}' not found");
        }

        var optionsError = ValidateProviderOptions(mappingDto.ProviderOptions);
        if (optionsError != null)
        {
            return BadRequest(new ErrorResponseDto(optionsError));
        }

        existingMapping.UpdateFromDto(mappingDto);
        var success = await _mappingService.UpdateMappingAsync(existingMapping);

        if (!success)
        {
            throw new InvalidOperationException("Failed to update model provider mapping");
        }

        LogAdminAudit("Updated", "ModelProviderMapping", id);
        AdminOperationsMetricsService.RecordModelMappingOperation("update", "success");
        AdminOperationsMetricsService.RecordConfigurationChange("modelmapping", "update");

        return NoContent();
    }

    private static readonly string[] ForbiddenProviderOptionKeys = { "model", "messages", "stream", "stream_options" };

    /// <summary>
    /// Validates a mapping's ProviderOptions JSON. Returns an error message if invalid, else null.
    /// The value must be a JSON object and may not contain keys that would hijack the request
    /// (model/messages/stream/stream_options).
    /// </summary>
    private static string? ValidateProviderOptions(string? providerOptions)
    {
        if (string.IsNullOrWhiteSpace(providerOptions))
        {
            return null;
        }

        System.Text.Json.JsonDocument doc;
        try
        {
            doc = System.Text.Json.JsonDocument.Parse(providerOptions);
        }
        catch (System.Text.Json.JsonException)
        {
            return "ProviderOptions must be valid JSON.";
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                return "ProviderOptions must be a JSON object.";
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (ForbiddenProviderOptionKeys.Contains(prop.Name, StringComparer.OrdinalIgnoreCase))
                {
                    return $"ProviderOptions may not contain the reserved key '{prop.Name}'.";
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Deletes a model provider mapping
    /// </summary>
    /// <param name="id">The ID of the mapping to delete</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMapping(int id)
    {
        var existingMapping = await _mappingService.GetMappingByIdAsync(id);
        if (existingMapping == null)
        {
            throw new KeyNotFoundException($"Model provider mapping with ID '{id}' not found");
        }

        var success = await _mappingService.DeleteMappingAsync(id);

        if (!success)
        {
            throw new InvalidOperationException("Failed to delete model provider mapping");
        }

        LogAdminAudit("Deleted", "ModelProviderMapping", id);
        AdminOperationsMetricsService.RecordModelMappingOperation("delete", "success");
        AdminOperationsMetricsService.RecordConfigurationChange("modelmapping", "delete");

        return NoContent();
    }

    /// <summary>
    /// Gets all available providers
    /// </summary>
    /// <returns>List of providers with IDs and names</returns>
    [HttpGet("providers")]
    [ProducesResponseType(typeof(IEnumerable<Provider>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProviders()
    {
        var result = await _mappingService.GetProvidersAsync();
        return Ok(result);
    }

    /// <summary>
    /// Creates multiple model provider mappings in a single operation
    /// </summary>
    /// <param name="mappingDtos">The mappings to create</param>
    /// <returns>The bulk mapping response with results</returns>
    [HttpPost("bulk")]
    [ProducesResponseType(typeof(BulkMappingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateBulkMappings([FromBody] List<CreateModelProviderMappingDto> mappingDtos)
    {
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

        LogAdminAuditBulk("BulkCreated", "ModelProviderMapping", result.SuccessCount, result.FailureCount);
        AdminOperationsMetricsService.RecordModelMappingOperation("bulk_create", "success");

        return Ok(result);
    }

    /// <summary>
    /// Deletes multiple model provider mappings in a single operation
    /// </summary>
    /// <param name="ids">The IDs of the mappings to delete</param>
    /// <returns>The bulk delete response with results</returns>
    [HttpPost("bulk/delete")]
    [ProducesResponseType(typeof(BulkDeleteResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteBulkMappings([FromBody] List<int> ids)
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
                Logger.LogError(ex, "Error deleting mapping with ID {Id}", id);
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

        LogAdminAuditBulk("BulkDeleted", "ModelProviderMapping", result.SuccessCount, result.FailureCount);
        AdminOperationsMetricsService.RecordModelMappingOperation("bulk_delete", "success");

        return Ok(result);
    }

    /// <summary>
    /// Enables multiple model provider mappings in a single operation
    /// </summary>
    /// <param name="ids">The IDs of the mappings to enable</param>
    /// <returns>The bulk update response with results</returns>
    [HttpPost("bulk/enable")]
    [ProducesResponseType(typeof(BulkUpdateResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
    public async Task<IActionResult> DisableBulkMappings([FromBody] List<int> ids)
    {
        return await UpdateBulkMappingsStatus(ids, false);
    }

    private async Task<IActionResult> UpdateBulkMappingsStatus(List<int> ids, bool isEnabled)
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
                Logger.LogError(ex, "Error updating mapping with ID {Id}", id);
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

        LogAdminAuditBulk(isEnabled ? "BulkEnabled" : "BulkDisabled", "ModelProviderMapping", result.SuccessCount, result.FailureCount);
        AdminOperationsMetricsService.RecordModelMappingOperation(isEnabled ? "bulk_enable" : "bulk_disable", "success");

        return Ok(result);
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
    [System.ComponentModel.DataAnnotations.Required]
    public List<ModelProviderMappingDto> Created { get; set; } = new();

    /// <summary>
    /// Error messages for failed mappings
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Total number of mappings processed
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    public int TotalProcessed { get; set; }

    /// <summary>
    /// Number of successful mappings
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed mappings
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
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
    [System.ComponentModel.DataAnnotations.Required]
    public List<int> DeletedIds { get; set; } = new();

    /// <summary>
    /// Error messages for failed deletions
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Total number of mappings processed
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    public int TotalProcessed { get; set; }

    /// <summary>
    /// Number of successful deletions
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed deletions
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
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
    [System.ComponentModel.DataAnnotations.Required]
    public List<ModelProviderMappingDto> Updated { get; set; } = new();

    /// <summary>
    /// Error messages for failed updates
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Total number of mappings processed
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    public int TotalProcessed { get; set; }

    /// <summary>
    /// Number of successful updates
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed updates
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    public int FailureCount { get; set; }
}

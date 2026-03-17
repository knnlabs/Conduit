using ConduitLLM.Core.Extensions;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Core.Events;
using ConduitLLM.Functions.Interfaces;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers;

/// <summary>
/// Controller for managing function configurations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "MasterKeyPolicy")]
public class FunctionConfigurationsController : AdminControllerBase
{
    private readonly IFunctionConfigurationRepository _configurationRepository;

    /// <summary>
    /// Initializes a new instance of the FunctionConfigurationsController.
    /// </summary>
    public FunctionConfigurationsController(
        IFunctionConfigurationRepository configurationRepository,
        IPublishEndpoint? publishEndpoint,
        ILogger<FunctionConfigurationsController> logger)
        : base(publishEndpoint, logger)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
    }

    /// <summary>
    /// Gets all function configurations.
    /// </summary>
    /// <returns>List of all function configurations</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetAllConfigurations()
    {
        return ExecuteAsync(
            () => _configurationRepository.GetAllUnboundedAsync(),
            Ok,
            "GetAllConfigurations");
    }

    /// <summary>
    /// Gets a function configuration by ID.
    /// </summary>
    /// <param name="id">The ID of the function configuration</param>
    /// <returns>The function configuration</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetConfigurationById(int id)
    {
        return ExecuteWithNotFoundAsync(
            () => _configurationRepository.GetByIdAsync(id),
            Ok,
            "FunctionConfiguration",
            id,
            "GetConfigurationById");
    }

    /// <summary>
    /// Gets function configurations by provider type.
    /// </summary>
    /// <param name="providerType">The provider type (e.g., "Exa")</param>
    /// <returns>List of function configurations for the specified provider</returns>
    [HttpGet("provider/{providerType}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetConfigurationsByProvider(string providerType)
    {
        if (!Enum.TryParse<ConduitLLM.Functions.Enums.FunctionProviderType>(providerType, true, out var providerEnum))
        {
            return Task.FromResult<IActionResult>(BadRequest(new ErrorResponseDto($"Invalid provider type: {providerType}")));
        }

        return ExecuteAsync(
            () => _configurationRepository.GetByProviderTypeAsync(providerEnum),
            Ok,
            "GetConfigurationsByProvider",
            new { ProviderType = providerType });
    }

    /// <summary>
    /// Gets function configurations by purpose.
    /// </summary>
    /// <param name="purpose">The purpose (e.g., "Search", "Answer", "Enrich")</param>
    /// <returns>List of function configurations for the specified purpose</returns>
    [HttpGet("purpose/{purpose}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetConfigurationsByPurpose(string purpose)
    {
        if (!Enum.TryParse<ConduitLLM.Functions.Enums.FunctionPurpose>(purpose, true, out var purposeEnum))
        {
            return Task.FromResult<IActionResult>(BadRequest(new ErrorResponseDto($"Invalid purpose: {purpose}")));
        }

        return ExecuteAsync(
            () => _configurationRepository.GetByPurposeAsync(purposeEnum),
            Ok,
            "GetConfigurationsByPurpose",
            new { Purpose = purpose });
    }

    /// <summary>
    /// Creates a new function configuration.
    /// </summary>
    /// <param name="configuration">The function configuration to create</param>
    /// <returns>The created function configuration</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> CreateConfiguration(
        [FromBody] ConduitLLM.Functions.Entities.FunctionConfiguration configuration)
    {
        if (configuration == null)
        {
            return Task.FromResult<IActionResult>(BadRequest(new ErrorResponseDto("Function configuration data is required")));
        }

        return ExecuteAsync(
            async () =>
            {
                int id = await _configurationRepository.CreateAsync(configuration);

                // Fetch the created entity to return
                var created = await _configurationRepository.GetByIdAsync(id);

                // Audit log and publish event for cache invalidation
                if (created != null)
                {
                    LogAdminAudit("Created", "FunctionConfiguration", created.Id, $"Name: {LoggingSanitizer.S(created.ConfigurationName)}");
                    PublishEventFireAndForget(new FunctionConfigurationChanged
                    {
                        FunctionConfigurationId = created.Id,
                        ConfigurationName = created.ConfigurationName,
                        ProviderType = created.ProviderType.ToString(),
                        Purpose = created.Purpose.ToString(),
                        ChangeType = "Created",
                        ChangedProperties = new[] { "Created" },
                        IsEnabledChanged = false,
                        CacheTtlChanged = false,
                        CorrelationId = Guid.NewGuid().ToString()
                    }, "create function configuration",
                    new { ConfigName = created.ConfigurationName, ConfigId = created.Id });
                }

                return (id, created);
            },
            result => CreatedAtAction(
                nameof(GetConfigurationById),
                new { id = result.id },
                result.created),
            "CreateConfiguration");
    }

    /// <summary>
    /// Updates an existing function configuration.
    /// </summary>
    /// <param name="id">The ID of the function configuration to update</param>
    /// <param name="configuration">The updated function configuration data</param>
    /// <returns>The updated function configuration</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> UpdateConfiguration(
        int id,
        [FromBody] ConduitLLM.Functions.Entities.FunctionConfiguration configuration)
    {
        if (configuration == null)
        {
            return Task.FromResult<IActionResult>(BadRequest(new ErrorResponseDto("Function configuration data is required")));
        }

        if (id != configuration.Id)
        {
            return Task.FromResult<IActionResult>(BadRequest(new ErrorResponseDto("ID mismatch")));
        }

        return ExecuteWithNotFoundAsync(
            () => _configurationRepository.GetByIdAsync(id),
            async existing =>
            {
                // Detect changes for event publishing
                bool isEnabledChanged = existing.IsEnabled != configuration.IsEnabled;
                bool cacheTtlChanged = existing.CacheTtlMinutes != configuration.CacheTtlMinutes;
                var changedProperties = new List<string>();
                if (existing.ConfigurationName != configuration.ConfigurationName) changedProperties.Add("ConfigurationName");
                if (existing.ProviderType != configuration.ProviderType) changedProperties.Add("ProviderType");
                if (existing.Purpose != configuration.Purpose) changedProperties.Add("Purpose");
                if (existing.IsEnabled != configuration.IsEnabled) changedProperties.Add("IsEnabled");
                if (existing.BaseUrl != configuration.BaseUrl) changedProperties.Add("BaseUrl");
                if (existing.TimeoutSeconds != configuration.TimeoutSeconds) changedProperties.Add("TimeoutSeconds");
                if (existing.CacheTtlMinutes != configuration.CacheTtlMinutes) changedProperties.Add("CacheTtlMinutes");
                if (existing.ProviderSettings != configuration.ProviderSettings) changedProperties.Add("ProviderSettings");
                if (existing.ParameterSchema != configuration.ParameterSchema) changedProperties.Add("ParameterSchema");
                if (existing.Description != configuration.Description) changedProperties.Add("Description");

                await _configurationRepository.UpdateAsync(configuration);

                // Fetch the updated entity to return
                var updated = await _configurationRepository.GetByIdAsync(id);

                if (updated == null)
                {
                    return NotFound(new ErrorResponseDto("Function configuration not found after update"));
                }

                LogAdminAudit("Updated", "FunctionConfiguration", id,
                    changedProperties.Count > 0 ? $"Changed: {string.Join(", ", changedProperties)}" : null);

                // Publish FunctionConfigurationChanged event for cache invalidation
                if (changedProperties.Count > 0)
                {
                    PublishEventFireAndForget(new FunctionConfigurationChanged
                    {
                        FunctionConfigurationId = updated.Id,
                        ConfigurationName = updated.ConfigurationName,
                        ProviderType = updated.ProviderType.ToString(),
                        Purpose = updated.Purpose.ToString(),
                        ChangeType = "Updated",
                        ChangedProperties = changedProperties.ToArray(),
                        IsEnabledChanged = isEnabledChanged,
                        CacheTtlChanged = cacheTtlChanged,
                        CorrelationId = Guid.NewGuid().ToString()
                    }, "update function configuration",
                    new { ConfigName = updated.ConfigurationName, ConfigId = updated.Id, ChangedProps = string.Join(", ", changedProperties) });
                }

                return Ok(updated);
            },
            "FunctionConfiguration",
            id,
            "UpdateConfiguration");
    }

    /// <summary>
    /// Deletes a function configuration.
    /// </summary>
    /// <param name="id">The ID of the function configuration to delete</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> DeleteConfiguration(int id)
    {
        return ExecuteWithNotFoundAsync(
            () => _configurationRepository.GetByIdAsync(id),
            async toDelete =>
            {
                await _configurationRepository.DeleteAsync(id);
                LogAdminAudit("Deleted", "FunctionConfiguration", id, $"Name: {LoggingSanitizer.S(toDelete.ConfigurationName)}");

                // Publish FunctionConfigurationChanged event for cache invalidation
                PublishEventFireAndForget(new FunctionConfigurationChanged
                {
                    FunctionConfigurationId = toDelete.Id,
                    ConfigurationName = toDelete.ConfigurationName,
                    ProviderType = toDelete.ProviderType.ToString(),
                    Purpose = toDelete.Purpose.ToString(),
                    ChangeType = "Deleted",
                    ChangedProperties = new[] { "Deleted" },
                    IsEnabledChanged = false,
                    CacheTtlChanged = false,
                    CorrelationId = Guid.NewGuid().ToString()
                }, "delete function configuration",
                new { ConfigName = toDelete.ConfigurationName, ConfigId = toDelete.Id });

                return NoContent();
            },
            "FunctionConfiguration",
            id,
            "DeleteConfiguration");
    }
}

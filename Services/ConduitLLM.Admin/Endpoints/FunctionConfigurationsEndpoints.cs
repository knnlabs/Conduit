using ConduitLLM.Core.Extensions;
using ConduitLLM.Admin.Auditing;
using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Services;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;
using ConduitLLM.Functions.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Endpoints;

/// <summary>
/// Controller for managing function configurations.
/// </summary>
public class FunctionConfigurationsEndpoints
{
    private readonly IFunctionConfigurationRepository _configurationRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<FunctionConfigurationsEndpoints> _logger;

    /// <summary>
    /// Initializes the Function Configurations endpoint handler.
    /// </summary>
    public FunctionConfigurationsEndpoints(
        IFunctionConfigurationRepository configurationRepository,
        IEventPublisher eventPublisher,
        IHttpContextAccessor httpContextAccessor,
        ILogger<FunctionConfigurationsEndpoints> logger)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _eventPublisher = eventPublisher;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public static IEndpointRouteBuilder MapFunctionConfigurationsEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/FunctionConfigurations")
            .RequireAuthorization("MasterKeyPolicy")
            .AddEndpointFilter<ValidationEndpointFilter>()
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Function Configurations");
        group.MapGet("/", ([FromServices] FunctionConfigurationsEndpoints e) => e.GetAllConfigurations())
            .WithName("FunctionConfigurations_GetAll").Produces<List<FunctionConfiguration>>();
        group.MapGet("/{id}", ([FromServices] FunctionConfigurationsEndpoints e, int id) => e.GetConfigurationById(id))
            .WithName("FunctionConfigurations_GetById").Produces<FunctionConfiguration>().Produces(StatusCodes.Status404NotFound);
        group.MapGet("/provider/{providerType}", ([FromServices] FunctionConfigurationsEndpoints e, string providerType) => e.GetConfigurationsByProvider(providerType))
            .WithName("FunctionConfigurations_GetByProvider").Produces<List<FunctionConfiguration>>();
        group.MapGet("/purpose/{purpose}", ([FromServices] FunctionConfigurationsEndpoints e, string purpose) => e.GetConfigurationsByPurpose(purpose))
            .WithName("FunctionConfigurations_GetByPurpose").Produces<List<FunctionConfiguration>>();
        group.MapPost("/", ([FromServices] FunctionConfigurationsEndpoints e, FunctionConfiguration configuration) => e.CreateConfiguration(configuration))
            .WithName("FunctionConfigurations_Create").Produces<FunctionConfiguration>(StatusCodes.Status201Created).Produces(StatusCodes.Status400BadRequest);
        group.MapPut("/{id}", ([FromServices] FunctionConfigurationsEndpoints e, int id, FunctionConfiguration configuration) => e.UpdateConfiguration(id, configuration))
            .WithName("FunctionConfigurations_Update").Produces<FunctionConfiguration>().Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);
        group.MapDelete("/{id}", ([FromServices] FunctionConfigurationsEndpoints e, int id) => e.DeleteConfiguration(id))
            .WithName("FunctionConfigurations_Delete").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound);
        return app;
    }

    /// <summary>
    /// Gets all function configurations.
    /// </summary>
    /// <returns>List of all function configurations</returns>
    public async Task<IResult> GetAllConfigurations()
    {
        var configurations = await _configurationRepository.GetAllUnboundedAsync();
        return Results.Ok(configurations);
    }

    /// <summary>
    /// Gets a function configuration by ID.
    /// </summary>
    /// <param name="id">The ID of the function configuration</param>
    /// <returns>The function configuration</returns>
    public async Task<IResult> GetConfigurationById(int id)
    {
        var configuration = await _configurationRepository.GetByIdAsync(id);
        if (configuration == null)
        {
            return AdminResults.NotFoundEntity("FunctionConfiguration", id);
        }
        return Results.Ok(configuration);
    }

    /// <summary>
    /// Gets function configurations by provider type.
    /// </summary>
    /// <param name="providerType">The provider type (e.g., "Exa")</param>
    /// <returns>List of function configurations for the specified provider</returns>
    public async Task<IResult> GetConfigurationsByProvider(string providerType)
    {
        if (!Enum.TryParse<ConduitLLM.Functions.Enums.FunctionProviderType>(providerType, true, out var providerEnum))
        {
            return Results.BadRequest(new ErrorResponseDto($"Invalid provider type: {providerType}"));
        }

        var configurations = await _configurationRepository.GetByProviderTypeAsync(providerEnum);
        return Results.Ok(configurations);
    }

    /// <summary>
    /// Gets function configurations by purpose.
    /// </summary>
    /// <param name="purpose">The purpose (e.g., "Search", "Answer", "Enrich")</param>
    /// <returns>List of function configurations for the specified purpose</returns>
    public async Task<IResult> GetConfigurationsByPurpose(string purpose)
    {
        if (!Enum.TryParse<ConduitLLM.Functions.Enums.FunctionPurpose>(purpose, true, out var purposeEnum))
        {
            return Results.BadRequest(new ErrorResponseDto($"Invalid purpose: {purpose}"));
        }

        var configurations = await _configurationRepository.GetByPurposeAsync(purposeEnum);
        return Results.Ok(configurations);
    }

    /// <summary>
    /// Creates a new function configuration.
    /// </summary>
    /// <param name="configuration">The function configuration to create</param>
    /// <returns>The created function configuration</returns>
    public async Task<IResult> CreateConfiguration(FunctionConfiguration configuration)
    {
        if (configuration == null)
        {
            return Results.BadRequest(new ErrorResponseDto("Function configuration data is required"));
        }

        int id = await _configurationRepository.CreateAsync(configuration);

        // Fetch the created entity to return
        var created = await _configurationRepository.GetByIdAsync(id);

        // Audit log and publish event for cache invalidation
        if (created != null)
        {
            LogAdminAudit("Created", "FunctionConfiguration", created.Id, $"Name: {LoggingSanitizer.S(created.ConfigurationName)}");
            AdminOperationsMetricsService.RecordConfigurationChange("functionconfiguration", "create");
            _eventPublisher.PublishFireAndForget(new FunctionConfigurationChanged
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

        return Results.Created($"/api/FunctionConfigurations/{id}", created);
    }

    /// <summary>
    /// Updates an existing function configuration.
    /// </summary>
    /// <param name="id">The ID of the function configuration to update</param>
    /// <param name="configuration">The updated function configuration data</param>
    /// <returns>The updated function configuration</returns>
    public async Task<IResult> UpdateConfiguration(
        int id,
        FunctionConfiguration configuration)
    {
        if (configuration == null)
        {
            return Results.BadRequest(new ErrorResponseDto("Function configuration data is required"));
        }

        if (id != configuration.Id)
        {
            return Results.BadRequest(new ErrorResponseDto("ID mismatch"));
        }

        var existing = await _configurationRepository.GetByIdAsync(id);
        if (existing == null)
        {
            return AdminResults.NotFoundEntity("FunctionConfiguration", id);
        }

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
            return Results.NotFound(new ErrorResponseDto("Function configuration not found after update"));
        }

        LogAdminAudit("Updated", "FunctionConfiguration", id,
            changedProperties.Count > 0 ? $"Changed: {string.Join(", ", changedProperties)}" : null);
        AdminOperationsMetricsService.RecordConfigurationChange("functionconfiguration", "update");

        // Publish FunctionConfigurationChanged event for cache invalidation
        if (changedProperties.Count > 0)
        {
            _eventPublisher.PublishFireAndForget(new FunctionConfigurationChanged
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

        return Results.Ok(updated);
    }

    /// <summary>
    /// Deletes a function configuration.
    /// </summary>
    /// <param name="id">The ID of the function configuration to delete</param>
    /// <returns>No content on success</returns>
    public async Task<IResult> DeleteConfiguration(int id)
    {
        var toDelete = await _configurationRepository.GetByIdAsync(id);
        if (toDelete == null)
        {
            return AdminResults.NotFoundEntity("FunctionConfiguration", id);
        }

        await _configurationRepository.DeleteAsync(id);
        LogAdminAudit("Deleted", "FunctionConfiguration", id, $"Name: {LoggingSanitizer.S(toDelete.ConfigurationName)}");
        AdminOperationsMetricsService.RecordConfigurationChange("functionconfiguration", "delete");

        // Publish FunctionConfigurationChanged event for cache invalidation
        _eventPublisher.PublishFireAndForget(new FunctionConfigurationChanged
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

        return Results.NoContent();
    }

    private void LogAdminAudit(string operation, string entityType, object? entityId = null, string? detail = null) =>
        AdminAudit.Log(_httpContextAccessor.HttpContext!, _logger, operation, entityType, entityId, detail);
}

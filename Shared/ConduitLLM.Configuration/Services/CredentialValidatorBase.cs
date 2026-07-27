using System.Linq.Expressions;
using ConduitLLM.Functions.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Services;

/// <summary>
/// Base class for credential validators that share the same validation state machine:
/// add (max count), set-primary (must be enabled), disable (must not be primary),
/// and has-enabled (at least one enabled in group).
/// </summary>
public abstract class CredentialValidatorBase<TEntity> where TEntity : class, ICredentialEntity
{
    private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
    private readonly ILogger _logger;

    /// <summary>Maximum number of credentials allowed per group.</summary>
    protected abstract int MaxPerGroup { get; }

    /// <summary>Human-readable name for the entity (e.g. "key" or "credential").</summary>
    protected abstract string EntityName { get; }

    /// <summary>Human-readable name for the group (e.g. "provider" or "provider type").</summary>
    protected abstract string GroupName { get; }

    /// <summary>Returns the DbSet for this entity type from the given context.</summary>
    protected abstract DbSet<TEntity> GetDbSet(ConduitDbContext context);

    protected CredentialValidatorBase(IDbContextFactory<ConduitDbContext> dbContextFactory, ILogger logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates that adding a new credential to the group would not exceed the maximum.
    /// </summary>
    protected async Task<ConduitValidationResult> ValidateAddAsync(
        Expression<Func<TEntity, bool>> groupPredicate,
        CancellationToken cancellationToken = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var currentCount = await GetDbSet(context)
            .CountAsync(groupPredicate, cancellationToken);

        if (currentCount >= MaxPerGroup)
        {
            _logger.LogWarning(
                "Credential add rejected: {GroupName} already has {CurrentCount}/{MaxPerGroup} {EntityName}s",
                GroupName, currentCount, MaxPerGroup, EntityName);
            return ConduitValidationResult.Failure(
                $"{GroupName} already has the maximum of {MaxPerGroup} {EntityName}s");
        }

        return ConduitValidationResult.Success();
    }

    /// <summary>
    /// Validates that the credential can be set as primary (must exist and be enabled).
    /// </summary>
    public async Task<ConduitValidationResult> ValidateSetPrimaryAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await GetDbSet(context)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity == null)
        {
            _logger.LogWarning("Set-primary rejected: {EntityName} {Id} not found", EntityName, id);
            return ConduitValidationResult.Failure($"{EntityName} not found");
        }

        if (!entity.IsEnabled)
        {
            _logger.LogWarning("Set-primary rejected: {EntityName} {Id} is disabled", EntityName, id);
            return ConduitValidationResult.Failure($"Cannot set a disabled {EntityName} as primary");
        }

        return ConduitValidationResult.Success();
    }

    /// <summary>
    /// Validates that the credential can be disabled (must exist and not be primary).
    /// </summary>
    public async Task<ConduitValidationResult> ValidateDisableAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await GetDbSet(context)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity == null)
        {
            _logger.LogWarning("Disable rejected: {EntityName} {Id} not found", EntityName, id);
            return ConduitValidationResult.Failure($"{EntityName} not found");
        }

        if (entity.IsPrimary)
        {
            _logger.LogWarning("Disable rejected: {EntityName} {Id} is primary", EntityName, id);
            return ConduitValidationResult.Failure(
                $"Cannot disable a primary {EntityName}. Set another {EntityName} as primary first.");
        }

        return ConduitValidationResult.Success();
    }

    /// <summary>
    /// Validates that at least one credential in the group is enabled.
    /// </summary>
    protected async Task<ConduitValidationResult> ValidateHasEnabledAsync(
        Expression<Func<TEntity, bool>> groupAndEnabledPredicate,
        CancellationToken cancellationToken = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var hasEnabled = await GetDbSet(context)
            .AnyAsync(groupAndEnabledPredicate, cancellationToken);

        if (!hasEnabled)
        {
            _logger.LogWarning(
                "Validation failed: {GroupName} has no enabled {EntityName}s",
                GroupName, EntityName);
            return ConduitValidationResult.Failure(
                $"{GroupName} must have at least one enabled {EntityName}");
        }

        return ConduitValidationResult.Success();
    }
}

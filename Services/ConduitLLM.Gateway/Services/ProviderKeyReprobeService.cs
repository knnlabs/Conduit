using System.Net;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Events;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Options;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Gateway.Services;

/// <summary>
/// Half-open recovery worker for provider keys disabled due to insufficient balance.
/// </summary>
public sealed class ProviderKeyReprobeService : PeriodicCollectorBackgroundService
{
    private readonly IRedisErrorStore _errorStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ProviderKeyReprobeOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProviderKeyReprobeService> _logger;

    public ProviderKeyReprobeService(
        IRedisErrorStore errorStore,
        IServiceScopeFactory scopeFactory,
        IOptions<ProviderKeyReprobeOptions> options,
        ILogger<ProviderKeyReprobeService> logger,
        TimeProvider? timeProvider = null)
        : base(
            logger,
            options.Value.ScanInterval,
            timeProvider: timeProvider)
    {
        _errorStore = errorStore;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override bool IsCollectorEnabled => _options.Enabled;

    protected override Task CollectOnceAsync(CancellationToken cancellationToken) =>
        RunOnceAsync(cancellationToken);

    protected override void OnCollectorDisabled() =>
        _logger.LogInformation("Provider key balance reprobes are disabled");

    protected override void OnCollectionFailed(Exception exception) =>
        _logger.LogError(exception, "Provider key reprobe scan failed");

    /// <summary>
    /// Executes one scan. Public for deterministic operational tests.
    /// </summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var states = await _errorStore.GetDisabledKeyReprobeStatesAsync();
        var eligibleStates = states
            .Where(state =>
                state.ErrorType == ProviderErrorType.InsufficientBalance &&
                GetNextEligibleAt(state) <= now)
            .OrderBy(GetNextEligibleAt)
            .Take(Math.Max(1, _options.BatchSize));

        foreach (var state in eligibleStates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await _errorStore.TryAcquireKeyReprobeAsync(
                    state.KeyId, _options.ProbeLockTtl))
            {
                continue;
            }

            await ProbeKeyAsync(state, now, cancellationToken);
        }
    }

    private DateTime GetNextEligibleAt(DisabledKeyReprobeState state) =>
        state.NextAttemptAt ?? state.DisabledAt + _options.InitialCooldown;

    private async Task ProbeKeyAsync(
        DisabledKeyReprobeState state,
        DateTime attemptedAt,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var keyRepository =
            scope.ServiceProvider.GetRequiredService<IProviderKeyCredentialRepository>();
        var providerRepository =
            scope.ServiceProvider.GetRequiredService<IProviderRepository>();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ILLMClientFactory>();

        var key = await keyRepository.GetByIdAsync(state.KeyId);
        if (key == null)
        {
            await _errorStore.ClearErrorsForKeyAsync(state.KeyId);
            return;
        }

        var provider = await providerRepository.GetByIdAsync(key.ProviderId);
        if (provider == null)
        {
            await _errorStore.ClearErrorsForKeyAsync(state.KeyId);
            return;
        }

        try
        {
            var client = clientFactory.CreateTestClient(provider, key);
            await client.ListModelsAsync(cancellationToken: cancellationToken);
            await ReenableAsync(
                scope.ServiceProvider,
                providerRepository,
                keyRepository,
                provider,
                key,
                cancellationToken);
        }
        catch (Exception ex)
        {
            var classification = ClassifyProbeFailure(ex);
            if (classification == ProviderErrorType.InvalidApiKey)
            {
                await _errorStore.MarkKeyReprobeRequiresManualAsync(
                    state.KeyId, ProviderErrorType.InvalidApiKey);
                _logger.LogWarning(
                    "Balance reprobe for key {KeyId} returned 401; manual credential repair is required",
                    state.KeyId);
                return;
            }

            var attemptCount = state.AttemptCount + 1;
            var nextAttemptAt = attemptedAt + CalculateBackoff(attemptCount);
            await _errorStore.RecordKeyReprobeAttemptAsync(
                state.KeyId,
                attemptCount,
                attemptedAt,
                nextAttemptAt);
            _logger.LogInformation(
                ex,
                "Balance reprobe for key {KeyId} failed; next attempt at {NextAttemptAt}",
                state.KeyId,
                nextAttemptAt);
        }
    }

    private async Task ReenableAsync(
        IServiceProvider serviceProvider,
        IProviderRepository providerRepository,
        IProviderKeyCredentialRepository keyRepository,
        Provider provider,
        ProviderKeyCredential key,
        CancellationToken cancellationToken)
    {
        var summary = await _errorStore.GetProviderSummaryAsync(provider.Id);
        var providerWasAutoDisabled =
            summary?.ProviderDisabledAt != null &&
            summary.ProviderDisableReason == ProviderErrorTrackingService.AllKeysDisabledReason;

        var keysToRecover = new List<ProviderKeyCredential> { key };
        if (key.ProviderAccountGroup > 0)
        {
            var reprobeStates = await _errorStore.GetDisabledKeyReprobeStatesAsync();
            var balanceDisabledIds = reprobeStates
                .Where(state => state.ErrorType == ProviderErrorType.InsufficientBalance)
                .Select(state => state.KeyId)
                .ToHashSet();
            var allProviderKeys = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                keyRepository.GetByProviderIdPaginatedAsync, provider.Id);
            keysToRecover.AddRange(allProviderKeys.Where(candidate =>
                candidate.Id != key.Id &&
                candidate.ProviderAccountGroup == key.ProviderAccountGroup &&
                balanceDisabledIds.Contains(candidate.Id)));
        }

        foreach (var recoveryKey in keysToRecover.Where(candidate => !candidate.IsEnabled))
        {
            recoveryKey.IsEnabled = true;
            await keyRepository.UpdateAsync(recoveryKey, cancellationToken);
        }

        if (providerWasAutoDisabled && !provider.IsEnabled)
        {
            provider.IsEnabled = true;
            await providerRepository.UpdateAsync(provider, cancellationToken);
            await _errorStore.ClearProviderDisabledAsync(provider.Id);
        }

        foreach (var recoveryKey in keysToRecover)
        {
            await _errorStore.ClearErrorsForKeyAsync(recoveryKey.Id, provider.Id);
        }

        var eventBus = serviceProvider.GetService<IEventBus>();
        if (eventBus != null)
        {
            await eventBus.PublishAsync(new ProviderKeyReenabledEvent
            {
                KeyId = key.Id,
                ProviderId = provider.Id,
                ReenabledBy = "auto-reprobe",
                Reason = "Provider balance or quota recovered",
                ReenabledAt = _timeProvider.GetUtcNow().UtcDateTime,
                AffectedKeyIds = keysToRecover.Select(candidate => candidate.Id).ToArray(),
                ProviderAccountGroup = keysToRecover.Count > 1
                    ? key.ProviderAccountGroup
                    : (short)0
            }, cancellationToken);
        }

        _logger.LogInformation(
            "Automatically re-enabled provider key {KeyId} after a successful balance reprobe",
            key.Id);
    }

    private TimeSpan CalculateBackoff(int attemptCount)
    {
        var multiplier = Math.Pow(2, Math.Min(attemptCount, 30));
        var ticks = Math.Min(
            _options.InitialCooldown.Ticks * multiplier,
            _options.MaxCooldown.Ticks);
        return TimeSpan.FromTicks((long)ticks);
    }

    internal static ProviderErrorType ClassifyProbeFailure(Exception exception)
    {
        var communicationException = LLMCommunicationException.FindWithStatus(exception);
        var details = $"{communicationException?.ResponseBody} {exception.Message}";

        var classified = ProviderErrorClassifier.Classify(communicationException?.StatusCode, details);
        if (classified != ProviderErrorType.Unknown)
        {
            return classified;
        }

        // No usable status anywhere in the chain — fall back to the balance keywords so a
        // still-unfunded key keeps its InsufficientBalance classification during reprobe.
        return ProviderErrorClassifier.IsBalanceResponse(details)
            ? ProviderErrorType.InsufficientBalance
            : ProviderErrorType.Unknown;
    }
}

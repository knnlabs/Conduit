using System.Net;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Events;
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
public sealed class ProviderKeyReprobeService : BackgroundService
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
    {
        _errorStore = errorStore;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Provider key balance reprobes are disabled");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Provider key reprobe scan failed");
            }

            await Task.Delay(_options.ScanInterval, _timeProvider, stoppingToken);
        }
    }

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

        if (!key.IsEnabled)
        {
            key.IsEnabled = true;
            await keyRepository.UpdateAsync(key, cancellationToken);
        }

        if (providerWasAutoDisabled && !provider.IsEnabled)
        {
            provider.IsEnabled = true;
            await providerRepository.UpdateAsync(provider, cancellationToken);
            await _errorStore.ClearProviderDisabledAsync(provider.Id);
        }

        await _errorStore.ClearErrorsForKeyAsync(key.Id, provider.Id);

        var eventBus = serviceProvider.GetService<IEventBus>();
        if (eventBus != null)
        {
            await eventBus.PublishAsync(new ProviderKeyReenabledEvent
            {
                KeyId = key.Id,
                ProviderId = provider.Id,
                ReenabledBy = "auto-reprobe",
                Reason = "Provider balance or quota recovered",
                ReenabledAt = _timeProvider.GetUtcNow().UtcDateTime
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
        var communicationException = FindCommunicationException(exception);
        if (communicationException?.StatusCode == HttpStatusCode.Unauthorized)
        {
            return ProviderErrorType.InvalidApiKey;
        }

        if (communicationException?.StatusCode == HttpStatusCode.PaymentRequired)
        {
            return ProviderErrorType.InsufficientBalance;
        }

        var details = $"{communicationException?.ResponseBody} {exception.Message}";
        return details.Contains("balance", StringComparison.OrdinalIgnoreCase) ||
               details.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
               details.Contains("payment required", StringComparison.OrdinalIgnoreCase)
            ? ProviderErrorType.InsufficientBalance
            : ProviderErrorType.Unknown;
    }

    private static LLMCommunicationException? FindCommunicationException(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException!)
        {
            if (current is LLMCommunicationException communicationException)
            {
                return communicationException;
            }

            if (current.InnerException == null)
            {
                break;
            }
        }

        return null;
    }
}

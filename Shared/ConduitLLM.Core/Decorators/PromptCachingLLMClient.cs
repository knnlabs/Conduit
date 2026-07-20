using System.Runtime.CompilerServices;
using System.Text.Json;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Metrics;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Decorators;

/// <summary>
/// Decorator that automatically injects cache_control directives into chat completion
/// requests when prompt caching auto-injection is enabled via GlobalSettings.
/// </summary>
public class PromptCachingLLMClient : ILLMClient, ILLMClientDecorator, IAuthenticationVerifiable
{
    private readonly ILLMClient _innerClient;
    private readonly IGlobalSettingsCacheService _settingsService;
    private readonly ILogger<PromptCachingLLMClient> _logger;

    /// <summary>
    /// GlobalSettings key for the prompt caching configuration.
    /// </summary>
    public const string SettingsKey = "PromptCaching.Config";

    public PromptCachingLLMClient(
        ILLMClient innerClient,
        IGlobalSettingsCacheService settingsService,
        ILogger<PromptCachingLLMClient> logger)
    {
        _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public ILLMClient InnerClient => _innerClient;

    /// <inheritdoc />
    public async Task<ChatCompletionResponse> CreateChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        await TryInjectCacheControlAsync(request);
        return await _innerClient.CreateChatCompletionAsync(request, apiKey, cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await TryInjectCacheControlAsync(request);
        await foreach (var chunk in _innerClient.StreamChatCompletionAsync(request, apiKey, cancellationToken)
            .WithCancellation(cancellationToken))
        {
            yield return chunk;
        }
    }

    /// <inheritdoc />
    public Task<List<string>> ListModelsAsync(string? apiKey = null, CancellationToken cancellationToken = default)
        => _innerClient.ListModelsAsync(apiKey, cancellationToken);

    /// <inheritdoc />
    public Task<EmbeddingResponse> CreateEmbeddingAsync(EmbeddingRequest request, string? apiKey = null, CancellationToken cancellationToken = default)
        => _innerClient.CreateEmbeddingAsync(request, apiKey, cancellationToken);

    /// <inheritdoc />
    public Task<ImageGenerationResponse> CreateImageAsync(ImageGenerationRequest request, string? apiKey = null, CancellationToken cancellationToken = default)
        => _innerClient.CreateImageAsync(request, apiKey, cancellationToken);

    /// <summary>
    /// Generates video by forwarding the optional provider capability through this decorator.
    /// </summary>
    public Task<VideoGenerationResponse> CreateVideoAsync(
        VideoGenerationRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
        => InvokeVideoGenerationAsync(request, apiKey, cancellationToken);

    /// <inheritdoc />
    public Task<ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null)
        => _innerClient.GetCapabilitiesAsync(modelId);

    private async Task<VideoGenerationResponse> InvokeVideoGenerationAsync(
        VideoGenerationRequest request,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        object target = _innerClient.UnwrapInnermost();
        System.Reflection.MethodInfo? method = null;
        for (ILLMClient? current = _innerClient; current != null;
             current = (current as ILLMClientDecorator)?.InnerClient)
        {
            method = current.GetType().GetMethod(
                nameof(CreateVideoAsync),
                new[] { typeof(VideoGenerationRequest), typeof(string), typeof(CancellationToken) });
            if (method != null)
            {
                target = current;
                break;
            }
        }

        if (method?.Invoke(target, new object?[] { request, apiKey, cancellationToken })
            is not Task<VideoGenerationResponse> task)
        {
            throw new NotSupportedException(
                $"The underlying client {target.GetType().Name} does not support video generation");
        }

        return await task;
    }

    /// <summary>
    /// Verifies authentication by delegating to the inner client if it supports
    /// <see cref="IAuthenticationVerifiable"/>.
    /// </summary>
    public Task<AuthenticationResult> VerifyAuthenticationAsync(
        string? apiKey = null,
        string? baseUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (_innerClient is IAuthenticationVerifiable authVerifiable)
        {
            return authVerifiable.VerifyAuthenticationAsync(apiKey, baseUrl, cancellationToken);
        }

        return Task.FromResult(AuthenticationResult.Failure(
            "Provider does not support authentication verification",
            $"The {_innerClient.GetType().Name} client has not implemented authentication verification"));
    }

    /// <summary>
    /// Gets the health check URL by delegating to the inner client if it supports
    /// <see cref="IAuthenticationVerifiable"/>.
    /// </summary>
    public string GetHealthCheckUrl(string? baseUrl = null)
    {
        if (_innerClient is IAuthenticationVerifiable authVerifiable)
        {
            return authVerifiable.GetHealthCheckUrl(baseUrl);
        }

        return baseUrl ?? "https://api.provider.com/health";
    }

    private async Task TryInjectCacheControlAsync(ChatCompletionRequest request)
    {
        try
        {
            var config = await GetPromptCachingConfigAsync();
            if (config is { AutoInjectEnabled: true })
            {
                PromptCacheInjectionService.InjectCacheControl(request, config);
                PromptCachingInjectionMetrics.RecordSuccess(request.Model ?? "unknown");
                _logger.LogDebug("Injected cache_control directives for model {Model}", request.Model);
            }
        }
        catch (Exception ex)
        {
            // Don't fail the request if cache injection fails — just log and continue
            PromptCachingInjectionMetrics.RecordError(request.Model ?? "unknown");
            _logger.LogWarning(ex, "Failed to inject cache_control directives, continuing without caching");
        }
    }

    private async Task<PromptCachingConfig?> GetPromptCachingConfigAsync()
    {
        var json = await _settingsService.GetSettingValueAsync(SettingsKey);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<PromptCachingConfig>(json);
    }
}

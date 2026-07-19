using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Decorators
{
    /// <summary>
    /// Decorator that sets provider key context and tracks errors for LLM operations
    /// </summary>
    public class ContextAwareLLMClient : ILLMClient, ILLMClientDecorator, IAuthenticationVerifiable
    {
        private readonly ILLMClient _innerClient;
        private readonly int _keyId;
        private readonly int _providerId;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ContextAwareLLMClient>? _logger;

        public ContextAwareLLMClient(
            ILLMClient innerClient, 
            int keyId, 
            int providerId,
            IServiceProvider serviceProvider)
        {
            _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
            _keyId = keyId;
            _providerId = providerId;
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = serviceProvider.GetService<ILogger<ContextAwareLLMClient>>();
        }

        /// <inheritdoc />
        public ILLMClient InnerClient => _innerClient;

        public async Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            using (ProviderKeyContext.Set(_keyId, _providerId))
            {
                try
                {
                    return await _innerClient.CreateChatCompletionAsync(request, apiKey, cancellationToken);
                }
                catch (LLMCommunicationException ex)
                {
                    await TrackErrorAsync(ex);
                    throw;
                }
            }
        }

        public async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using (ProviderKeyContext.Set(_keyId, _providerId))
            {
                var sourceStream = _innerClient.StreamChatCompletionAsync(request, apiKey, cancellationToken);
                var enumerator = sourceStream.GetAsyncEnumerator(cancellationToken);
                bool errorTracked = false;
                
                try
                {
                    while (true)
                    {
                        ChatCompletionChunk current;
                        bool hasNext;
                        
                        try
                        {
                            hasNext = await enumerator.MoveNextAsync();
                            if (!hasNext) break;
                            current = enumerator.Current;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(
                                "Caught exception in streaming: Type={ExceptionType}, Message={Message}, HasStatusCode={HasStatusCode}",
                                ex.GetType().Name, ex.Message.Substring(0, Math.Min(ex.Message.Length, 200)), (ex as LLMCommunicationException)?.StatusCode);

                            // Track error only once per stream
                            if (!errorTracked)
                            {
                                // Try to extract LLMCommunicationException from nested exceptions
                                var llmEx = ExtractLLMCommunicationException(ex);
                                if (llmEx != null)
                                {
                                    _logger?.LogWarning(
                                        "Extracted LLMCommunicationException: StatusCode={StatusCode}",
                                        llmEx.StatusCode);
                                    await TrackErrorAsync(llmEx);
                                    errorTracked = true;
                                }
                                else
                                {
                                    _logger?.LogWarning("Could not extract LLMCommunicationException from {ExceptionType}", ex.GetType().Name);
                                }
                            }
                            throw;
                        }
                        
                        yield return current;
                    }
                }
                finally
                {
                    await enumerator.DisposeAsync();
                }
            }
        }

        public async Task<List<string>> ListModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            using (ProviderKeyContext.Set(_keyId, _providerId))
            {
                try
                {
                    return await _innerClient.ListModelsAsync(apiKey, cancellationToken);
                }
                catch (LLMCommunicationException ex)
                {
                    await TrackErrorAsync(ex);
                    throw;
                }
            }
        }

        public async Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            using (ProviderKeyContext.Set(_keyId, _providerId))
            {
                try
                {
                    return await _innerClient.CreateEmbeddingAsync(request, apiKey, cancellationToken);
                }
                catch (LLMCommunicationException ex)
                {
                    await TrackErrorAsync(ex);
                    throw;
                }
            }
        }

        public async Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            using (ProviderKeyContext.Set(_keyId, _providerId))
            {
                try
                {
                    return await _innerClient.CreateImageAsync(request, apiKey, cancellationToken);
                }
                catch (LLMCommunicationException ex)
                {
                    await TrackErrorAsync(ex);
                    throw;
                }
            }
        }

        public async Task<VideoGenerationResponse> CreateVideoAsync(
            VideoGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            using (ProviderKeyContext.Set(_keyId, _providerId))
            {
                try
                {
                    // CreateVideoAsync is not on ILLMClient — only specific providers implement it.
                    // The inner client may itself be a decorator (e.g. PromptCachingLLMClient)
                    // that hides the provider's video capability, so unwrap the chain to the
                    // innermost provider client before reflecting (issue #976).
                    var providerClient = _innerClient.UnwrapInnermost();
                    var providerClientType = providerClient.GetType();
                    var createVideoMethod = providerClientType.GetMethod("CreateVideoAsync",
                        new[] { typeof(VideoGenerationRequest), typeof(string), typeof(CancellationToken) });

                    if (createVideoMethod == null)
                    {
                        throw new NotSupportedException(
                            $"The underlying client {providerClientType.Name} does not support video generation");
                    }

                    var task = (Task<VideoGenerationResponse>?)createVideoMethod.Invoke(
                        providerClient, new object?[] { request, apiKey, cancellationToken });

                    if (task == null)
                    {
                        throw new InvalidOperationException(
                            $"CreateVideoAsync on {providerClientType.Name} returned null");
                    }

                    return await task;
                }
                catch (LLMCommunicationException ex)
                {
                    await TrackErrorAsync(ex);
                    throw;
                }
            }
        }

        public async Task<ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null)
        {
            using (ProviderKeyContext.Set(_keyId, _providerId))
            {
                try
                {
                    return await _innerClient.GetCapabilitiesAsync(modelId);
                }
                catch (LLMCommunicationException ex)
                {
                    await TrackErrorAsync(ex);
                    throw;
                }
            }
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

        private static LLMCommunicationException? ExtractLLMCommunicationException(Exception ex)
            => FailoverErrorClassifier.ExtractLLMCommunicationException(ex);

        private async Task TrackErrorAsync(LLMCommunicationException ex)
        {
            try
            {
                // Skip tracking for test keys (ID 0 or negative)
                if (_keyId <= 0)
                {
                    return;
                }

                var errorTracker = _serviceProvider.GetService<IProviderErrorTrackingService>();
                if (errorTracker == null)
                {
                    _logger?.LogDebug("Error tracking service not available");
                    return;
                }

                var errorType = ClassifyError(ex.StatusCode);
                
                // Only track errors that are meaningful for provider health
                if (errorType == ProviderErrorType.Unknown)
                {
                    return;
                }

                await errorTracker.TrackErrorAsync(new ProviderErrorInfo
                {
                    KeyCredentialId = _keyId,
                    ProviderId = _providerId,
                    ErrorType = errorType,
                    ErrorMessage = ex.Message,
                    HttpStatusCode = (int?)ex.StatusCode,
                    RetryAttempt = 0, // Direct error, not from retry
                    RequestId = null
                });

                _logger?.LogInformation(
                    "Tracked {ErrorType} error for key {KeyId}, provider {ProviderId}",
                    errorType, _keyId, _providerId);
            }
            catch (Exception trackingEx)
            {
                // Don't let error tracking break the main flow
                _logger?.LogError(trackingEx, "Failed to track provider error");
            }
        }

        private static ProviderErrorType ClassifyError(HttpStatusCode? statusCode)
        {
            return statusCode switch
            {
                HttpStatusCode.Unauthorized => ProviderErrorType.InvalidApiKey,
                HttpStatusCode.PaymentRequired => ProviderErrorType.InsufficientBalance,
                HttpStatusCode.Forbidden => ProviderErrorType.AccessForbidden,
                HttpStatusCode.TooManyRequests => ProviderErrorType.RateLimitExceeded,
                HttpStatusCode.NotFound => ProviderErrorType.ModelNotFound,
                HttpStatusCode.ServiceUnavailable => ProviderErrorType.ServiceUnavailable,
                HttpStatusCode.BadGateway => ProviderErrorType.ServiceUnavailable,
                HttpStatusCode.GatewayTimeout => ProviderErrorType.Timeout,
                HttpStatusCode.InternalServerError => ProviderErrorType.ServiceUnavailable,
                _ => ProviderErrorType.Unknown
            };
        }
    }
}
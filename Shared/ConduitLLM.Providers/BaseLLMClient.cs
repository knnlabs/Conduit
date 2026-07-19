using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Metrics;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.Authentication;
using ConduitLLM.Providers.Common.Models;
using ConduitLLM.Providers.Configuration;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Base class for LLM client implementations that provides common functionality
    /// and standardized handling of requests, responses, and errors.
    /// </summary>
    public abstract class BaseLLMClient : ILLMClient, IAuthenticationVerifiable
    {
        // NOTE: all HTTP time budgets (per-attempt, total, per operation class) live in the
        // resilience pipeline — see ProviderResilienceOptions and
        // docs/architecture/provider-http-resilience.md. HttpClient.Timeout is always infinite.

        protected readonly Provider Provider;
        protected readonly ProviderKeyCredential PrimaryKeyCredential;
        protected readonly string ProviderModelId;
        protected readonly ILogger Logger;
        protected readonly string ProviderName;
        protected readonly IHttpClientFactory? HttpClientFactory;
        protected readonly ProviderDefaultModels? DefaultModels;

        protected static readonly JsonSerializerOptions DefaultJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Gets the authentication strategy for this provider.
        /// Override in derived classes to use provider-specific authentication methods.
        /// </summary>
        /// <remarks>
        /// Default is Bearer token authentication. Override this property in derived classes
        /// for providers that use different authentication methods (e.g., Token, api-key header).
        /// </remarks>
        protected virtual IAuthenticationStrategy AuthenticationStrategy => BearerTokenStrategy.Instance;

        /// <summary>
        /// Gets the provider configuration from the registry.
        /// Returns null if no configuration is registered for this provider type.
        /// </summary>
        protected virtual ProviderConfiguration? ProviderConfig =>
            ProviderConfigurationRegistry.TryGetConfiguration(Provider.ProviderType, out var config)
                ? config : null;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseLLMClient"/> class.
        /// </summary>
        /// <param name="provider">The provider entity containing configuration.</param>
        /// <param name="primaryKeyCredential">The primary key credential to use for requests.</param>
        /// <param name="providerModelId">The provider's model identifier.</param>
        /// <param name="logger">The logger to use for logging.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory for creating HttpClient instances.</param>
        /// <param name="providerName">The name of this LLM provider.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        protected BaseLLMClient(
            Provider provider,
            ProviderKeyCredential primaryKeyCredential,
            string providerModelId,
            ILogger logger,
            IHttpClientFactory? httpClientFactory = null,
            string? providerName = null,
            ProviderDefaultModels? defaultModels = null)
        {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            PrimaryKeyCredential = primaryKeyCredential ?? throw new ArgumentNullException(nameof(primaryKeyCredential));
            ProviderModelId = providerModelId ?? throw new ArgumentNullException(nameof(providerModelId));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            HttpClientFactory = httpClientFactory;
            ProviderName = providerName ?? provider.ProviderName ?? GetType().Name.Replace("Client", string.Empty);
            DefaultModels = defaultModels;

            ValidateCredentials();
        }

        /// <summary>
        /// Validates that the required credentials are present.
        /// Override in derived classes to add provider-specific validation.
        /// </summary>
        protected virtual void ValidateCredentials()
        {
            if (string.IsNullOrWhiteSpace(PrimaryKeyCredential.ApiKey))
            {
                throw new ConfigurationException($"API key is missing for provider '{ProviderName}'");
            }
        }

        /// <summary>
        /// Creates a configured HttpClient for making requests to the provider API.
        /// </summary>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <returns>A configured HttpClient instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown when IHttpClientFactory is not available.</exception>
        protected virtual HttpClient CreateHttpClient(string? apiKey = null)
        {
            if (HttpClientFactory == null)
            {
                throw new InvalidOperationException(
                    $"IHttpClientFactory is required for {ProviderName} but was not injected. " +
                    "Ensure IHttpClientFactory is registered in the dependency injection container. " +
                    "Creating HttpClient instances directly can cause socket exhaustion under load.");
            }

            var client = HttpClientFactory.CreateClient(Http.ProviderHttpClientNames.Chat(Provider.ProviderType));

            string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : PrimaryKeyCredential.ApiKey!;
            if (string.IsNullOrWhiteSpace(effectiveApiKey))
            {
                throw new ConfigurationException($"API key is missing for provider '{ProviderName}'");
            }

            ConfigureHttpClient(client, effectiveApiKey);
            return client;
        }

        /// <summary>
        /// Configures the HttpClient with necessary headers and settings.
        /// Override in derived classes to add provider-specific configuration.
        /// </summary>
        /// <param name="client">The HttpClient to configure.</param>
        /// <param name="apiKey">The API key to use for authentication.</param>
        protected virtual void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");

            // Configure authentication
            ConfigureAuthentication(client, apiKey);

            // All timeout budgets live in the resilience pipeline (per-attempt + total, per
            // operation class). HttpClient.Timeout must stay infinite: it would otherwise also
            // cancel streaming response reads mid-stream (killing SSE streams that outlive it)
            // and truncate pipeline budgets longer than itself.
            client.Timeout = Timeout.InfiniteTimeSpan;
        }
        
        /// <summary>
        /// Configures authentication for the HttpClient.
        /// Uses the <see cref="AuthenticationStrategy"/> property to determine the authentication method.
        /// Override the <see cref="AuthenticationStrategy"/> property in derived classes to change the authentication method.
        /// </summary>
        /// <param name="client">The HttpClient to configure.</param>
        /// <param name="apiKey">The API key to use for authentication.</param>
        protected virtual void ConfigureAuthentication(HttpClient client, string apiKey)
        {
            AuthenticationStrategy.ApplyAuthentication(client, apiKey);
        }

        /// <summary>
        /// Creates an HttpClient specifically for authentication verification.
        /// This client should NOT have BaseAddress set when using absolute URLs.
        /// </summary>
        /// <param name="apiKey">The API key to use for authentication.</param>
        /// <returns>A configured HttpClient for authentication verification.</returns>
        /// <exception cref="InvalidOperationException">Thrown when IHttpClientFactory is not available.</exception>
        protected virtual HttpClient CreateAuthenticationVerificationClient(string apiKey)
        {
            if (HttpClientFactory == null)
            {
                throw new InvalidOperationException(
                    $"IHttpClientFactory is required for {ProviderName} authentication verification but was not injected. " +
                    "Ensure IHttpClientFactory is registered in the dependency injection container. " +
                    "Creating HttpClient instances directly can cause socket exhaustion under load.");
            }

            var client = HttpClientFactory.CreateClient(Http.ProviderHttpClientNames.Auth(Provider.ProviderType));

            // Configure basic headers
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");

            // Configure authentication
            ConfigureAuthentication(client, apiKey);

            // Timeout budgets live in the resilience pipeline (auth class: fail fast, no retries)
            client.Timeout = Timeout.InfiniteTimeSpan;

            // Do NOT set BaseAddress - we'll be using absolute URLs
            return client;
        }

        /// <summary>
        /// Creates a chat completion using the LLM provider.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A chat completion response.</returns>
        public abstract Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Streams a chat completion using the LLM provider.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An async enumerable of chat completion chunks.</returns>
        public abstract IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets available models from the LLM provider.
        /// </summary>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A list of available models.</returns>
        public abstract Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists available model IDs from the LLM provider.
        /// </summary>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A list of available model IDs.</returns>
        public virtual async Task<List<string>> ListModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Default implementation calls GetModelsAsync and extracts just the IDs
            var models = await GetModelsAsync(apiKey, cancellationToken);
            return models.Select(m => m.Id).ToList();
        }

        /// <summary>
        /// Creates embeddings using the LLM provider.
        /// </summary>
        /// <param name="request">The embedding request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An embedding response.</returns>
        public abstract Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates images using the LLM provider.
        /// </summary>
        /// <param name="request">The image generation request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An image generation response.</returns>
        public abstract Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the capabilities for this provider. Override in derived classes to provide 
        /// provider-specific capabilities.
        /// </summary>
        /// <param name="modelId">Optional specific model ID to get capabilities for.</param>
        /// <returns>The provider capabilities.</returns>
        public virtual Task<ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null)
        {
            // Return basic capabilities by default
            return Task.FromResult(new ProviderCapabilities
            {
                Provider = ProviderName,
                ModelId = modelId ?? ProviderModelId,
                ChatParameters = new ChatParameterSupport
                {
                    Temperature = true,
                    MaxTokens = true
                },
                Features = new FeatureSupport
                {
                    Streaming = true
                }
            });
        }

        /// <summary>
        /// Reads error content from an HTTP response.
        /// </summary>
        /// <param name="response">The HTTP response.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The error content as a string.</returns>
        protected async Task<string> ReadErrorContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            return await ConduitLLM.Core.Utilities.HttpClientHelper.ReadErrorContentAsync(response, cancellationToken);
        }

        /// <summary>
        /// Safely executes an API request with standardized error handling, tracing,
        /// metrics, and structured logging scope.
        /// </summary>
        /// <typeparam name="TResult">The type of result expected from the operation.</typeparam>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="operationName">The name of the operation for error messages.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The result of the operation.</returns>
        protected async Task<TResult> ExecuteApiRequestAsync<TResult>(
            Func<Task<TResult>> operation,
            string operationName,
            CancellationToken cancellationToken)
        {
            using var activity = ProviderInstrumentation.StartRequestActivity(
                operationName, ProviderName, ProviderTypeName, ProviderModelId);
            using var scope = BeginProviderLogScope(operationName);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await ExceptionHandler.HandleHttpRequestAsync(
                    async () =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return await operation();
                    },
                    Logger,
                    $"{ProviderName} ({operationName})");

                stopwatch.Stop();
                ProviderInstrumentation.RecordRequest(
                    operationName, ProviderName, ProviderTypeName, ProviderModelId,
                    stopwatch.Elapsed.TotalMilliseconds, success: true);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return result;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                ProviderInstrumentation.RecordRequest(
                    operationName, ProviderName, ProviderTypeName, ProviderModelId,
                    stopwatch.Elapsed.TotalMilliseconds, success: false,
                    errorType: nameof(OperationCanceledException));
                activity?.SetStatus(ActivityStatusCode.Error, "cancelled");
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                ProviderInstrumentation.RecordRequest(
                    operationName, ProviderName, ProviderTypeName, ProviderModelId,
                    stopwatch.Elapsed.TotalMilliseconds, success: false,
                    errorType: ex.GetType().Name);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Provider type name (e.g., "OpenAI", "Groq") used as a tag on spans and metrics.
        /// </summary>
        protected string ProviderTypeName => Provider.ProviderType.ToString();

        /// <summary>
        /// Opens an <see cref="ILogger"/> scope populated with provider, model, key, and
        /// operation context so that all downstream log entries inherit correlation tags.
        /// </summary>
        /// <param name="operationName">The current operation name (e.g., "ChatCompletion").</param>
        /// <returns>A disposable scope; may be null if the underlying logger does not support scopes.</returns>
        protected IDisposable? BeginProviderLogScope(string operationName)
        {
            return Logger.BeginScope(new Dictionary<string, object?>
            {
                ["ProviderName"] = ProviderName,
                ["ProviderType"] = ProviderTypeName,
                ["ProviderId"] = Provider.Id,
                ["KeyCredentialId"] = PrimaryKeyCredential.Id,
                ["Model"] = ProviderModelId,
                ["Operation"] = operationName
            });
        }

        /// <summary>
        /// Records token usage metrics + tags reported by the provider.
        /// Safe to call with a null <paramref name="usage"/>; counters are only incremented
        /// for token dimensions that are populated.
        /// </summary>
        /// <param name="usage">The usage object returned by the provider, or null.</param>
        /// <param name="operationName">The operation that produced the usage (e.g., "ChatCompletion").</param>
        protected void RecordUsage(Usage? usage, string operationName)
        {
            if (usage == null)
            {
                return;
            }

            ProviderInstrumentation.RecordUsage(
                operationName,
                ProviderName,
                ProviderTypeName,
                ProviderModelId,
                usage.PromptTokens,
                usage.CompletionTokens,
                usage.TotalTokens);

            if ((usage.PromptTokens ?? 0) > 0 ||
                (usage.CompletionTokens ?? 0) > 0 ||
                (usage.TotalTokens ?? 0) > 0)
            {
                Logger.LogDebug(
                    "{Provider} {Operation} usage: Prompt={Prompt}, Completion={Completion}, Total={Total}",
                    ProviderName, operationName,
                    usage.PromptTokens ?? 0,
                    usage.CompletionTokens ?? 0,
                    usage.TotalTokens ?? 0);
            }
        }

        /// <summary>
        /// Begins an instrumentation scope for a streaming provider request.
        /// Use inside an async iterator with try/finally; call <c>RecordChunk()</c> per chunk
        /// and <c>RecordFailure(...)</c> before re-throwing on error.
        /// </summary>
        /// <param name="operationName">The operation name (e.g., "StreamChatCompletion").</param>
        protected ProviderInstrumentation.StreamingScope BeginStreamingScope(string operationName)
        {
            return ProviderInstrumentation.BeginStreaming(
                operationName, ProviderName, ProviderTypeName, ProviderModelId);
        }

        /// <summary>
        /// Begins an instrumentation scope for a long-running async-job poll loop
        /// (e.g., Replicate predictions, MiniMax video generation).
        /// Pair with <c>using</c> and pass to <c>AsyncJobPoller.PollAsync</c>.
        /// </summary>
        /// <param name="operationName">The operation name (e.g., "CreateVideo", "CreateImage").</param>
        protected ProviderInstrumentation.PollingScope BeginPollingScope(string operationName)
        {
            return ProviderInstrumentation.BeginPolling(
                operationName, ProviderName, ProviderTypeName, ProviderModelId);
        }

        /// <summary>
        /// Prepares and validates a request before sending it to the API.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <param name="request">The request to validate.</param>
        /// <param name="operationName">The name of the operation for error messages.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        /// <exception cref="ValidationException">Thrown when the request fails validation.</exception>
        protected virtual void ValidateRequest<TRequest>(TRequest request, string operationName)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request),
                    $"Request cannot be null for {operationName} operation");
            }
        }

        /// <summary>
        /// Creates a dictionary of standard headers for API requests.
        /// Uses the <see cref="AuthenticationStrategy"/> property to determine the authentication header.
        /// </summary>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <returns>A dictionary of headers.</returns>
        protected virtual Dictionary<string, string> CreateStandardHeaders(string? apiKey = null)
        {
            string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : PrimaryKeyCredential.ApiKey!;

            var headers = new Dictionary<string, string>
            {
                ["User-Agent"] = "ConduitLLM"
            };

            // Add authentication using the strategy
            var authHeader = AuthenticationStrategy.CreateAuthenticationHeader(effectiveApiKey);
            if (authHeader != null)
            {
                headers["Authorization"] = $"{authHeader.Scheme} {authHeader.Parameter}";
            }
            else if (AuthenticationStrategy is ApiKeyHeaderStrategy apiKeyStrategy)
            {
                // For header-based auth strategies that don't use Authorization header
                headers[apiKeyStrategy.HeaderName] = effectiveApiKey;
            }

            return headers;
        }

        /// <summary>
        /// Verifies that the provider credentials are valid by making a test request.
        /// </summary>
        /// <param name="apiKey">Optional API key to test. If null, uses the configured key.</param>
        /// <param name="baseUrl">Optional base URL override. If null, uses the configured URL.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>An authentication result indicating success or failure.</returns>
        /// <remarks>
        /// This implementation makes an actual HTTP request to verify the API key works.
        /// It uses <see cref="GetHealthCheckUrl"/> to determine the endpoint.
        /// Derived classes can override this for provider-specific verification logic,
        /// or just override <see cref="GetHealthCheckUrl"/> if only the endpoint differs.
        /// </remarks>
        public virtual async Task<Core.Interfaces.AuthenticationResult> VerifyAuthenticationAsync(
            string? apiKey = null,
            string? baseUrl = null,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // Use provided API key or fall back to configured one
                var effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : PrimaryKeyCredential.ApiKey;

                // Basic validation
                if (string.IsNullOrWhiteSpace(effectiveApiKey))
                {
                    return Core.Interfaces.AuthenticationResult.Failure(
                        "API key is required",
                        $"No API key provided for {ProviderName} authentication");
                }

                // Create HTTP client and make verification request
                using var client = CreateAuthenticationVerificationClient(effectiveApiKey);
                var healthCheckUrl = GetHealthCheckUrl(baseUrl);

                Logger.LogDebug("Verifying {Provider} authentication with endpoint: {Endpoint}", ProviderName, healthCheckUrl);

                using var response = await client.GetAsync(healthCheckUrl, cancellationToken);
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                Logger.LogInformation("{Provider} auth check returned status {StatusCode}", ProviderName, response.StatusCode);

                if (response.IsSuccessStatusCode)
                {
                    return Core.Interfaces.AuthenticationResult.Success(
                        $"Connected successfully to {ProviderName}",
                        responseTime);
                }

                // Handle specific error cases
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    Logger.LogWarning("{Provider} authentication failed: {Response}", ProviderName, responseContent);
                    return Core.Interfaces.AuthenticationResult.Failure(
                        "Authentication failed",
                        $"Invalid API key for {ProviderName}");
                }

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    return Core.Interfaces.AuthenticationResult.Failure(
                        "Access forbidden",
                        $"API key does not have sufficient permissions for {ProviderName}");
                }

                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Unexpected response: {response.StatusCode}",
                    responseContent);
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex, "Network error verifying {Provider} authentication", ProviderName);
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Network error: {ex.Message}",
                    ex.ToString());
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                Logger.LogError(ex, "Timeout verifying {Provider} authentication", ProviderName);
                return Core.Interfaces.AuthenticationResult.Failure(
                    "Request timeout",
                    "Authentication request timed out");
            }
            catch (OperationCanceledException)
            {
                Logger.LogDebug("{Provider} authentication verification was cancelled", ProviderName);
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error verifying {Provider} authentication", ProviderName);
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }

        /// <summary>
        /// Gets the health check URL for this provider.
        /// </summary>
        /// <param name="baseUrl">Optional base URL override. If null, uses the configured URL.</param>
        /// <returns>The URL to use for health checks.</returns>
        /// <remarks>
        /// This default implementation uses the health check endpoint from the provider configuration registry,
        /// falling back to the /models endpoint which is commonly used by OpenAI-compatible APIs.
        /// Derived classes can override this method for provider-specific endpoints.
        /// </remarks>
        public virtual string GetHealthCheckUrl(string? baseUrl = null)
        {
            var effectiveBaseUrl = !string.IsNullOrWhiteSpace(baseUrl)
                ? baseUrl.TrimEnd('/')
                : (Provider.BaseUrl ?? GetDefaultBaseUrl()).TrimEnd('/');

            // Use the health check endpoint from configuration, or default to /models
            var healthCheckEndpoint = ProviderConfigurationRegistry.GetHealthCheckEndpoint(Provider.ProviderType);
            return $"{effectiveBaseUrl}{healthCheckEndpoint}";
        }

        /// <summary>
        /// Gets the default base URL for this provider.
        /// </summary>
        /// <returns>The default base URL.</returns>
        /// <remarks>
        /// Uses the default URL from the provider configuration registry.
        /// Override in derived classes to provide provider-specific default URLs
        /// if not defined in the registry.
        /// </remarks>
        protected virtual string GetDefaultBaseUrl()
        {
            return ProviderConfigurationRegistry.GetDefaultBaseUrl(Provider.ProviderType)
                ?? "https://api.example.com";
        }

        /// <summary>
        /// Classifies an HTTP error response into a provider error type.
        /// </summary>
        /// <param name="response">The HTTP response message.</param>
        /// <param name="responseBody">The response body content.</param>
        /// <returns>The classified error type.</returns>
        protected virtual ProviderErrorType ClassifyHttpError(
            HttpResponseMessage response, 
            string? responseBody)
        {
            // Base classification by status code
            var errorType = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => ProviderErrorType.InvalidApiKey,
                HttpStatusCode.PaymentRequired => ProviderErrorType.InsufficientBalance,
                HttpStatusCode.Forbidden => ProviderErrorType.AccessForbidden,
                HttpStatusCode.TooManyRequests => ProviderErrorType.RateLimitExceeded,
                HttpStatusCode.NotFound => ProviderErrorType.ModelNotFound,
                HttpStatusCode.ServiceUnavailable => ProviderErrorType.ServiceUnavailable,
                HttpStatusCode.BadGateway => ProviderErrorType.ServiceUnavailable,
                HttpStatusCode.GatewayTimeout => ProviderErrorType.Timeout,
                HttpStatusCode.RequestTimeout => ProviderErrorType.Timeout,
                _ => ProviderErrorType.Unknown
            };

            // Allow provider-specific refinement
            return RefineErrorClassification(errorType, responseBody);
        }

        /// <summary>
        /// Refines error classification based on provider-specific response patterns.
        /// Override in derived classes to handle provider-specific error messages.
        /// </summary>
        /// <param name="baseType">The base error type from status code.</param>
        /// <param name="responseBody">The response body for additional context.</param>
        /// <returns>The refined error type.</returns>
        protected virtual ProviderErrorType RefineErrorClassification(
            ProviderErrorType baseType,
            string? responseBody)
        {
            if (string.IsNullOrEmpty(responseBody))
                return baseType;

            var lowerBody = responseBody.ToLowerInvariant();

            // Common pattern: 403 with quota/billing keywords → InsufficientBalance
            if (baseType == ProviderErrorType.AccessForbidden &&
                (lowerBody.Contains("insufficient_quota") ||
                 lowerBody.Contains("exceeded your current quota") ||
                 lowerBody.Contains("billing") ||
                 lowerBody.Contains("payment") ||
                 lowerBody.Contains("credit")))
            {
                return ProviderErrorType.InsufficientBalance;
            }

            // Common pattern: authentication method mismatch (e.g., Bearer token
            // sent to a provider that requires a custom API key header)
            if (baseType == ProviderErrorType.InvalidApiKey &&
                (lowerBody.Contains("invalid bearer token") ||
                 lowerBody.Contains("invalid api key") ||
                 lowerBody.Contains("invalid x-api-key") ||
                 lowerBody.Contains("authentication_error")))
            {
                return ProviderErrorType.InvalidApiKey;
            }

            // Common pattern: rate limit keywords regardless of status code
            if (lowerBody.Contains("rate limit") ||
                lowerBody.Contains("too many requests"))
            {
                return ProviderErrorType.RateLimitExceeded;
            }

            // Common pattern: model not found keywords
            if (lowerBody.Contains("model") &&
                (lowerBody.Contains("not found") ||
                 lowerBody.Contains("does not exist") ||
                 lowerBody.Contains("invalid model")))
            {
                return ProviderErrorType.ModelNotFound;
            }

            return baseType;
        }

        /// <summary>
        /// Extracts a more helpful error message from exception details.
        /// Checks Response: patterns, embedded JSON, Data["Body"], and InnerException.
        /// </summary>
        /// <param name="ex">The exception to extract information from.</param>
        /// <returns>An enhanced error message.</returns>
        protected virtual string ExtractEnhancedErrorMessage(Exception ex)
        {
            // 1. Look for "Response:" pattern in the message
            var msg = ex.Message;
            var responseIdx = msg.IndexOf("Response:");
            if (responseIdx >= 0)
            {
                var extracted = msg.Substring(responseIdx + "Response:".Length).Trim();
                if (!string.IsNullOrEmpty(extracted))
                {
                    return extracted;
                }
            }

            // 2. Look for JSON content in the message
            var jsonStart = msg.IndexOf("{");
            var jsonEnd = msg.LastIndexOf("}");
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonPart = msg.Substring(jsonStart, jsonEnd - jsonStart + 1);
                try
                {
                    var json = JsonDocument.Parse(jsonPart);
                    if (json.RootElement.TryGetProperty("error", out var errorElement))
                    {
                        if (errorElement.TryGetProperty("message", out var messageElement))
                        {
                            return messageElement.GetString() ?? msg;
                        }
                    }
                }
                catch
                {
                    // If parsing fails, continue to the next method
                }
            }

            // 3. Look for Body data in the exception's Data dictionary
            if (ex.Data.Contains("Body") && ex.Data["Body"] is string body && !string.IsNullOrEmpty(body))
            {
                return body;
            }

            // 4. Try inner exception
            if (ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message))
            {
                return ex.InnerException.Message;
            }

            // 5. Fallback to original message
            return msg;
        }

        /// <summary>
        /// Extracts a user-friendly error message from a JSON string by checking common error paths:
        /// error.message, error (as string), and message.
        /// </summary>
        /// <param name="jsonContent">The JSON content to parse.</param>
        /// <param name="fallback">The fallback message if parsing fails.</param>
        /// <returns>The extracted error message or the fallback.</returns>
        protected static string ExtractErrorFromJson(string jsonContent, string fallback)
        {
            try
            {
                var json = JsonDocument.Parse(jsonContent);

                if (json.RootElement.TryGetProperty("error", out var error))
                {
                    if (error.TryGetProperty("message", out var message))
                        return message.GetString() ?? fallback;

                    if (error.ValueKind == JsonValueKind.String)
                        return error.GetString() ?? fallback;
                }

                if (json.RootElement.TryGetProperty("message", out var directMessage))
                    return directMessage.GetString() ?? fallback;
            }
            catch
            {
                // Not JSON or parsing failed
            }

            return fallback;
        }

        /// <summary>
        /// Extracts a user-friendly error message from an HTTP response.
        /// </summary>
        /// <param name="response">The HTTP response.</param>
        /// <param name="responseBody">The response body.</param>
        /// <returns>A user-friendly error message.</returns>
        protected virtual async Task<string> ExtractErrorMessageAsync(
            HttpResponseMessage response,
            string? responseBody = null)
        {
            if (string.IsNullOrEmpty(responseBody) && response.Content != null)
            {
                try
                {
                    responseBody = await response.Content.ReadAsStringAsync();
                }
                catch
                {
                    // Ignore read errors
                }
            }

            var fallback = $"{response.StatusCode}: {response.ReasonPhrase ?? "Unknown error"}";

            if (!string.IsNullOrEmpty(responseBody))
            {
                return ExtractErrorFromJson(responseBody, fallback);
            }

            return fallback;
        }

        /// <summary>
        /// Sends an HTTP request with provider key tracking for error attribution.
        /// </summary>
        /// <param name="request">The HTTP request to send.</param>
        /// <param name="keyCredential">The key credential being used.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The HTTP response.</returns>
        protected async Task<HttpResponseMessage> SendRequestWithKeyTracking(
            HttpRequestMessage request,
            ProviderKeyCredential keyCredential,
            CancellationToken cancellationToken = default)
        {
            // Attach key context to request for error tracking
            request.Options.Set(new HttpRequestOptionsKey<int>("KeyCredentialId"), keyCredential.Id);
            request.Options.Set(new HttpRequestOptionsKey<int>("ProviderId"), keyCredential.ProviderId);
            
            // Use the appropriate HttpClient
            var httpClient = CreateHttpClient(keyCredential.ApiKey ?? string.Empty);
            
            return await httpClient.SendAsync(request, cancellationToken);
        }
    }
}

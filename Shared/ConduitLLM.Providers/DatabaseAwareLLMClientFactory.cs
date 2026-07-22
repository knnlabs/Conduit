using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Decorators;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Providers.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Database-aware implementation of ILLMClientFactory that uses provider credentials from the database.
    /// </summary>
    /// <remarks>
    /// This factory creates LLM client instances using credentials dynamically loaded from the database.
    /// It supports all configured providers and applies decorators like performance tracking when enabled.
    /// 
    /// Use this factory when:
    /// - Credentials are stored in the database
    /// - Multiple providers of the same type are configured
    /// - Dynamic credential management is required
    /// </remarks>
    public class DatabaseAwareLLMClientFactory : ILLMClientFactory
    {
        private readonly IProviderService _credentialService;
        private readonly IModelProviderMappingService _mappingService;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DatabaseAwareLLMClientFactory> _logger;
        private readonly IPerformanceMetricsService? _performanceMetricsService;
        private readonly IModelCapabilityService? _capabilityService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDbContextFactory<ConduitDbContext>? _dbContextFactory;
        private readonly IDistributedCache? _distributedCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseAwareLLMClientFactory"/> class.
        /// </summary>
        public DatabaseAwareLLMClientFactory(
            IProviderService credentialService,
            IModelProviderMappingService mappingService,
            ILoggerFactory loggerFactory,
            IHttpClientFactory httpClientFactory,
            ILogger<DatabaseAwareLLMClientFactory> logger,
            IServiceProvider serviceProvider,
            IPerformanceMetricsService? performanceMetricsService = null,
            IModelCapabilityService? capabilityService = null,
            IDbContextFactory<ConduitDbContext>? dbContextFactory = null,
            IDistributedCache? distributedCache = null)
        {
            _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
            _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _performanceMetricsService = performanceMetricsService;
            _capabilityService = capabilityService;
            _dbContextFactory = dbContextFactory;
            _distributedCache = distributedCache;
        }

        public async Task<ILLMClient> GetClientForChatAsync(
            ConduitLLM.Core.Models.ChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            var mappings = await _mappingService.GetMappingsByModelAliasAsync(request.Model);
            if (mappings.Count == 0)
                throw new ModelNotFoundException(request.Model, $"Model '{request.Model}' not found. Please check your model configuration.");

            var settings = _serviceProvider.GetService<IGlobalSettingsCacheService>();
            var switchValue = settings is null ? null : await settings.GetSettingValueAsync("Routing.Chat.Enabled");
            var routingEnabled = !bool.TryParse(switchValue, out var enabled) || enabled;
            var policy = await GetRoutePolicyAsync(request.Model, cancellationToken);
            var scored = routingEnabled ? BalancedRouteScorer.Score(mappings, policy) :
                mappings.OrderBy(mapping => mapping.Id).Take(1).Select(mapping => new ScoredRoute(mapping, 0.5m)).ToArray();
            if (scored.Count == 0)
                throw new ServiceUnavailableException($"No healthy provider route for model '{request.Model}'.", "Routing");

            var ordered = scored.ToList();
            if (routingEnabled && policy.CacheAffinityEnabled && _distributedCache is not null &&
                !string.IsNullOrWhiteSpace(request.RoutingAffinityKey))
            {
                var cachedId = await _distributedCache.GetStringAsync(
                    RoutedChatClient.AffinityCacheKey(request.Model, request.RoutingAffinityKey), cancellationToken);
                if (int.TryParse(cachedId, out var affinityId))
                {
                    var affinity = ordered.FirstOrDefault(route => route.Mapping.Id == affinityId);
                    if (affinity is not null && ordered[0].Score - affinity.Score <= policy.MaxAffinityScorePenalty)
                    {
                        ordered.Remove(affinity); ordered.Insert(0, affinity);
                        request.RoutingAffinityUsed = true;
                        request.RoutingDecisionReason = "affinity_reuse";
                    }
                }
            }
            request.RoutingDecisionReason ??= routingEnabled ? "balanced_score" : "routing_disabled";

            var routes = new List<(ModelProviderMapping, ILLMClient)>();
            foreach (var route in ordered)
            {
                var provider = await _credentialService.GetProviderByIdAsync(route.Mapping.ProviderId);
                if (provider is null || !provider.IsEnabled) continue;
                var credential = await ValidateProviderAndGetCredentialAsync(provider);
                routes.Add((route.Mapping, CreateClientForProvider(provider, credential,
                    route.Mapping.ProviderModelId, route.Mapping.ProviderOptions)));
            }
            if (routes.Count == 0)
                throw new ServiceUnavailableException($"No configured provider credential for model '{request.Model}'.", "Routing");
            request.SelectedMappingId = routes[0].Item1.Id;
            return new RoutedChatClient(routes, request, _distributedCache, policy);
        }

        private async Task<ModelRoutePolicy> GetRoutePolicyAsync(string alias, CancellationToken cancellationToken)
        {
            if (_dbContextFactory is not null)
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var persisted = await context.ModelRoutePolicies.AsNoTracking()
                    .SingleOrDefaultAsync(policy => policy.ModelAlias == alias, cancellationToken);
                if (persisted is not null && persisted.IsEnabled) return persisted;
            }
            var result = new ModelRoutePolicy { ModelAlias = alias };
            var settings = _serviceProvider.GetService<IGlobalSettingsCacheService>();
            var json = settings is null ? null : await settings.GetSettingValueAsync("Routing.Defaults");
            if (string.IsNullOrWhiteSpace(json)) return result;

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.TryGetProperty("costWeight", out var cost)) result.CostWeight = cost.GetDecimal();
                if (root.TryGetProperty("speedWeight", out var speed)) result.SpeedWeight = speed.GetDecimal();
                if (root.TryGetProperty("qualityWeight", out var quality)) result.QualityWeight = quality.GetDecimal();
                if (root.TryGetProperty("cacheAffinityEnabled", out var affinity)) result.CacheAffinityEnabled = affinity.GetBoolean();
                if (root.TryGetProperty("affinityTtlSeconds", out var ttl)) result.AffinityTtlSeconds = ttl.GetInt32();
                if (root.TryGetProperty("maxAffinityScorePenalty", out var penalty)) result.MaxAffinityScorePenalty = penalty.GetDecimal();
            }
            catch (JsonException exception)
            {
                _logger.LogError(exception, "Invalid Routing.Defaults configuration; using built-in defaults");
            }
            return result;
        }

        /// <inheritdoc />
        public async Task<ILLMClient> GetClientAsync(string modelName, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("DatabaseAwareLLMClientFactory.GetClientAsync called for model: {ModelName}", modelName);

            // Get model mapping from database
            var mapping = await _mappingService.GetMappingByModelAliasAsync(modelName);

            if (mapping == null)
            {
                _logger.LogWarning("No model mapping found in database for alias: {ModelAlias}", modelName);
                throw new ModelNotFoundException(modelName, $"Model '{modelName}' not found. Please check your model configuration.");
            }

            _logger.LogDebug("Found mapping in database: {ModelAlias} -> ProviderId:{ProviderId}/{ProviderModelId}",
                mapping.ModelAlias, mapping.ProviderId, mapping.ProviderModelId);

            // Get the provider from database
            var provider = await _credentialService.GetProviderByIdAsync(mapping.ProviderId);

            if (provider == null)
            {
                _logger.LogWarning("Provider {ProviderId} not found", mapping.ProviderId);
                throw new ServiceUnavailableException($"Provider for model '{modelName}' is not available.", "Provider");
            }

            var primaryKey = await ValidateProviderAndGetCredentialAsync(provider);
            return CreateClientForProvider(provider, primaryKey, mapping.ProviderModelId, mapping.ProviderOptions);
        }

        /// <inheritdoc />
        public Task<ILLMClient> GetClientByProviderIdAsync(int providerId, CancellationToken cancellationToken = default)
            => GetClientByProviderIdAsync(providerId, "default-model-id", cancellationToken);

        /// <inheritdoc />
        public async Task<ILLMClient> GetClientByProviderIdAsync(int providerId, string providerModelId, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Getting client for provider ID {ProviderId} and model {ProviderModelId} using database credentials",
                providerId, providerModelId);

            var provider = await _credentialService.GetProviderByIdAsync(providerId);

            if (provider == null)
            {
                _logger.LogWarning("No provider found for provider ID {ProviderId} in database", providerId);
                throw new InvalidRequestException($"Provider with ID '{providerId}' not found.", "provider_not_found", "providerId");
            }

            var primaryKey = await ValidateProviderAndGetCredentialAsync(provider);
            return CreateClientForProvider(provider, primaryKey, providerModelId);
        }

        /// <inheritdoc />
        public IProviderMetadata? GetProviderMetadata(ProviderType providerType)
        {
            // This factory doesn't have access to provider metadata
            // Return null to indicate metadata is not available through this factory
            return null;
        }

        /// <inheritdoc />
        public async Task<ILLMClient> GetClientByProviderTypeAsync(ProviderType providerType, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Getting client for provider type {ProviderType} using database credentials", providerType);

            var allProviders = await _credentialService.GetAllProvidersAsync();
            var provider = allProviders.FirstOrDefault(p => p.ProviderType == providerType);

            if (provider == null)
            {
                _logger.LogWarning("No provider found for provider type {ProviderType} in database", providerType);
                throw new InvalidRequestException($"No provider configured for type '{providerType}'.", "provider_type_not_found", "providerType");
            }

            var primaryKey = await ValidateProviderAndGetCredentialAsync(provider);
            return CreateClientForProvider(provider, primaryKey, "default-model-id");
        }

        /// <inheritdoc />
        public ILLMClient CreateTestClient(Provider provider, ProviderKeyCredential keyCredential)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            if (keyCredential == null)
            {
                throw new ArgumentNullException(nameof(keyCredential));
            }

            if (string.IsNullOrWhiteSpace(keyCredential.ApiKey))
            {
                throw new ArgumentException("API key is required for testing credentials", nameof(keyCredential));
            }

            _logger.LogDebug("Creating test client for provider type: {ProviderType}", provider.ProviderType);

            // Use a minimal model ID for testing - providers should accept this for auth verification
            const string testModelId = "test-model";

            return CreateClientForProvider(provider, keyCredential, testModelId);
        }

        /// <summary>
        /// Validates that a provider is enabled, then retrieves its primary key credential.
        /// </summary>
        private async Task<ProviderKeyCredential> ValidateProviderAndGetCredentialAsync(Provider provider)
        {
            if (!provider.IsEnabled)
            {
                _logger.LogWarning("Provider {ProviderId} is disabled", provider.Id);
                throw new ServiceUnavailableException(
                    $"Provider '{provider.ProviderName}' is currently disabled.", provider.ProviderName);
            }

            return await GetPrimaryKeyCredentialAsync(provider);
        }

        private async Task<ProviderKeyCredential> GetPrimaryKeyCredentialAsync(Provider provider)
        {
            var keyCredentials = await _credentialService.GetKeyCredentialsByProviderIdAsync(provider.Id);

            var primaryKey = keyCredentials.FirstOrDefault(k => k.IsPrimary && k.IsEnabled)
                ?? keyCredentials.FirstOrDefault(k => k.IsEnabled);

            if (primaryKey == null)
            {
                _logger.LogWarning("No enabled API key found for provider {ProviderId}", provider.Id);
                throw new ConfigurationException($"No API key configured for provider '{provider.ProviderName}'.");
            }

            return primaryKey;
        }

        private ILLMClient CreateClientForProvider(Provider provider, ProviderKeyCredential keyCredential, string modelId, string? providerOptionsJson = null)
        {
            var providerName = provider.ProviderType.ToString().ToLowerInvariant();

            _logger.LogDebug("Creating client for provider type: {ProviderType}, model: {ModelId}",
                provider.ProviderType, modelId);

            // Create the client creation context with all dependencies
            var context = new ClientCreationContext
            {
                LoggerFactory = _loggerFactory,
                HttpClientFactory = _httpClientFactory,
                CapabilityService = _capabilityService,
                ProviderOptionsJson = providerOptionsJson
            };

            // Create the base client using the registry
            ILLMClient client;
            try
            {
                client = ClientCreatorRegistry.CreateClient(
                    provider.ProviderType,
                    provider,
                    keyCredential,
                    modelId,
                    context);
            }
            catch (ArgumentException ex)
            {
                throw new ConfigurationException($"Unsupported provider type: {provider.ProviderType}", ex);
            }

            // Apply prompt caching decorator (before context/perf so it modifies request early)
            var settingsService = _serviceProvider.GetService<IGlobalSettingsCacheService>();
            if (settingsService != null)
            {
                var cachingLogger = _loggerFactory.CreateLogger<PromptCachingLLMClient>();
                client = new PromptCachingLLMClient(
                    client, settingsService, cachingLogger, provider.ProviderType.ToString(), modelId);
            }

            // Apply context decorator to set provider key context for error tracking
            _logger.LogDebug("Applying context decorator for KeyId: {KeyId}, ProviderId: {ProviderId}",
                keyCredential.Id, provider.Id);
            client = new ContextAwareLLMClient(client, keyCredential.Id, provider.Id, _serviceProvider);

            // Apply decorators if configured
            if (_performanceMetricsService != null)
            {
                _logger.LogDebug("Applying performance tracking decorator to client");
                var perfLogger = _loggerFactory.CreateLogger<PerformanceTrackingLLMClient>();
                client = new PerformanceTrackingLLMClient(client, _performanceMetricsService, perfLogger, providerName, true);
            }

            return client;
        }
    }
}

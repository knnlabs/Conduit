using System.Diagnostics;
using System.Text.Json;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Configuration.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Hosted service that warms the discovery cache on application startup
    /// </summary>
    public class DiscoveryCacheWarmingService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IDiscoveryCacheService _discoveryCacheService;
        private readonly DiscoveryCacheOptions _options;
        private readonly ILogger<DiscoveryCacheWarmingService> _logger;

        public DiscoveryCacheWarmingService(
            IServiceProvider serviceProvider,
            IDiscoveryCacheService discoveryCacheService,
            IOptions<DiscoveryCacheOptions> options,
            ILogger<DiscoveryCacheWarmingService> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _discoveryCacheService = discoveryCacheService ?? throw new ArgumentNullException(nameof(discoveryCacheService));
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.WarmCacheOnStartup || !_options.EnableCaching)
            {
                _logger.LogInformation("Discovery cache warming is disabled");
                return;
            }

            // Wait for the application to fully start using configurable delay
            var startupDelay = TimeSpan.FromSeconds(_options.WarmupStartupDelaySeconds);
            _logger.LogDebug("Waiting {Seconds} seconds before starting cache warming", _options.WarmupStartupDelaySeconds);
            await Task.Delay(startupDelay, stoppingToken);

            if (!_options.UseDistributedLockForWarming)
            {
                await WarmCachesAsync(stoppingToken);
                return;
            }

            using var lockScope = _serviceProvider.CreateScope();
            var lockService = lockScope.ServiceProvider.GetService<IDistributedLockService>();
            _logger.LogDebug("Attempting to acquire distributed lock for cache warming");

            var result = await lockService.RunWithOptionalLockAsync(
                "discovery:cache:warming",
                TimeSpan.FromMinutes(5),
                TimeSpan.FromSeconds(_options.DistributedLockTimeoutSeconds),
                TimeSpan.FromSeconds(1),
                async lockAcquired =>
                {
                    if (lockAcquired)
                    {
                        _logger.LogDebug("Acquired distributed lock for cache warming");
                    }

                    await WarmCachesAsync(stoppingToken);
                    return true;
                },
                _logger,
                stoppingToken,
                skipOnTimeout: true);

            if (!result.Executed)
            {
                _logger.LogInformation("Another instance is performing cache warming, skipping");
            }
        }

        private async Task WarmCachesAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Starting discovery cache warming");
                var stopwatch = Stopwatch.StartNew();

                using var scope = _serviceProvider.CreateScope();
                var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ConduitDbContext>>();

                // Warm cache for common capability filters
                var commonCapabilities = _options.WarmupCapabilities ?? new List<string> 
                { 
                    "chat", "image_input", "video_input", "audio_input", "file_input",
                    "image_generation", "video_generation"
                };

                // First, warm the cache with all models (no filter)
                await WarmCacheForCapability(dbContextFactory, null, stoppingToken);

                // Then warm cache for each common capability
                foreach (var capability in commonCapabilities)
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    await WarmCacheForCapability(dbContextFactory, capability, stoppingToken);
                    
                    // Small delay between cache warming operations
                    await Task.Delay(100, stoppingToken);
                }

                stopwatch.Stop();
                _logger.LogInformation(
                    "Discovery cache warming completed in {ElapsedMs}ms. Warmed {Count} cache entries",
                    stopwatch.ElapsedMilliseconds,
                    commonCapabilities.Count + 1); // +1 for the "all" entry
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Cache warming cancelled due to application shutdown");
            }
            catch (Exception ex)
            {
                // Log error but don't throw - we don't want cache warming failures to prevent startup
                _logger.LogError(ex, "Error during discovery cache warming - application will continue without warmed cache");
            }
        }

        private async Task WarmCacheForCapability(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            string? capability,
            CancellationToken cancellationToken)
        {
            try
            {
                using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                // Get all enabled model mappings with their related data
                var modelMappings = await context.ModelProviderMappings
                    .Include(m => m.Provider)
                    .Include(m => m.ModelProviderTypeAssociation)
                        .ThenInclude(mpta => mpta.Model)
                            .ThenInclude(m => m.Series)
                    .Where(m => m.IsEnabled && m.Provider != null && m.Provider.IsEnabled)
                    .ToListAsync(cancellationToken);

                var models = new List<JsonElement>();

                foreach (var mapping in modelMappings)
                {
                    // Skip if model is missing
                    if (mapping.ModelProviderTypeAssociation?.Model == null)
                    {
                        continue;
                    }

                    var model = mapping.ModelProviderTypeAssociation.Model;
                    var caps = ModelCapabilityResolver.Resolve(model, mapping.ModelProviderTypeAssociation);

                    // Apply capability filter if specified
                    if (!string.IsNullOrEmpty(capability))
                    {
                        var capabilityKey = capability.Replace("-", "_").ToLowerInvariant();
                        bool hasCapability = capabilityKey switch
                        {
                            "chat" => caps.SupportsChat,
                            "streaming" or "chat_stream" => caps.SupportsStreaming,
                            "vision" => caps.SupportsVision,
                            "image_input" => caps.SupportsImageInput,
                            "video_input" => caps.SupportsVideoInput,
                            "audio_input" => caps.SupportsAudioInput,
                            "file_input" => caps.SupportsFileInput,
                            "pdf_input" => caps.SupportsFileInput ||
                                           mapping.Provider?.ProviderType == ProviderType.OpenRouter,
                            "video_understanding" => caps.SupportsVideoUnderstanding,
                            "video_generation" => caps.SupportsVideoGeneration,
                            "image_generation" => caps.SupportsImageGeneration,
                            "embeddings" => caps.SupportsEmbeddings,
                            "function_calling" => caps.SupportsFunctionCalling,
                            _ => false
                        };

                        if (!hasCapability)
                        {
                            continue;
                        }
                    }

                    // Use overrides from association first, then fall back to model defaults
                    var maxInputTokens = mapping.ModelProviderTypeAssociation.MaxInputTokens ?? model.MaxInputTokens ?? 0;
                    var maxOutputTokens = mapping.ModelProviderTypeAssociation.MaxOutputTokens ?? model.MaxOutputTokens ?? 0;

                    // Serialize to JsonElement for cache-safe storage (anonymous objects can't round-trip through JSON deserialization)
                    models.Add(JsonSerializer.SerializeToElement(new
                    {
                        // Identity
                        id = mapping.ModelAlias,
                        provider = mapping.Provider?.ProviderType.ToString().ToLowerInvariant(),
                        display_name = mapping.ModelAlias,

                        // Metadata
                        description = mapping.ModelProviderTypeAssociation?.Model?.Description ?? string.Empty,
                        model_card_url = mapping.ModelProviderTypeAssociation?.Model?.ModelCardUrl ?? string.Empty,
                        max_tokens = maxInputTokens + maxOutputTokens, // Total context window size
                        max_input_tokens = maxInputTokens,
                        max_output_tokens = maxOutputTokens,
                        tokenizer_type = model.TokenizerType.ToString().ToLowerInvariant(),
                        input_modalities = caps.InputModalities,
                        output_modalities = caps.OutputModalities,
                        capability_source = caps.CapabilitySource.ToString().ToLowerInvariant(),
                        capabilities_last_verified_at = caps.CapabilitiesLastVerifiedAt,

                        // UI Parameters from Model or Series
                        parameters = mapping.ModelProviderTypeAssociation?.Model?.ModelParameters ?? mapping.ModelProviderTypeAssociation?.Model?.Series?.Parameters ?? "{}",

                        // Capabilities (nested object as expected by SDK)
                        capabilities = new
                        {
                            chat = caps.SupportsChat,
                            chat_stream = caps.SupportsStreaming,
                            embeddings = caps.SupportsEmbeddings,
                            image_generation = caps.SupportsImageGeneration,
                            vision = caps.SupportsVision,
                            video_generation = caps.SupportsVideoGeneration,
                            image_input = caps.SupportsImageInput,
                            video_input = caps.SupportsVideoInput,
                            audio_input = caps.SupportsAudioInput,
                            file_input = caps.SupportsFileInput,
                            pdf_input = caps.SupportsFileInput ||
                                        mapping.Provider?.ProviderType == ProviderType.OpenRouter,
                            video_understanding = caps.SupportsVideoUnderstanding,
                            function_calling = caps.SupportsFunctionCalling,
                            tool_use = caps.SupportsFunctionCalling, // Same as function calling for now
                            json_mode = (bool?)null, // Not tracked — null, not a confident false
                            max_tokens = maxInputTokens + maxOutputTokens,
                            max_output_tokens = maxOutputTokens
                        }
                    }));
                }

                // Cache the results
                var cacheKey = DiscoveryCacheService.BuildCacheKey(capability);
                var discoveryResult = new DiscoveryModelsResult
                {
                    Data = models,
                    Count = models.Count,
                    CapabilityFilter = capability
                };

                await _discoveryCacheService.SetDiscoveryResultsAsync(cacheKey, discoveryResult, cancellationToken);
                
                _logger.LogInformation(
                    "Warmed discovery cache for capability '{Capability}' with {Count} models",
                    capability ?? "all",
                    models.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error warming cache for capability: {Capability}", capability ?? "all");
            }
        }
    }
}

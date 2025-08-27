using System.Diagnostics;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Http.Services
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
            _logger.LogInformation("Waiting {Seconds} seconds before starting cache warming", _options.WarmupStartupDelaySeconds);
            await Task.Delay(startupDelay, stoppingToken);

            // Try to acquire distributed lock if enabled
            IDistributedLock? distributedLock = null;
            if (_options.UseDistributedLockForWarming)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var lockService = scope.ServiceProvider.GetService<IDistributedLockService>();
                    
                    if (lockService != null)
                    {
                        _logger.LogInformation("Attempting to acquire distributed lock for cache warming");
                        
                        var lockTimeout = TimeSpan.FromSeconds(_options.DistributedLockTimeoutSeconds);
                        distributedLock = await lockService.AcquireLockWithRetryAsync(
                            "discovery:cache:warming",
                            TimeSpan.FromMinutes(5), // Lock expiry time
                            lockTimeout,
                            TimeSpan.FromSeconds(1), // Retry delay
                            stoppingToken);
                        
                        if (distributedLock != null)
                        {
                            _logger.LogInformation("Acquired distributed lock for cache warming");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Distributed lock service not available, proceeding without coordination");
                    }
                }
                catch (TimeoutException)
                {
                    _logger.LogInformation("Another instance is performing cache warming, skipping");
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to acquire distributed lock, proceeding without coordination");
                }
            }

            try
            {
                _logger.LogInformation("Starting discovery cache warming");
                var stopwatch = Stopwatch.StartNew();

                using var scope = _serviceProvider.CreateScope();
                var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ConduitDbContext>>();

                // Warm cache for common capability filters
                var commonCapabilities = _options.WarmupCapabilities ?? new List<string> 
                { 
                    "chat", "vision", "image_generation", "video_generation" 
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
            finally
            {
                // Release the distributed lock if we acquired one
                if (distributedLock != null)
                {
                    try
                    {
                        await distributedLock.ReleaseAsync();
                        _logger.LogDebug("Released distributed lock for cache warming");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error releasing distributed lock");
                    }
                }
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
                    .Include(m => m.Model)
                        .ThenInclude(m => m.Series)
                    .Include(m => m.Model)
                        .ThenInclude(m => m.Capabilities)
                    .Where(m => m.IsEnabled && m.Provider != null && m.Provider.IsEnabled)
                    .ToListAsync(cancellationToken);

                var models = new List<object>();

                foreach (var mapping in modelMappings)
                {
                    // Skip if model or capabilities are missing
                    if (mapping.Model?.Capabilities == null)
                    {
                        continue;
                    }

                    var caps = mapping.Model.Capabilities;

                    // Apply capability filter if specified
                    if (!string.IsNullOrEmpty(capability))
                    {
                        var capabilityKey = capability.Replace("-", "_").ToLowerInvariant();
                        bool hasCapability = capabilityKey switch
                        {
                            "chat" => caps.SupportsChat,
                            "streaming" or "chat_stream" => caps.SupportsStreaming,
                            "vision" => caps.SupportsVision,
                            "audio_transcription" => caps.SupportsAudioTranscription,
                            "text_to_speech" => caps.SupportsTextToSpeech,
                            "realtime_audio" => caps.SupportsRealtimeAudio,
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

                    models.Add(new
                    {
                        // Identity
                        id = mapping.ModelAlias,
                        provider = mapping.Provider?.ProviderType.ToString().ToLowerInvariant(),
                        display_name = mapping.ModelAlias,
                        
                        // Metadata
                        description = mapping.Model?.Description ?? string.Empty,
                        model_card_url = mapping.Model?.ModelCardUrl ?? string.Empty,
                        max_tokens = caps.MaxTokens,
                        tokenizer_type = caps.TokenizerType.ToString().ToLowerInvariant(),
                        
                        // UI Parameters from Model or Series
                        parameters = mapping.Model?.ModelParameters ?? mapping.Model?.Series?.Parameters ?? "{}",
                        
                        // Capabilities (flat boolean flags)
                        supports_chat = caps.SupportsChat,
                        supports_streaming = caps.SupportsStreaming,
                        supports_vision = caps.SupportsVision,
                        supports_function_calling = caps.SupportsFunctionCalling,
                        supports_audio_transcription = caps.SupportsAudioTranscription,
                        supports_text_to_speech = caps.SupportsTextToSpeech,
                        supports_realtime_audio = caps.SupportsRealtimeAudio,
                        supports_video_generation = caps.SupportsVideoGeneration,
                        supports_image_generation = caps.SupportsImageGeneration,
                        supports_embeddings = caps.SupportsEmbeddings
                    });
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
using System.Collections.Concurrent;
using ConduitLLM.Core.Interfaces;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Provides self-healing and automatic failover capabilities for image generation.
    /// </summary>
    public partial class ImageGenerationResilienceService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ImageGenerationResilienceService> _logger;
        private readonly IImageGenerationMetricsCollector _metricsCollector;
        private readonly IImageGenerationAlertingService _alertingService;
        private readonly ImageGenerationResilienceOptions _options;
        private readonly IPublishEndpoint? _publishEndpoint;
        
        private readonly ConcurrentDictionary<int, ProviderHealthState> _providerStates = new();
        private readonly ConcurrentDictionary<int, FailoverState> _failoverStates = new();
        private readonly ConcurrentDictionary<int, RecoveryAttempt> _recoveryAttempts = new();
        
        // Cache for providers
        private readonly ConcurrentDictionary<int, ConduitLLM.Configuration.Entities.Provider> _providerCache = new();
        
        private Timer? _healthCheckTimer;
        private Timer? _recoveryTimer;

        public ImageGenerationResilienceService(
            IServiceProvider serviceProvider,
            ILogger<ImageGenerationResilienceService> logger,
            IImageGenerationMetricsCollector metricsCollector,
            IImageGenerationAlertingService alertingService,
            IOptions<ImageGenerationResilienceOptions> options,
            IPublishEndpoint? publishEndpoint = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
            _alertingService = alertingService ?? throw new ArgumentNullException(nameof(alertingService));
            _options = options?.Value ?? new ImageGenerationResilienceOptions();
            _publishEndpoint = publishEndpoint;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("Image generation resilience service is disabled");
                return Task.CompletedTask;
            }
            
            _logger.LogInformation(
                "Image generation resilience service started - Health check: {HealthInterval}min, Recovery: {RecoveryInterval}min",
                _options.HealthCheckIntervalMinutes, _options.RecoveryCheckIntervalMinutes);
            
            // Initialize provider states
            _ = RefreshProviderCacheAsync(stoppingToken);
            
            // Start health monitoring timer
            _healthCheckTimer = new Timer(
                async _ => await PerformHealthChecksAsync(stoppingToken),
                null,
                TimeSpan.FromSeconds(30), // Initial delay
                TimeSpan.FromMinutes(_options.HealthCheckIntervalMinutes));
            
            // Start recovery timer
            _recoveryTimer = new Timer(
                async _ => await PerformRecoveryChecksAsync(stoppingToken),
                null,
                TimeSpan.FromMinutes(1), // Initial delay
                TimeSpan.FromMinutes(_options.RecoveryCheckIntervalMinutes));
            
            return Task.CompletedTask;
        }

        private async Task PerformHealthChecksAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;
            
            try
            {
                // Get current metrics
                var metrics = await _metricsCollector.GetMetricsSnapshotAsync(cancellationToken);
                
                // Check each provider
                foreach (var (providerName, status) in metrics.ProviderStatuses)
                {
                    // Try to find provider ID from name
                    var providerId = GetProviderIdFromName(providerName);
                    if (providerId.HasValue)
                    {
                        await CheckProviderHealthAsync(providerId.Value, status, metrics);
                    }
                    else
                    {
                        _logger.LogWarning("Could not find provider ID for provider name: {ProviderName}", providerName);
                    }
                }
                
                // Check for global issues
                await CheckGlobalHealthAsync(metrics);
                
                // Trigger alert evaluation
                await _alertingService.EvaluateMetricsAsync(metrics, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing resilience health checks");
            }
        }

        private async Task CheckProviderHealthAsync(
            int providerId,
            ProviderStatus status,
            ImageGenerationMetricsSnapshot metrics)
        {
            var state = _providerStates.GetOrAdd(providerId, new ProviderHealthState
            {
                ProviderId = providerId
            });
            
            // Update health state
            state.IsHealthy = status.IsHealthy;
            state.HealthScore = status.HealthScore;
            state.ConsecutiveFailures = status.ConsecutiveFailures;
            state.LastChecked = DateTime.UtcNow;
            
            // Check if provider needs intervention
            if (!status.IsHealthy || status.ConsecutiveFailures >= _options.FailureThreshold)
            {
                await HandleUnhealthyProviderAsync(providerId, state, status);
            }
            else if (state.IsQuarantined && status.HealthScore > _options.RecoveryHealthScoreThreshold)
            {
                // Provider appears to be recovering
                await AttemptProviderRecoveryAsync(providerId, state);
            }
            
            // Check for performance degradation
            if (status.AverageResponseTimeMs > _options.SlowResponseThresholdMs)
            {
                await HandleSlowProviderAsync(providerId, status);
            }
        }



        private int? GetProviderIdFromName(string providerName)
        {
            // Try to find provider by name in cache
            var provider = _providerCache.Values.FirstOrDefault(p => 
                p.ProviderName == providerName || p.ProviderType.ToString() == providerName);
            return provider?.Id;
        }
        
        private string GetProviderName(int providerId)
        {
            if (_providerCache.TryGetValue(providerId, out var provider))
            {
                return provider.ProviderName ?? provider.ProviderType.ToString();
            }
            return $"Provider_{providerId}";
        }
        

        private async Task RefreshProviderCacheAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var providerService = scope.ServiceProvider.GetService<IProviderService>();
            
            if (providerService == null)
            {
                _logger.LogWarning("IProviderService not available, cannot refresh provider cache");
                return;
            }
            
            try
            {
                var providers = await providerService.GetAllProvidersAsync();
                
                // Update the provider cache
                _providerCache.Clear();
                foreach (var provider in providers)
                {
                    _providerCache[provider.Id] = provider;
                    
                    // Initialize health state for enabled providers
                    if (provider.IsEnabled && !_providerStates.ContainsKey(provider.Id))
                    {
                        _providerStates[provider.Id] = new ProviderHealthState
                        {
                            ProviderId = provider.Id,
                            IsHealthy = true,
                            HealthScore = 1.0
                        };
                    }
                }
                
                _logger.LogInformation("Refreshed provider cache. Found {Count} providers", _providerCache.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh provider cache");
            }
        }
        
        private bool IsPrimaryProvider(int providerId)
        {
            // Determine if this is a primary provider that requires immediate failover
            // In the new model, this should be based on provider configuration, not hardcoded
            // Determine if this is a primary provider that requires immediate failover
            if (_providerCache.TryGetValue(providerId, out var provider))
            {
                // TODO: Add IsPrimary flag to Provider entity
                // For now, check the provider name
                var name = provider.ProviderName ?? string.Empty;
                return name.Contains("Primary", StringComparison.OrdinalIgnoreCase) || 
                       name.Contains("Production", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Image generation resilience service is stopping");
            
            _healthCheckTimer?.Change(Timeout.Infinite, 0);
            _healthCheckTimer?.Dispose();
            
            _recoveryTimer?.Change(Timeout.Infinite, 0);
            _recoveryTimer?.Dispose();
            
            await base.StopAsync(cancellationToken);
        }
    }
}
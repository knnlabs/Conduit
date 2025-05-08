using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.WebUI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Background service that monitors the health of LLM providers
    /// </summary>
    public class ProviderHealthMonitorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ProviderHealthMonitorService> _logger;
        private readonly IConfiguration _configuration;
        private readonly ProviderHealthOptions _options;
        private readonly Dictionary<string, DateTime> _lastChecks = new();
        private readonly Dictionary<string, int> _consecutiveFailures = new();
        private readonly Stopwatch _stopwatch = new();
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderHealthMonitorService"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider for creating scoped services</param>
        /// <param name="logger">The logger</param>
        /// <param name="configuration">The configuration</param>
        /// <param name="options">The provider health options</param>
        public ProviderHealthMonitorService(
            IServiceProvider serviceProvider,
            ILogger<ProviderHealthMonitorService> logger,
            IConfiguration configuration,
            IOptions<ProviderHealthOptions> options)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }
        
        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Provider Health Monitor Service is starting.");
            
            // Delay for 30 seconds on startup to allow other services to initialize
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            
            try
            {
                // Load existing provider health configurations
                await LoadProviderConfigurationsAsync(stoppingToken);
                
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        // Check if global monitoring is enabled
                        if (!_options.Enabled)
                        {
                            _logger.LogDebug("Provider health monitoring is globally disabled.");
                            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                            continue;
                        }
                        
                        await CheckProvidersAsync(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error occurred while checking provider health.");
                    }
                    
                    // Sleep for 1 minute between check cycles
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
            catch (TaskCanceledException)
            {
                // Normal shutdown
                _logger.LogInformation("Provider Health Monitor Service shutting down.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred in Provider Health Monitor Service.");
            }
        }
        
        /// <summary>
        /// Loads existing provider health configurations from the database
        /// </summary>
        private async Task LoadProviderConfigurationsAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IProviderHealthRepository>();
                
                var configurations = await repository.GetAllConfigurationsAsync();
                
                // Initialize the last checks dictionary with existing configurations
                foreach (var config in configurations)
                {
                    if (config.LastCheckedUtc.HasValue)
                    {
                        _lastChecks[config.ProviderName] = config.LastCheckedUtc.Value;
                    }
                }
                
                _logger.LogInformation("Loaded {Count} provider configurations.", configurations.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading provider configurations.");
            }
        }
        
        /// <summary>
        /// Checks the health of all providers that are due for checking
        /// </summary>
        private async Task CheckProvidersAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IProviderHealthRepository>();
                var statusService = scope.ServiceProvider.GetRequiredService<ProviderStatusService>();
                var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
                var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext>>();
                
                // Get all provider credentials
                using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                var providers = await dbContext.ProviderCredentials.ToListAsync(cancellationToken);
                
                _logger.LogDebug("Found {Count} providers to check.", providers.Count);
                
                foreach (var provider in providers)
                {
                    try
                    {
                        // Get or create the health configuration for this provider
                        var config = await repository.EnsureConfigurationExistsAsync(provider.ProviderName);
                        
                        // Skip if monitoring is disabled for this provider
                        if (!config.MonitoringEnabled)
                        {
                            _logger.LogDebug("Skipping health check for provider {ProviderName} (monitoring disabled).", provider.ProviderName);
                            continue;
                        }
                        
                        // Check if it's time to check this provider
                        var now = DateTime.UtcNow;
                        if (_lastChecks.TryGetValue(provider.ProviderName, out var lastCheck))
                        {
                            var timeSinceLastCheck = now - lastCheck;
                            if (timeSinceLastCheck.TotalMinutes < config.CheckIntervalMinutes)
                            {
                                _logger.LogDebug("Skipping health check for provider {ProviderName} (checked {Minutes} minutes ago, interval is {IntervalMinutes}).",
                                    provider.ProviderName, timeSinceLastCheck.TotalMinutes, config.CheckIntervalMinutes);
                                continue;
                            }
                        }
                        
                        // Update the last check time
                        _lastChecks[provider.ProviderName] = now;
                        await repository.UpdateLastCheckedTimeAsync(provider.ProviderName);
                        
                        // Check the provider status
                        _logger.LogInformation("Checking health for provider {ProviderName}...", provider.ProviderName);
                        
                        // Create a record before the check to measure response time accurately
                        var record = new ProviderHealthRecord
                        {
                            ProviderName = provider.ProviderName,
                            TimestampUtc = now,
                            EndpointUrl = GetEndpointUrlForProvider(provider)
                        };
                        
                        // Measure response time
                        _stopwatch.Restart();
                        
                        // Custom timeout for the provider
                        var timeoutToken = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds)).Token;
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutToken, cancellationToken);
                        
                        var status = await CheckProviderWithTimeoutAsync(statusService, provider, linkedCts.Token);
                        
                        _stopwatch.Stop();
                        
                        // Update the record with the results
                        record.IsOnline = status.IsOnline; // This property maintains compatibility
                        record.StatusMessage = status.StatusMessage;
                        record.ResponseTimeMs = _stopwatch.Elapsed.TotalMilliseconds;
                        
                        if (status.Status == ProviderStatus.StatusType.Offline)
                        {
                            // Categorize the error if the provider is offline
                            (record.ErrorCategory, record.ErrorDetails) = CategorizeError(status.StatusMessage);
                            
                            // Increment consecutive failure count
                            if (!_consecutiveFailures.TryGetValue(provider.ProviderName, out int failures))
                            {
                                failures = 0;
                            }
                            _consecutiveFailures[provider.ProviderName] = failures + 1;
                            
                            // Send notification if threshold is reached
                            if (config.NotificationsEnabled && failures + 1 >= config.ConsecutiveFailuresThreshold)
                            {
                                notificationService.AddNotification(
                                    WebUI.Models.NotificationType.Error,
                                    $"Provider {provider.ProviderName} is down: {record.ErrorCategory} - {status.StatusMessage}",
                                    "/home"
                                );
                                
                                _logger.LogWarning("Provider {ProviderName} is down: {ErrorCategory} - {StatusMessage}",
                                    provider.ProviderName, record.ErrorCategory, status.StatusMessage);
                            }
                        }
                        else if (status.Status == ProviderStatus.StatusType.Online)
                        {
                            // Check if the provider was previously down and now it's recovered
                            if (_consecutiveFailures.TryGetValue(provider.ProviderName, out int failures) && failures >= config.ConsecutiveFailuresThreshold)
                            {
                                // Send recovery notification
                                notificationService.AddNotification(
                                    WebUI.Models.NotificationType.Success,
                                    $"Provider {provider.ProviderName} is back online after {failures} consecutive failures.",
                                    "/home"
                                );
                                
                                _logger.LogInformation("Provider {ProviderName} is back online after {Failures} consecutive failures.",
                                    provider.ProviderName, failures);
                            }
                            
                            // Reset consecutive failures
                            _consecutiveFailures[provider.ProviderName] = 0;
                        }
                        else
                        {
                            // Provider status is unknown
                            record.ErrorCategory = null;
                            record.ErrorDetails = null;
                            
                            // Log at debug level
                            _logger.LogDebug("Provider {ProviderName} status is unknown: {StatusMessage}",
                                provider.ProviderName, status.StatusMessage);
                            
                            // Don't change consecutive failures counter
                        }
                        
                        // Save the record
                        await repository.SaveStatusAsync(record);
                        
                        _logger.LogDebug("Health check for provider {ProviderName} completed in {ElapsedMs}ms, IsOnline: {IsOnline}",
                            provider.ProviderName, _stopwatch.ElapsedMilliseconds, status.IsOnline);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error checking health for provider {ProviderName}", provider.ProviderName);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // Ignore cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking providers");
            }
        }
        
        /// <summary>
        /// Gets the endpoint URL for a provider
        /// </summary>
        private string GetEndpointUrlForProvider(ProviderCredential provider)
        {
            // Check if we have a custom endpoint configured
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IProviderHealthRepository>();
            var config = repository.GetConfigurationAsync(provider.ProviderName).GetAwaiter().GetResult();
            
            if (config?.CustomEndpointUrl != null)
            {
                return config.CustomEndpointUrl;
            }
            
            // Otherwise, get the default from ProviderStatusService internal method
            // We can't directly access the private method, so we infer this from the provider object
            return $"{(string.IsNullOrEmpty(provider.BaseUrl) ? "(Default)" : provider.BaseUrl)}/...";
        }
        
        /// <summary>
        /// Checks a provider with a timeout
        /// </summary>
        private static async Task<ProviderStatus> CheckProviderWithTimeoutAsync(
            ProviderStatusService statusService,
            ProviderCredential provider,
            CancellationToken cancellationToken)
        {
            try
            {
                var status = await statusService.CheckProviderStatusAsync(provider);
                return status;
            }
            catch (TaskCanceledException)
            {
                return new ProviderStatus
                {
                    Status = ProviderStatus.StatusType.Offline,
                    StatusMessage = "Connection timeout",
                    LastCheckedUtc = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new ProviderStatus
                {
                    Status = ProviderStatus.StatusType.Offline,
                    StatusMessage = $"Error: {ex.Message}",
                    LastCheckedUtc = DateTime.UtcNow
                };
            }
        }
        
        /// <summary>
        /// Categorizes an error message into a standard error category and detailed message
        /// </summary>
        private (string category, string details) CategorizeError(string statusMessage)
        {
            // Default category
            string category = "Unknown";
            string details = statusMessage;
            
            if (string.IsNullOrEmpty(statusMessage))
            {
                return (category, details);
            }
            
            // Categorize by common patterns
            if (statusMessage.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                statusMessage.Contains("timed out", StringComparison.OrdinalIgnoreCase))
            {
                category = "Timeout";
            }
            else if (statusMessage.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                     statusMessage.Contains("dns", StringComparison.OrdinalIgnoreCase) ||
                     statusMessage.Contains("connect", StringComparison.OrdinalIgnoreCase))
            {
                category = "Network";
            }
            else if (statusMessage.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
                     statusMessage.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
                     statusMessage.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                     statusMessage.Contains("authoriz", StringComparison.OrdinalIgnoreCase) ||
                     statusMessage.Contains("api key", StringComparison.OrdinalIgnoreCase) ||
                     statusMessage.Contains("401", StringComparison.OrdinalIgnoreCase))
            {
                category = "Authentication";
            }
            else if (statusMessage.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                     statusMessage.Contains("too many", StringComparison.OrdinalIgnoreCase) ||
                     statusMessage.Contains("429", StringComparison.OrdinalIgnoreCase))
            {
                category = "RateLimit";
            }
            else if (statusMessage.Contains("server", StringComparison.OrdinalIgnoreCase) ||
                     statusMessage.Contains("5", StringComparison.OrdinalIgnoreCase) ||
                     statusMessage.Contains("503", StringComparison.OrdinalIgnoreCase) ||
                     statusMessage.Contains("502", StringComparison.OrdinalIgnoreCase) ||
                     statusMessage.Contains("500", StringComparison.OrdinalIgnoreCase))
            {
                category = "ServerError";
            }
            else if (statusMessage.Contains("configuration", StringComparison.OrdinalIgnoreCase) ||
                     statusMessage.Contains("config", StringComparison.OrdinalIgnoreCase) ||
                     statusMessage.Contains("setting", StringComparison.OrdinalIgnoreCase))
            {
                category = "Configuration";
            }
            
            return (category, details);
        }
        
        /// <summary>
        /// Called when the service is starting
        /// </summary>
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Provider Health Monitor Service is starting.");
            return base.StartAsync(cancellationToken);
        }
        
        /// <summary>
        /// Called when the service is stopping
        /// </summary>
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Provider Health Monitor Service is stopping.");
            return base.StopAsync(cancellationToken);
        }
    }
}
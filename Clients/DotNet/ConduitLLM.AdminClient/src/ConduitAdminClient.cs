using ConduitLLM.AdminClient.Client;
using ConduitLLM.AdminClient.Services;
using ConduitLLM.AdminClient.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.AdminClient;

/// <summary>
/// Main client for accessing the Conduit Admin API.
/// Provides comprehensive management capabilities for virtual keys, providers, analytics, and system configuration.
/// </summary>
public class ConduitAdminClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ConduitAdminClientConfiguration _configuration;
    private readonly ILogger<ConduitAdminClient>? _logger;
    private readonly IMemoryCache? _cache;
    private bool _disposed;

    /// <summary>
    /// Gets the virtual keys service for managing API keys and budget controls.
    /// </summary>
    public VirtualKeyService VirtualKeys { get; }

    /// <summary>
    /// Gets the providers service for managing provider credentials and health monitoring.
    /// </summary>
    public ProviderService Providers { get; }

    /// <summary>
    /// Gets the analytics service for cost analysis, usage metrics, and reporting.
    /// </summary>
    public AnalyticsService Analytics { get; }

    /// <summary>
    /// Gets the discovery service for model discovery and capability testing.
    /// </summary>
    public DiscoveryService Discovery { get; }

    /// <summary>
    /// Gets the model mapping service for managing model provider mappings.
    /// </summary>
    public ModelMappingService ModelMappings { get; }

    /// <summary>
    /// Gets the settings service for managing global settings and configurations.
    /// </summary>
    public SettingsService Settings { get; }

    /// <summary>
    /// Gets the IP filter service for managing IP access controls.
    /// </summary>
    public IpFilterService IpFilters { get; }

    /// <summary>
    /// Gets the model cost service for managing model pricing configurations.
    /// </summary>
    public ModelCostService ModelCosts { get; }

    /// <summary>
    /// Gets the system service for accessing system information and health status.
    /// </summary>
    public SystemService System { get; }

    /// <summary>
    /// Gets the provider models service for model discovery and capability testing.
    /// </summary>
    public ProviderModelsService ProviderModels { get; }

    /// <summary>
    /// Gets the audio configuration service for managing audio providers, costs, and usage analytics.
    /// </summary>
    public AudioConfigurationService AudioConfiguration { get; }

    /// <summary>
    /// Initializes a new instance of the ConduitAdminClient class.
    /// </summary>
    /// <param name="configuration">The client configuration.</param>
    /// <param name="httpClient">Optional HTTP client instance. If not provided, a new one will be created.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="cache">Optional memory cache instance for performance optimization.</param>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
    /// <exception cref="ValidationException">Thrown when configuration is invalid.</exception>
    public ConduitAdminClient(
        ConduitAdminClientConfiguration configuration,
        HttpClient? httpClient = null,
        ILogger<ConduitAdminClient>? logger = null,
        IMemoryCache? cache = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
        _cache = cache;

        // Validate configuration
        ValidateConfiguration(configuration);

        // Create HTTP client if not provided
        _httpClient = httpClient ?? new HttpClient();

        // Create logger factory for services
        var loggerFactory = logger != null ? CreateLoggerFactory(logger) : null;

        // Initialize services
        VirtualKeys = new VirtualKeyService(
            _httpClient, 
            _configuration, 
            loggerFactory?.CreateLogger<VirtualKeyService>(), 
            _cache);

        Providers = new ProviderService(
            _httpClient, 
            _configuration, 
            loggerFactory?.CreateLogger<ProviderService>(), 
            _cache);

        Analytics = new AnalyticsService(
            _httpClient, 
            _configuration, 
            loggerFactory?.CreateLogger<AnalyticsService>(), 
            _cache);

        Discovery = new DiscoveryService(
            _httpClient, 
            _configuration, 
            loggerFactory?.CreateLogger<DiscoveryService>(), 
            _cache);

        ModelMappings = new ModelMappingService(
            _httpClient, 
            _configuration, 
            loggerFactory?.CreateLogger<ModelMappingService>(), 
            _cache);

        Settings = new SettingsService(
            _httpClient, 
            _configuration, 
            loggerFactory?.CreateLogger<SettingsService>(), 
            _cache);

        IpFilters = new IpFilterService(
            _httpClient, 
            _configuration, 
            loggerFactory?.CreateLogger<IpFilterService>(), 
            _cache);

        ModelCosts = new ModelCostService(
            _httpClient, 
            _configuration, 
            loggerFactory?.CreateLogger<ModelCostService>(), 
            _cache);

        System = new SystemService(
            _httpClient, 
            _configuration, 
            loggerFactory?.CreateLogger<SystemService>(), 
            _cache);

        ProviderModels = new ProviderModelsService(
            _httpClient, 
            _configuration, 
            loggerFactory?.CreateLogger<ProviderModelsService>(), 
            _cache);

        AudioConfiguration = new AudioConfigurationService(
            _httpClient, 
            _configuration, 
            loggerFactory?.CreateLogger<AudioConfigurationService>(), 
            _cache);
    }

    /// <summary>
    /// Creates a new ConduitAdminClient instance from environment variables.
    /// </summary>
    /// <param name="httpClient">Optional HTTP client instance.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="cache">Optional memory cache instance.</param>
    /// <returns>A new ConduitAdminClient instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required environment variables are missing.</exception>
    public static ConduitAdminClient FromEnvironment(
        HttpClient? httpClient = null,
        ILogger<ConduitAdminClient>? logger = null,
        IMemoryCache? cache = null)
    {
        var configuration = ConduitAdminClientConfiguration.FromEnvironment();
        return new ConduitAdminClient(configuration, httpClient, logger, cache);
    }

    /// <summary>
    /// Creates a new ConduitAdminClient instance with the specified master key and admin API URL.
    /// </summary>
    /// <param name="masterKey">The master key for authentication.</param>
    /// <param name="adminApiUrl">The admin API base URL.</param>
    /// <param name="httpClient">Optional HTTP client instance.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="cache">Optional memory cache instance.</param>
    /// <returns>A new ConduitAdminClient instance.</returns>
    /// <exception cref="ArgumentException">Thrown when masterKey or adminApiUrl is null or empty.</exception>
    public static ConduitAdminClient Create(
        string masterKey,
        string adminApiUrl,
        HttpClient? httpClient = null,
        ILogger<ConduitAdminClient>? logger = null,
        IMemoryCache? cache = null)
    {
        if (string.IsNullOrWhiteSpace(masterKey))
            throw new ArgumentException("Master key cannot be null or empty", nameof(masterKey));

        if (string.IsNullOrWhiteSpace(adminApiUrl))
            throw new ArgumentException("Admin API URL cannot be null or empty", nameof(adminApiUrl));

        var configuration = new ConduitAdminClientConfiguration
        {
            MasterKey = masterKey,
            AdminApiUrl = adminApiUrl
        };

        return new ConduitAdminClient(configuration, httpClient, logger, cache);
    }

    /// <summary>
    /// Tests the connection to the Conduit Admin API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the connection is successful, false otherwise.</returns>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to get virtual keys as a simple connectivity test
            var virtualKeys = await VirtualKeys.ListAsync(new Models.VirtualKeyFilters { PageSize = 1 }, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Connection test failed");
            return false;
        }
    }

    /// <summary>
    /// Gets a health summary of the admin system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health summary information.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<object> GetSystemHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Get health information from various services
            var providerHealthTask = Providers.GetHealthSummaryAsync(cancellationToken);
            var discoveryStatsTask = Discovery.GetDiscoveryStatsAsync(cancellationToken);

            await Task.WhenAll(providerHealthTask, discoveryStatsTask);

            return new
            {
                Timestamp = DateTime.UtcNow,
                ProviderHealth = await providerHealthTask,
                DiscoveryStats = await discoveryStatsTask
            };
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            _logger?.LogError(ex, "Failed to get system health");
            throw new ConduitAdminException("Failed to retrieve system health information", null, null, null, null, ex);
        }
    }

    /// <summary>
    /// Gets information about the client configuration (with sensitive data redacted).
    /// </summary>
    /// <returns>A readonly copy of the client configuration with sensitive data redacted.</returns>
    public ConduitAdminClientConfiguration GetConfiguration()
    {
        // Return a copy with sensitive data redacted
        return new ConduitAdminClientConfiguration
        {
            MasterKey = "***REDACTED***", // Don't expose the actual master key
            AdminApiUrl = _configuration.AdminApiUrl,
            CoreApiUrl = _configuration.CoreApiUrl,
            TimeoutSeconds = _configuration.TimeoutSeconds,
            MaxRetries = _configuration.MaxRetries,
            RetryDelayMs = _configuration.RetryDelayMs,
            DefaultHeaders = new Dictionary<string, string>(_configuration.DefaultHeaders),
            EnableCaching = _configuration.EnableCaching,
            CacheTimeoutSeconds = _configuration.CacheTimeoutSeconds
        };
    }

    /// <summary>
    /// Performs bulk operations across multiple services for efficiency.
    /// </summary>
    /// <param name="operations">A collection of operations to perform.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Results of all operations.</returns>
    public async Task<Dictionary<string, object>> PerformBulkOperationsAsync(
        IEnumerable<BulkOperation> operations,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, object>();
        var tasks = new List<Task>();

        foreach (var operation in operations)
        {
            tasks.Add(ExecuteOperationAsync(operation, results, cancellationToken));
        }

        await Task.WhenAll(tasks);
        return results;
    }

    /// <summary>
    /// Disposes of the client and releases all resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of the client resources.
    /// </summary>
    /// <param name="disposing">Whether to dispose managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            VirtualKeys?.Dispose();
            Providers?.Dispose();
            Analytics?.Dispose();
            Discovery?.Dispose();
            ModelMappings?.Dispose();
            Settings?.Dispose();
            IpFilters?.Dispose();
            ModelCosts?.Dispose();
            System?.Dispose();
            ProviderModels?.Dispose();
            AudioConfiguration?.Dispose();
            _httpClient?.Dispose();
            _disposed = true;
        }
    }

    private static void ValidateConfiguration(ConduitAdminClientConfiguration configuration)
    {
        var context = new ValidationContext(configuration);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(configuration, context, results, true))
        {
            var errors = string.Join(", ", results.Select(r => r.ErrorMessage));
            throw new ValidationException($"Invalid configuration: {errors}");
        }
    }

    private static ILoggerFactory CreateLoggerFactory(ILogger logger)
    {
        var factory = new LoggerFactory();
        factory.AddProvider(new SimpleLoggerProvider(logger));
        return factory;
    }

    private async Task ExecuteOperationAsync(
        BulkOperation operation,
        Dictionary<string, object> results,
        CancellationToken cancellationToken)
    {
        try
        {
            object result = operation.Type switch
            {
                BulkOperationType.GetVirtualKeys => await VirtualKeys.ListAsync(
                    operation.Parameters as Models.VirtualKeyFilters, cancellationToken),
                BulkOperationType.GetProviderCredentials => await Providers.ListCredentialsAsync(
                    operation.Parameters as Models.ProviderFilters, cancellationToken),
                BulkOperationType.GetDiscoveryStats => await Discovery.GetDiscoveryStatsAsync(cancellationToken),
                _ => throw new NotSupportedException($"Bulk operation type {operation.Type} is not supported")
            };

            lock (results)
            {
                results[operation.Id] = result;
            }
        }
        catch (Exception ex)
        {
            lock (results)
            {
                results[operation.Id] = new { Error = ex.Message, Type = ex.GetType().Name };
            }
        }
    }

    /// <summary>
    /// Simple logger provider for service injection.
    /// </summary>
    private class SimpleLoggerProvider : ILoggerProvider
    {
        private readonly ILogger _logger;

        public SimpleLoggerProvider(ILogger logger)
        {
            _logger = logger;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _logger;
        }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }
}

/// <summary>
/// Represents a bulk operation to be performed by the admin client.
/// </summary>
public class BulkOperation
{
    /// <summary>
    /// Gets or sets the unique identifier for the operation.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of operation to perform.
    /// </summary>
    public BulkOperationType Type { get; set; }

    /// <summary>
    /// Gets or sets the parameters for the operation.
    /// </summary>
    public object? Parameters { get; set; }
}

/// <summary>
/// Represents the types of bulk operations supported.
/// </summary>
public enum BulkOperationType
{
    /// <summary>
    /// Get virtual keys operation.
    /// </summary>
    GetVirtualKeys,

    /// <summary>
    /// Get provider credentials operation.
    /// </summary>
    GetProviderCredentials,

    /// <summary>
    /// Get discovery statistics operation.
    /// </summary>
    GetDiscoveryStats
}

/// <summary>
/// Extension methods for ConduitAdminClient.
/// </summary>
public static class ConduitAdminClientExtensions
{
    /// <summary>
    /// Creates a virtual key with default settings.
    /// </summary>
    /// <param name="client">The admin client.</param>
    /// <param name="keyName">The name for the virtual key.</param>
    /// <param name="maxBudget">Optional maximum budget.</param>
    /// <param name="allowedModels">Optional comma-separated list of allowed models.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created virtual key information.</returns>
    public static async Task<ConduitLLM.AdminClient.Models.CreateVirtualKeyResponse> CreateVirtualKeyAsync(
        this ConduitAdminClient client,
        string keyName,
        decimal? maxBudget = null,
        string? allowedModels = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ConduitLLM.AdminClient.Models.CreateVirtualKeyRequest
        {
            KeyName = keyName,
            MaxBudget = maxBudget,
            AllowedModels = allowedModels
        };

        return await client.VirtualKeys.CreateAsync(request, cancellationToken);
    }

    /// <summary>
    /// Gets cost summary for the last N days.
    /// </summary>
    /// <param name="client">The admin client.</param>
    /// <param name="days">Number of days to look back (default: 7).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cost summary for the specified period.</returns>
    public static async Task<ConduitLLM.AdminClient.Models.CostSummaryDto> GetRecentCostSummaryAsync(
        this ConduitAdminClient client,
        int days = 7,
        CancellationToken cancellationToken = default)
    {
        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddDays(-Math.Abs(days));

        return await client.Analytics.GetCostSummaryAsync(startDate, endDate, cancellationToken);
    }

    /// <summary>
    /// Tests if a provider connection is working.
    /// </summary>
    /// <param name="client">The admin client.</param>
    /// <param name="providerName">The provider name to test.</param>
    /// <param name="apiKey">The API key to test with.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The connection test result.</returns>
    public static async Task<ConduitLLM.AdminClient.Models.ProviderConnectionTestResultDto> TestProviderConnectionAsync(
        this ConduitAdminClient client,
        string providerName,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var request = new ConduitLLM.AdminClient.Models.ProviderConnectionTestRequest
        {
            ProviderName = providerName,
            ApiKey = apiKey
        };

        return await client.Providers.TestConnectionAsync(request, cancellationToken);
    }
}
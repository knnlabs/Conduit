using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;
using Microsoft.EntityFrameworkCore;
using ConduitLLM.Configuration.Repositories;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for tracking Admin API specific operational metrics.
    /// Monitors virtual key operations, provider management, and configuration changes.
    /// </summary>
    public class AdminOperationsMetricsService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AdminOperationsMetricsService> _logger;
        private readonly TimeSpan _collectionInterval = TimeSpan.FromMinutes(1);

        // Virtual Key operation metrics
        private static readonly Counter VirtualKeyOperations = Prometheus.Metrics
            .CreateCounter("conduit_admin_virtualkey_operations_total", "Total virtual key operations",
                new CounterConfiguration
                {
                    LabelNames = new[] { "operation", "status" } // operation: create, update, delete, rotate
                });

        private static readonly Histogram VirtualKeyOperationDuration = Prometheus.Metrics
            .CreateHistogram("conduit_admin_virtualkey_operation_duration_seconds", "Virtual key operation duration",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "operation" },
                    Buckets = Histogram.ExponentialBuckets(0.001, 2, 14) // 1ms to ~16s
                });

        private static readonly Gauge TotalVirtualKeys = Prometheus.Metrics
            .CreateGauge("conduit_admin_virtualkeys_total", "Total number of virtual keys",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "status" } // status: active, disabled, expired
                });

        // Provider management metrics
        private static readonly Counter ProviderOperations = Prometheus.Metrics
            .CreateCounter("conduit_admin_provider_operations_total", "Total provider operations",
                new CounterConfiguration
                {
                    LabelNames = new[] { "operation", "provider", "status" } // operation: create, update, delete, test
                });

        private static readonly Histogram ProviderTestDuration = Prometheus.Metrics
            .CreateHistogram("conduit_admin_provider_test_duration_seconds", "Provider connection test duration",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "provider" },
                    Buckets = Histogram.ExponentialBuckets(0.1, 2, 12) // 100ms to ~410s
                });

        private static readonly Gauge ConfiguredProviders = Prometheus.Metrics
            .CreateGauge("conduit_admin_providers_configured", "Number of configured providers",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "provider", "enabled" }
                });

        // Model mapping metrics
        private static readonly Counter ModelMappingOperations = Prometheus.Metrics
            .CreateCounter("conduit_admin_modelmapping_operations_total", "Total model mapping operations",
                new CounterConfiguration
                {
                    LabelNames = new[] { "operation", "status" } // operation: create, update, delete
                });

        private static readonly Gauge ActiveModelMappings = Prometheus.Metrics
            .CreateGauge("conduit_admin_modelmappings_active", "Number of active model mappings",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "provider" }
                });

        // Configuration change metrics
        private static readonly Counter ConfigurationChanges = Prometheus.Metrics
            .CreateCounter("conduit_admin_configuration_changes_total", "Total configuration changes",
                new CounterConfiguration
                {
                    LabelNames = new[] { "entity_type", "change_type" } // entity_type: virtualkey, provider, mapping
                });

        // Admin API usage metrics
        private static readonly Counter AdminApiAuthentications = Prometheus.Metrics
            .CreateCounter("conduit_admin_authentications_total", "Total authentication attempts",
                new CounterConfiguration
                {
                    LabelNames = new[] { "status" } // status: success, failed
                });

        private static readonly Gauge ActiveAdminSessions = Prometheus.Metrics
            .CreateGauge("conduit_admin_sessions_active", "Number of active admin sessions");

        // CSV import/export metrics
        private static readonly Counter CsvOperations = Prometheus.Metrics
            .CreateCounter("conduit_admin_csv_operations_total", "Total CSV operations",
                new CounterConfiguration
                {
                    LabelNames = new[] { "operation", "entity_type", "status" } // operation: import, export
                });

        private static readonly Histogram CsvOperationDuration = Prometheus.Metrics
            .CreateHistogram("conduit_admin_csv_operation_duration_seconds", "CSV operation duration",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "operation", "entity_type" },
                    Buckets = Histogram.ExponentialBuckets(0.1, 2, 12) // 100ms to ~410s
                });

        private static readonly Counter CsvRecordsProcessed = Prometheus.Metrics
            .CreateCounter("conduit_admin_csv_records_processed_total", "Total CSV records processed",
                new CounterConfiguration
                {
                    LabelNames = new[] { "operation", "entity_type" }
                });

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminOperationsMetricsService"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider for resolving scoped services.</param>
        /// <param name="logger">The logger instance.</param>
        public AdminOperationsMetricsService(
            IServiceProvider serviceProvider,
            ILogger<AdminOperationsMetricsService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Executes the background service to periodically collect metrics.
        /// </summary>
        /// <param name="stoppingToken">The cancellation token to stop the service.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Admin operations metrics service starting...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CollectMetricsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error collecting admin operations metrics");
                }

                await Task.Delay(_collectionInterval, stoppingToken);
            }

            _logger.LogInformation("Admin operations metrics service stopped");
        }

        private async Task CollectMetricsAsync()
        {
            using var scope = _serviceProvider.CreateScope();

            var tasks = new[]
            {
                CollectVirtualKeyMetrics(scope),
                CollectProviderMetrics(scope),
                CollectModelMappingMetrics(scope)
            };

            await Task.WhenAll(tasks);
        }

        private async Task CollectVirtualKeyMetrics(IServiceScope scope)
        {
            try
            {
                var virtualKeyRepo = scope.ServiceProvider.GetRequiredService<IVirtualKeyRepository>();
                var allKeys = await virtualKeyRepo.GetAllAsync();

                var now = DateTime.UtcNow;
                var activeCount = 0;
                var disabledCount = 0;
                var expiredCount = 0;

                foreach (var key in allKeys)
                {
                    if (!key.IsEnabled)
                    {
                        disabledCount++;
                    }
                    else if (key.ExpiresAt.HasValue && key.ExpiresAt.Value < now)
                    {
                        expiredCount++;
                    }
                    else
                    {
                        activeCount++;
                    }
                }

                TotalVirtualKeys.WithLabels("active").Set(activeCount);
                TotalVirtualKeys.WithLabels("disabled").Set(disabledCount);
                TotalVirtualKeys.WithLabels("expired").Set(expiredCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting virtual key metrics");
            }
        }

        private async Task CollectProviderMetrics(IServiceScope scope)
        {
            try
            {
                var providerCredentialService = scope.ServiceProvider.GetRequiredService<ConduitLLM.Configuration.IProviderCredentialService>();
                var providers = await providerCredentialService.GetAllCredentialsAsync();

                // Reset gauges to handle removed providers
                ConfiguredProviders.WithLabels("openai", "true").Set(0);
                ConfiguredProviders.WithLabels("openai", "false").Set(0);
                ConfiguredProviders.WithLabels("anthropic", "true").Set(0);
                ConfiguredProviders.WithLabels("anthropic", "false").Set(0);
                ConfiguredProviders.WithLabels("google", "true").Set(0);
                ConfiguredProviders.WithLabels("google", "false").Set(0);
                ConfiguredProviders.WithLabels("minimax", "true").Set(0);
                ConfiguredProviders.WithLabels("minimax", "false").Set(0);
                ConfiguredProviders.WithLabels("replicate", "true").Set(0);
                ConfiguredProviders.WithLabels("replicate", "false").Set(0);

                foreach (var provider in providers.GroupBy(p => p.ProviderType))
                {
                    var enabledCount = provider.Count(p => p.IsEnabled);
                    var disabledCount = provider.Count(p => !p.IsEnabled);

                    if (enabledCount > 0)
                        ConfiguredProviders.WithLabels(provider.Key.ToString().ToLower(), "true").Set(enabledCount);
                    if (disabledCount > 0)
                        ConfiguredProviders.WithLabels(provider.Key.ToString().ToLower(), "false").Set(disabledCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting provider metrics");
            }
        }

        private async Task CollectModelMappingMetrics(IServiceScope scope)
        {
            try
            {
                var modelMappingService = scope.ServiceProvider.GetRequiredService<ConduitLLM.Configuration.IModelProviderMappingService>();
                var mappings = await modelMappingService.GetAllMappingsAsync();

                // Group by provider and count
                var mappingsByProvider = mappings
                    .GroupBy(m => m.ProviderType)
                    .Select(g => new { Provider = g.Key.ToString().ToLower(), Count = g.Count() });

                foreach (var group in mappingsByProvider)
                {
                    ActiveModelMappings.WithLabels(group.Provider).Set(group.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting model mapping metrics");
            }
        }

        // Static methods to be called by Admin API operations
        /// <summary>
        /// Records a virtual key operation metric.
        /// </summary>
        /// <param name="operation">The operation type (e.g., create, update, delete).</param>
        /// <param name="status">The operation status (e.g., success, failure).</param>
        /// <param name="durationSeconds">The optional operation duration in seconds.</param>
        public static void RecordVirtualKeyOperation(string operation, string status, double? durationSeconds = null)
        {
            VirtualKeyOperations.WithLabels(operation, status).Inc();
            if (durationSeconds.HasValue)
            {
                VirtualKeyOperationDuration.WithLabels(operation).Observe(durationSeconds.Value);
            }
        }

        /// <summary>
        /// Records a provider operation metric.
        /// </summary>
        /// <param name="operation">The operation type.</param>
        /// <param name="provider">The provider name.</param>
        /// <param name="status">The operation status.</param>
        /// <param name="durationSeconds">The optional operation duration in seconds.</param>
        public static void RecordProviderOperation(string operation, string provider, string status, double? durationSeconds = null)
        {
            ProviderOperations.WithLabels(operation, provider, status).Inc();
            if (durationSeconds.HasValue && operation == "test")
            {
                ProviderTestDuration.WithLabels(provider).Observe(durationSeconds.Value);
            }
        }

        /// <summary>
        /// Records a model mapping operation metric.
        /// </summary>
        /// <param name="operation">The operation type.</param>
        /// <param name="status">The operation status.</param>
        public static void RecordModelMappingOperation(string operation, string status)
        {
            ModelMappingOperations.WithLabels(operation, status).Inc();
        }

        /// <summary>
        /// Records a configuration change metric.
        /// </summary>
        /// <param name="entityType">The entity type that was changed.</param>
        /// <param name="changeType">The type of change performed.</param>
        public static void RecordConfigurationChange(string entityType, string changeType)
        {
            ConfigurationChanges.WithLabels(entityType, changeType).Inc();
        }

        /// <summary>
        /// Records an authentication attempt metric.
        /// </summary>
        /// <param name="status">The authentication status (e.g., success, failure).</param>
        public static void RecordAuthentication(string status)
        {
            AdminApiAuthentications.WithLabels(status).Inc();
        }

        /// <summary>
        /// Sets the current count of active admin sessions.
        /// </summary>
        /// <param name="count">The number of active sessions.</param>
        public static void SetActiveSessions(int count)
        {
            ActiveAdminSessions.Set(count);
        }

        /// <summary>
        /// Records a CSV operation metric.
        /// </summary>
        /// <param name="operation">The CSV operation type (e.g., import, export).</param>
        /// <param name="entityType">The entity type being processed.</param>
        /// <param name="status">The operation status.</param>
        /// <param name="recordCount">The number of records processed.</param>
        /// <param name="durationSeconds">The optional operation duration in seconds.</param>
        public static void RecordCsvOperation(string operation, string entityType, string status, int recordCount = 0, double? durationSeconds = null)
        {
            CsvOperations.WithLabels(operation, entityType, status).Inc();
            
            if (recordCount > 0)
            {
                CsvRecordsProcessed.WithLabels(operation, entityType).Inc(recordCount);
            }
            
            if (durationSeconds.HasValue)
            {
                CsvOperationDuration.WithLabels(operation, entityType).Observe(durationSeconds.Value);
            }
        }
    }
}
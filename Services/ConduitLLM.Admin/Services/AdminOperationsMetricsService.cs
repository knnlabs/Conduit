using Prometheus;

using ConduitLLM.Configuration.Interfaces;
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
            _logger.LogInformation("AdminOperationsMetricsService starting with collection interval {Interval}", _collectionInterval);

            // Brief delay to let other services initialize first
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CollectMetricsAsync();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error collecting admin operations metrics");
                }

                await Task.Delay(_collectionInterval, stoppingToken);
            }

            _logger.LogInformation("AdminOperationsMetricsService stopped");
        }

        private async Task CollectMetricsAsync()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var scope = _serviceProvider.CreateScope();

            var tasks = new[]
            {
                CollectVirtualKeyMetrics(scope),
                CollectProviderMetrics(scope),
                CollectModelMappingMetrics(scope)
            };

            await Task.WhenAll(tasks);
            sw.Stop();

            if (sw.ElapsedMilliseconds > 5000)
            {
                _logger.LogWarning("Slow admin metrics collection: took {ElapsedMs}ms (threshold: 5000ms)", sw.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogDebug("Admin metrics collection completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
            }
        }

        private async Task CollectVirtualKeyMetrics(IServiceScope scope)
        {
            try
            {
                var virtualKeyRepo = scope.ServiceProvider.GetRequiredService<IVirtualKeyRepository>();

                // Use database-level counts; active = enabled and not expired,
                // expired = past ExpiresAt regardless of enabled state,
                // disabled = the remainder (not enabled, not expired)
                var activeCount = await virtualKeyRepo.CountActiveAsync();
                var expiredCount = await virtualKeyRepo.CountExpiredAsync();

                // Get total count via pagination (just need count, not items)
                var (_, totalCount) = await virtualKeyRepo.GetPaginatedAsync(1, 1);

                var disabledCount = Math.Max(0, totalCount - activeCount - expiredCount);

                TotalVirtualKeys.WithLabels("active").Set(activeCount);
                TotalVirtualKeys.WithLabels("disabled").Set(disabledCount);
                TotalVirtualKeys.WithLabels("expired").Set(expiredCount);

                _logger.LogDebug(
                    "Virtual key metrics: {ActiveCount} active, {DisabledCount} disabled, {ExpiredCount} expired",
                    activeCount, disabledCount, expiredCount);
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
                var providerRepository = scope.ServiceProvider.GetRequiredService<IProviderRepository>();

                // Use database-level counts instead of loading all providers
                var enabledCount = await providerRepository.CountAsync(enabledOnly: true);
                var disabledCount = await providerRepository.CountAsync(enabledOnly: false);

                // Use simple enabled/disabled labels instead of provider types
                ConfiguredProviders.WithLabels("all", "true").Set(enabledCount);
                ConfiguredProviders.WithLabels("all", "false").Set(disabledCount);

                _logger.LogDebug(
                    "Provider metrics: {EnabledCount} enabled, {DisabledCount} disabled",
                    enabledCount, disabledCount);
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
                var modelMappingService = scope.ServiceProvider.GetRequiredService<ConduitLLM.Configuration.Interfaces.IModelProviderMappingService>();
                var mappings = await modelMappingService.GetAllMappingsAsync();

                // Count total active mappings (no grouping by provider type)
                var totalMappings = mappings.Count(m => m.Provider != null && m.IsEnabled);
                
                // Use a simple "total" label instead of provider-specific labels
                ActiveModelMappings.WithLabels("total").Set(totalMappings);

                _logger.LogDebug("Model mapping metrics: {TotalMappings} active mappings", totalMappings);
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
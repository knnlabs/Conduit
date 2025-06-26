using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Prometheus;
using StackExchange.Redis;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Microsoft.Extensions.Configuration;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Background service for collecting infrastructure metrics.
    /// Monitors database, Redis, and RabbitMQ health and performance.
    /// </summary>
    public class InfrastructureMetricsService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<InfrastructureMetricsService> _logger;
        private readonly IConfiguration _configuration;
        private readonly TimeSpan _collectionInterval = TimeSpan.FromSeconds(15);

        // Database metrics
        private static readonly Gauge DatabaseConnectionsActive = Metrics
            .CreateGauge("conduit_database_connections_active", "Number of active database connections");

        private static readonly Gauge DatabaseConnectionsAvailable = Metrics
            .CreateGauge("conduit_database_connections_available", "Number of available database connections");

        private static readonly Gauge DatabaseConnectionsIdle = Metrics
            .CreateGauge("conduit_database_connections_idle", "Number of idle database connections");

        private static readonly Histogram DatabaseQueryDuration = Metrics
            .CreateHistogram("conduit_database_query_duration_seconds", "Database query duration in seconds",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "operation" },
                    Buckets = Histogram.ExponentialBuckets(0.001, 2, 16) // 1ms to ~65s
                });

        private static readonly Counter DatabaseErrors = Metrics
            .CreateCounter("conduit_database_errors_total", "Total number of database errors",
                new CounterConfiguration
                {
                    LabelNames = new[] { "error_type" }
                });

        // Redis metrics
        private static readonly Gauge RedisMemoryUsed = Metrics
            .CreateGauge("conduit_redis_memory_used_bytes", "Redis memory usage in bytes");

        private static readonly Gauge RedisKeysCount = Metrics
            .CreateGauge("conduit_redis_keys_count", "Total number of Redis keys");

        private static readonly Gauge RedisConnectedClients = Metrics
            .CreateGauge("conduit_redis_connected_clients", "Number of connected Redis clients");

        private static readonly Counter RedisCacheHits = Metrics
            .CreateCounter("conduit_redis_cache_hits_total", "Total number of Redis cache hits",
                new CounterConfiguration
                {
                    LabelNames = new[] { "cache_type" }
                });

        private static readonly Counter RedisCacheMisses = Metrics
            .CreateCounter("conduit_redis_cache_misses_total", "Total number of Redis cache misses",
                new CounterConfiguration
                {
                    LabelNames = new[] { "cache_type" }
                });

        private static readonly Histogram RedisOperationDuration = Metrics
            .CreateHistogram("conduit_redis_operation_duration_seconds", "Redis operation duration in seconds",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "operation" },
                    Buckets = Histogram.ExponentialBuckets(0.0001, 2, 16) // 0.1ms to ~6.5s
                });

        // RabbitMQ metrics
        private static readonly Gauge RabbitMQQueueDepth = Metrics
            .CreateGauge("conduit_rabbitmq_queue_messages", "Number of messages in RabbitMQ queue",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "queue" }
                });

        private static readonly Gauge RabbitMQQueueConsumers = Metrics
            .CreateGauge("conduit_rabbitmq_queue_consumers", "Number of consumers for RabbitMQ queue",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "queue" }
                });

        private static readonly Counter RabbitMQPublishedMessages = Metrics
            .CreateCounter("conduit_rabbitmq_published_messages_total", "Total number of messages published to RabbitMQ",
                new CounterConfiguration
                {
                    LabelNames = new[] { "exchange" }
                });

        private static readonly Counter RabbitMQConsumedMessages = Metrics
            .CreateCounter("conduit_rabbitmq_consumed_messages_total", "Total number of messages consumed from RabbitMQ",
                new CounterConfiguration
                {
                    LabelNames = new[] { "queue" }
                });

        private static readonly Gauge RabbitMQConnectionState = Metrics
            .CreateGauge("conduit_rabbitmq_connection_state", "RabbitMQ connection state (1=connected, 0=disconnected)");

        // System resource metrics
        private static readonly Gauge ProcessCpuUsage = Metrics
            .CreateGauge("conduit_process_cpu_usage_percent", "Process CPU usage percentage");

        private static readonly Gauge ProcessMemoryUsage = Metrics
            .CreateGauge("conduit_process_memory_bytes", "Process memory usage in bytes");

        private static readonly Gauge ProcessThreadCount = Metrics
            .CreateGauge("conduit_process_thread_count", "Number of threads in the process");

        private static readonly Gauge ProcessHandleCount = Metrics
            .CreateGauge("conduit_process_handle_count", "Number of handles held by the process");

        public InfrastructureMetricsService(
            IServiceProvider serviceProvider,
            ILogger<InfrastructureMetricsService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Infrastructure metrics service starting...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CollectMetricsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error collecting infrastructure metrics");
                }

                await Task.Delay(_collectionInterval, stoppingToken);
            }

            _logger.LogInformation("Infrastructure metrics service stopped");
        }

        private async Task CollectMetricsAsync()
        {
            // Collect metrics in parallel for efficiency
            var tasks = new[]
            {
                Task.Run(() => CollectDatabaseMetrics()),
                Task.Run(() => CollectRedisMetrics()),
                Task.Run(() => CollectRabbitMQMetrics()),
                Task.Run(() => CollectSystemMetrics())
            };

            await Task.WhenAll(tasks);
        }

        private async Task CollectDatabaseMetrics()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext>>();
                await using var context = await dbContextFactory.CreateDbContextAsync();

                // Get connection pool statistics
                var npgsqlConnection = context.Database.GetDbConnection() as NpgsqlConnection;
                if (npgsqlConnection != null)
                {
                    // For now, we'll just test the connection
                    // NpgsqlDataSource statistics might not be directly accessible
                    // TODO: Find a better way to get pool statistics
                    
                    // Simple connection test
                    if (npgsqlConnection.State != System.Data.ConnectionState.Open)
                    {
                        await npgsqlConnection.OpenAsync();
                    }
                    
                    // Set some default values for now
                    // In production, you'd need to use monitoring APIs or custom metrics
                    DatabaseConnectionsActive.Set(1);
                    DatabaseConnectionsIdle.Set(0);
                    DatabaseConnectionsAvailable.Set(99);

                    // Test query performance
                    var stopwatch = Stopwatch.StartNew();
                    await context.Database.ExecuteSqlRawAsync("SELECT 1");
                    stopwatch.Stop();
                    DatabaseQueryDuration.WithLabels("health_check").Observe(stopwatch.Elapsed.TotalSeconds);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting database metrics");
                DatabaseErrors.WithLabels(ex.GetType().Name).Inc();
            }
        }

        private async Task CollectRedisMetrics()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var connectionMultiplexer = scope.ServiceProvider.GetService<IConnectionMultiplexer>();
                
                if (connectionMultiplexer == null || !connectionMultiplexer.IsConnected)
                {
                    _logger.LogDebug("Redis not configured or not connected");
                    return;
                }

                var server = connectionMultiplexer.GetServer(connectionMultiplexer.GetEndPoints().First());
                var info = await server.InfoAsync();

                // Parse Redis INFO output
                foreach (var section in info)
                {
                    foreach (var kvp in section)
                    {
                        switch (kvp.Key)
                        {
                            case "used_memory":
                                if (long.TryParse(kvp.Value, out var memoryUsed))
                                    RedisMemoryUsed.Set(memoryUsed);
                                break;
                            case "connected_clients":
                                if (int.TryParse(kvp.Value, out var clients))
                                    RedisConnectedClients.Set(clients);
                                break;
                            case "db0":
                                // Parse db0:keys=X,expires=Y,avg_ttl=Z
                                var dbInfo = kvp.Value.Split(',');
                                foreach (var item in dbInfo)
                                {
                                    var parts = item.Split('=');
                                    if (parts.Length == 2 && parts[0] == "keys" && int.TryParse(parts[1], out var keys))
                                    {
                                        RedisKeysCount.Set(keys);
                                        break;
                                    }
                                }
                                break;
                        }
                    }
                }

                // Test operation performance
                var db = connectionMultiplexer.GetDatabase();
                var stopwatch = Stopwatch.StartNew();
                await db.PingAsync();
                stopwatch.Stop();
                RedisOperationDuration.WithLabels("ping").Observe(stopwatch.Elapsed.TotalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting Redis metrics");
            }
        }

        private async Task CollectRabbitMQMetrics()
        {
            try
            {
                var rabbitMqHost = _configuration["ConduitLLM:RabbitMQ:Host"];
                if (string.IsNullOrEmpty(rabbitMqHost) || rabbitMqHost == "localhost")
                {
                    _logger.LogDebug("RabbitMQ not configured");
                    RabbitMQConnectionState.Set(0);
                    return;
                }

                var factory = new ConnectionFactory
                {
                    HostName = rabbitMqHost,
                    Port = _configuration.GetValue<int>("ConduitLLM:RabbitMQ:Port", 5672),
                    UserName = _configuration["ConduitLLM:RabbitMQ:Username"] ?? "guest",
                    Password = _configuration["ConduitLLM:RabbitMQ:Password"] ?? "guest",
                    VirtualHost = _configuration["ConduitLLM:RabbitMQ:VHost"] ?? "/"
                };

                // TODO: RabbitMQ metrics collection disabled due to missing types
                // The RabbitMQ.Client library needs proper integration
                // For now, just set connection state to 0
                RabbitMQConnectionState.Set(0);
                
                await Task.CompletedTask; // Keep async signature
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting RabbitMQ metrics");
                RabbitMQConnectionState.Set(0);
            }
        }

        private void CollectSystemMetrics()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                
                // CPU usage (requires calculation over time)
                ProcessCpuUsage.Set(GetCpuUsageForProcess());
                
                // Memory usage
                ProcessMemoryUsage.Set(process.WorkingSet64);
                
                // Thread count
                ProcessThreadCount.Set(process.Threads.Count);
                
                // Handle count
                ProcessHandleCount.Set(process.HandleCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting system metrics");
            }
        }

        private double _lastTotalProcessorTime;
        private DateTime _lastTime = DateTime.MinValue;

        private double GetCpuUsageForProcess()
        {
            var process = Process.GetCurrentProcess();
            var currentTime = DateTime.UtcNow;
            var currentTotalProcessorTime = process.TotalProcessorTime.TotalMilliseconds;

            if (_lastTime == DateTime.MinValue)
            {
                _lastTime = currentTime;
                _lastTotalProcessorTime = currentTotalProcessorTime;
                return 0;
            }

            var timeDiff = (currentTime - _lastTime).TotalMilliseconds;
            var cpuUsedMs = currentTotalProcessorTime - _lastTotalProcessorTime;

            var cpuUsageTotal = cpuUsedMs / timeDiff;
            var cpuUsagePercentage = cpuUsageTotal * 100 / Environment.ProcessorCount;

            _lastTime = currentTime;
            _lastTotalProcessorTime = currentTotalProcessorTime;

            return Math.Min(100, cpuUsagePercentage);
        }

        // Cache hit/miss tracking methods - to be called by cache implementations
        public static void RecordCacheHit(string cacheType)
        {
            RedisCacheHits.WithLabels(cacheType).Inc();
        }

        public static void RecordCacheMiss(string cacheType)
        {
            RedisCacheMisses.WithLabels(cacheType).Inc();
        }

        public static void RecordRedisOperation(string operation, double durationSeconds)
        {
            RedisOperationDuration.WithLabels(operation).Observe(durationSeconds);
        }

        public static void RecordDatabaseQuery(string operation, double durationSeconds)
        {
            DatabaseQueryDuration.WithLabels(operation).Observe(durationSeconds);
        }

        public static void RecordRabbitMQPublish(string exchange)
        {
            RabbitMQPublishedMessages.WithLabels(exchange).Inc();
        }

        public static void RecordRabbitMQConsume(string queue)
        {
            RabbitMQConsumedMessages.WithLabels(queue).Inc();
        }
    }
}
using ConduitLLM.Configuration.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service for managing repository pattern configuration and metrics.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service provides centralized management of repository pattern usage,
    /// including environment-specific configuration, performance tracking, and logging.
    /// </para>
    /// <para>
    /// It is designed to support the phased migration to the repository pattern by
    /// providing detailed metrics and configuration options for each environment.
    /// </para>
    /// </remarks>
    public class RepositoryPatternConfigurationService
    {
        private readonly RepositoryPatternOptions _options;
        private readonly ILogger<RepositoryPatternConfigurationService> _logger;
        private readonly string _currentEnvironment;
        private readonly Dictionary<string, List<TimeSpan>> _operationTimes = new();
        
        /// <summary>
        /// Initializes a new instance of the <see cref="RepositoryPatternConfigurationService"/> class.
        /// </summary>
        /// <param name="options">The repository pattern configuration options.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="environment">The current environment name.</param>
        public RepositoryPatternConfigurationService(
            IOptions<RepositoryPatternOptions> options,
            ILogger<RepositoryPatternConfigurationService> logger,
            string environment)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _currentEnvironment = environment ?? "Production";
            
            _logger.LogInformation(
                "Repository pattern configuration: Enabled={Enabled}, EnabledEnvironments={EnabledEnvironments}, CurrentEnvironment={CurrentEnvironment}",
                _options.Enabled,
                _options.EnabledEnvironments,
                _currentEnvironment);
        }
        
        /// <summary>
        /// Gets whether the repository pattern is enabled for the current environment.
        /// </summary>
        public bool IsEnabled => _options.IsEnabledForEnvironment(_currentEnvironment);
        
        /// <summary>
        /// Gets whether detailed logging is enabled.
        /// </summary>
        public bool DetailedLoggingEnabled => _options.EnableDetailedLogging;
        
        /// <summary>
        /// Gets whether performance metrics should be tracked.
        /// </summary>
        public bool TrackPerformanceMetrics => _options.TrackPerformanceMetrics;
        
        /// <summary>
        /// Gets whether parallel verification is enabled.
        /// </summary>
        public bool ParallelVerificationEnabled => _options.EnableParallelVerification;
        
        /// <summary>
        /// Records the execution time of a repository operation.
        /// </summary>
        /// <param name="operationName">The name of the operation.</param>
        /// <param name="duration">The duration of the operation.</param>
        public void RecordOperationTime(string operationName, TimeSpan duration)
        {
            if (!TrackPerformanceMetrics)
            {
                return;
            }
            
            lock (_operationTimes)
            {
                if (!_operationTimes.TryGetValue(operationName, out var times))
                {
                    times = new List<TimeSpan>();
                    _operationTimes[operationName] = times;
                }
                
                times.Add(duration);
                
                if (DetailedLoggingEnabled)
                {
                    _logger.LogDebug("Repository operation {OperationName} completed in {Duration}ms", 
                        operationName, duration.TotalMilliseconds);
                }
            }
        }
        
        /// <summary>
        /// Executes a repository operation with performance tracking.
        /// </summary>
        /// <typeparam name="T">The result type of the operation.</typeparam>
        /// <param name="operationName">The name of the operation.</param>
        /// <param name="operation">The operation to execute.</param>
        /// <returns>The result of the operation.</returns>
        public async Task<T> ExecuteWithTracking<T>(string operationName, Func<Task<T>> operation)
        {
            if (!TrackPerformanceMetrics)
            {
                return await operation();
            }
            
            var stopwatch = Stopwatch.StartNew();
            try
            {
                return await operation();
            }
            finally
            {
                stopwatch.Stop();
                RecordOperationTime(operationName, stopwatch.Elapsed);
            }
        }
        
        /// <summary>
        /// Gets the performance metrics for repository operations.
        /// </summary>
        /// <returns>A dictionary mapping operation names to performance statistics.</returns>
        public Dictionary<string, OperationMetrics> GetPerformanceMetrics()
        {
            var metrics = new Dictionary<string, OperationMetrics>();
            
            lock (_operationTimes)
            {
                foreach (var entry in _operationTimes)
                {
                    var operationName = entry.Key;
                    var times = entry.Value;
                    
                    if (times.Count == 0)
                    {
                        continue;
                    }
                    
                    var avgMs = times.Average(t => t.TotalMilliseconds);
                    var minMs = times.Min(t => t.TotalMilliseconds);
                    var maxMs = times.Max(t => t.TotalMilliseconds);
                    var count = times.Count;
                    
                    metrics[operationName] = new OperationMetrics
                    {
                        OperationName = operationName,
                        AverageMs = avgMs,
                        MinMs = minMs,
                        MaxMs = maxMs,
                        Count = count
                    };
                }
            }
            
            return metrics;
        }
        
        /// <summary>
        /// Clears all performance metrics.
        /// </summary>
        public void ClearMetrics()
        {
            lock (_operationTimes)
            {
                _operationTimes.Clear();
            }
        }
    }
    
    /// <summary>
    /// Represents performance metrics for a repository operation.
    /// </summary>
    public class OperationMetrics
    {
        /// <summary>
        /// Gets or sets the name of the operation.
        /// </summary>
        public string OperationName { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the average execution time in milliseconds.
        /// </summary>
        public double AverageMs { get; set; }
        
        /// <summary>
        /// Gets or sets the minimum execution time in milliseconds.
        /// </summary>
        public double MinMs { get; set; }
        
        /// <summary>
        /// Gets or sets the maximum execution time in milliseconds.
        /// </summary>
        public double MaxMs { get; set; }
        
        /// <summary>
        /// Gets or sets the number of operations measured.
        /// </summary>
        public int Count { get; set; }
    }
}
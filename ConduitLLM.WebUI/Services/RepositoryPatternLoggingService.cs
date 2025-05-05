using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service for logging repository pattern usage and activity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service provides detailed logging of repository pattern operations,
    /// including metrics on usage, errors, and performance. It's intended for
    /// use during the migration phase to help verify that the implementation
    /// is working correctly.
    /// </para>
    /// <para>
    /// The service buffers log entries and periodically flushes them to avoid
    /// overwhelming the logging system with high-frequency events.
    /// </para>
    /// </remarks>
    public class RepositoryPatternLoggingService
    {
        private readonly ILogger<RepositoryPatternLoggingService> _logger;
        private readonly RepositoryPatternConfigurationService _configService;
        private readonly ConcurrentQueue<RepositoryLogEntry> _logEntries = new();
        private readonly object _flushLock = new();
        private readonly int _flushThreshold = 100;
        private readonly TimeSpan _flushInterval = TimeSpan.FromMinutes(1);
        private DateTime _lastFlushTime = DateTime.UtcNow;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="RepositoryPatternLoggingService"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="configService">The repository pattern configuration service.</param>
        public RepositoryPatternLoggingService(
            ILogger<RepositoryPatternLoggingService> logger,
            RepositoryPatternConfigurationService configService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }
        
        /// <summary>
        /// Logs a repository method call.
        /// </summary>
        /// <param name="repositoryName">The name of the repository.</param>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="parameters">Optional parameters for additional context.</param>
        public void LogMethodCall(string repositoryName, string methodName, params object[] parameters)
        {
            if (!_configService.DetailedLoggingEnabled)
            {
                return;
            }
            
            var entry = new RepositoryLogEntry
            {
                Timestamp = DateTime.UtcNow,
                RepositoryName = repositoryName,
                MethodName = methodName,
                EntryType = RepositoryLogEntryType.MethodCall,
                Parameters = parameters
            };
            
            _logEntries.Enqueue(entry);
            
            CheckAndFlush();
        }
        
        /// <summary>
        /// Logs a repository method result.
        /// </summary>
        /// <param name="repositoryName">The name of the repository.</param>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="result">The result of the method call.</param>
        /// <param name="duration">The duration of the method call.</param>
        public void LogMethodResult(string repositoryName, string methodName, object result, TimeSpan duration)
        {
            if (!_configService.DetailedLoggingEnabled)
            {
                return;
            }
            
            var entry = new RepositoryLogEntry
            {
                Timestamp = DateTime.UtcNow,
                RepositoryName = repositoryName,
                MethodName = methodName,
                EntryType = RepositoryLogEntryType.MethodResult,
                Duration = duration,
                Result = result
            };
            
            _logEntries.Enqueue(entry);
            
            CheckAndFlush();
        }
        
        /// <summary>
        /// Logs a repository method error.
        /// </summary>
        /// <param name="repositoryName">The name of the repository.</param>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="exception">The exception that occurred.</param>
        /// <param name="duration">The duration of the method call before the error.</param>
        public void LogMethodError(string repositoryName, string methodName, Exception exception, TimeSpan duration)
        {
            // Always log errors, regardless of detailed logging setting
            var entry = new RepositoryLogEntry
            {
                Timestamp = DateTime.UtcNow,
                RepositoryName = repositoryName,
                MethodName = methodName,
                EntryType = RepositoryLogEntryType.MethodError,
                Duration = duration,
                Exception = exception
            };
            
            _logEntries.Enqueue(entry);
            
            // Log errors immediately
            Flush();
            
            // Also log through the logger directly for critical errors
            _logger.LogError(exception, "Repository error in {RepositoryName}.{MethodName}: {Message}",
                repositoryName, methodName, exception.Message);
        }
        
        /// <summary>
        /// Flushes all pending log entries to the logger.
        /// </summary>
        public void Flush()
        {
            lock (_flushLock)
            {
                _lastFlushTime = DateTime.UtcNow;
                
                while (_logEntries.TryDequeue(out var entry))
                {
                    LogEntry(entry);
                }
            }
        }
        
        /// <summary>
        /// Logs a repository operation with performance tracking.
        /// </summary>
        /// <typeparam name="T">The result type of the operation.</typeparam>
        /// <param name="repositoryName">The name of the repository.</param>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="parameters">Optional parameters for additional context.</param>
        /// <returns>The result of the operation.</returns>
        public async Task<T> LogOperationAsync<T>(string repositoryName, string methodName, Func<Task<T>> operation, params object[] parameters)
        {
            if (!_configService.DetailedLoggingEnabled)
            {
                return await operation();
            }
            
            LogMethodCall(repositoryName, methodName, parameters);
            
            var startTime = DateTime.UtcNow;
            try
            {
                var result = await operation();
                var duration = DateTime.UtcNow - startTime;
                
                // Safely handle potential null results
                LogMethodResult(repositoryName, methodName, result is null ? (object)"null result" : result, duration);
                return result;
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                LogMethodError(repositoryName, methodName, ex, duration);
                throw;
            }
        }
        
        /// <summary>
        /// Logs a repository operation with performance tracking for void operations.
        /// </summary>
        /// <param name="repositoryName">The name of the repository.</param>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="parameters">Optional parameters for additional context.</param>
        public async Task LogOperationAsync(string repositoryName, string methodName, Func<Task> operation, params object[] parameters)
        {
            if (!_configService.DetailedLoggingEnabled)
            {
                await operation();
                return;
            }
            
            LogMethodCall(repositoryName, methodName, parameters);
            
            var startTime = DateTime.UtcNow;
            try
            {
                await operation();
                var duration = DateTime.UtcNow - startTime;
                
                // Use an empty string as result for void methods to avoid null reference issue
                LogMethodResult(repositoryName, methodName, "void result", duration);
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                LogMethodError(repositoryName, methodName, ex, duration);
                throw;
            }
        }
        
        private void CheckAndFlush()
        {
            var shouldFlush = false;
            
            if (_logEntries.Count >= _flushThreshold)
            {
                shouldFlush = true;
            }
            else if (DateTime.UtcNow - _lastFlushTime >= _flushInterval)
            {
                shouldFlush = true;
            }
            
            if (shouldFlush)
            {
                Flush();
            }
        }
        
        private void LogEntry(RepositoryLogEntry entry)
        {
            switch (entry.EntryType)
            {
                case RepositoryLogEntryType.MethodCall:
                    _logger.LogDebug("Repository call: {RepositoryName}.{MethodName}",
                        entry.RepositoryName, entry.MethodName);
                    break;
                    
                case RepositoryLogEntryType.MethodResult:
                    _logger.LogDebug("Repository result: {RepositoryName}.{MethodName} completed in {Duration}ms",
                        entry.RepositoryName, entry.MethodName, entry.Duration?.TotalMilliseconds);
                    break;
                    
                case RepositoryLogEntryType.MethodError:
                    _logger.LogError(entry.Exception, "Repository error: {RepositoryName}.{MethodName} failed after {Duration}ms",
                        entry.RepositoryName, entry.MethodName, entry.Duration?.TotalMilliseconds);
                    break;
            }
        }
        
        /// <summary>
        /// Gets or sets the flush threshold (number of entries).
        /// </summary>
        public int FlushThreshold
        {
            get => _flushThreshold;
            init => _flushThreshold = value;
        }
        
        /// <summary>
        /// Gets or sets the flush interval.
        /// </summary>
        public TimeSpan FlushInterval
        {
            get => _flushInterval;
            init => _flushInterval = value;
        }
    }
    
    /// <summary>
    /// Represents a repository log entry.
    /// </summary>
    internal class RepositoryLogEntry
    {
        /// <summary>
        /// Gets or sets the timestamp of the entry.
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Gets or sets the name of the repository.
        /// </summary>
        public string RepositoryName { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the name of the method.
        /// </summary>
        public string MethodName { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the type of the entry.
        /// </summary>
        public RepositoryLogEntryType EntryType { get; set; }
        
        /// <summary>
        /// Gets or sets the parameters of the method call.
        /// </summary>
        public object[]? Parameters { get; set; }
        
        /// <summary>
        /// Gets or sets the result of the method call.
        /// </summary>
        public object? Result { get; set; }
        
        /// <summary>
        /// Gets or sets the exception that occurred.
        /// </summary>
        public Exception? Exception { get; set; }
        
        /// <summary>
        /// Gets or sets the duration of the method call.
        /// </summary>
        public TimeSpan? Duration { get; set; }
    }
    
    /// <summary>
    /// Represents the type of a repository log entry.
    /// </summary>
    internal enum RepositoryLogEntryType
    {
        /// <summary>
        /// A method call entry.
        /// </summary>
        MethodCall,
        
        /// <summary>
        /// A method result entry.
        /// </summary>
        MethodResult,
        
        /// <summary>
        /// A method error entry.
        /// </summary>
        MethodError
    }
}
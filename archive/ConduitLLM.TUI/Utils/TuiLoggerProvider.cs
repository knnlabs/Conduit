using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.TUI.Utils
{
    /// <summary>
    /// Custom logger provider that captures logs to a buffer for display in the TUI
    /// </summary>
    public class TuiLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, TuiLogger> _loggers = new();
        private readonly LogBuffer _logBuffer;
        private readonly LogLevel _minimumLevel;

        public TuiLoggerProvider(LogBuffer logBuffer, LogLevel minimumLevel = LogLevel.Information)
        {
            _logBuffer = logBuffer ?? throw new ArgumentNullException(nameof(logBuffer));
            _minimumLevel = minimumLevel;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new TuiLogger(name, _logBuffer, _minimumLevel));
        }

        public void Dispose()
        {
            _loggers.Clear();
        }
    }

    /// <summary>
    /// Logger implementation that writes to the TUI log buffer
    /// </summary>
    public class TuiLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly LogBuffer _logBuffer;
        private readonly LogLevel _minimumLevel;

        public TuiLogger(string categoryName, LogBuffer logBuffer, LogLevel minimumLevel)
        {
            _categoryName = categoryName;
            _logBuffer = logBuffer;
            _minimumLevel = minimumLevel;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message) && exception == null)
                return;

            var timestamp = DateTime.Now;
            var logEntry = new LogEntry
            {
                Timestamp = timestamp,
                Level = logLevel,
                Category = _categoryName,
                Message = message,
                Exception = exception
            };

            _logBuffer.Add(logEntry);
        }
    }

    /// <summary>
    /// Thread-safe buffer for storing log entries
    /// </summary>
    public class LogBuffer
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();
        private readonly int _maxEntries;
        public event EventHandler<LogEntry>? LogAdded;

        public LogBuffer(int maxEntries = 1000)
        {
            _maxEntries = maxEntries;
        }

        public void Add(LogEntry entry)
        {
            _entries.Enqueue(entry);
            
            // Remove old entries if we exceed the limit
            while (_entries.Count > _maxEntries && _entries.TryDequeue(out _))
            {
                // Entry removed
            }

            // Notify subscribers
            LogAdded?.Invoke(this, entry);
        }

        public IEnumerable<LogEntry> GetEntries()
        {
            return _entries.ToArray();
        }

        public void Clear()
        {
            while (_entries.TryDequeue(out _))
            {
                // Entry removed
            }
        }
    }

    /// <summary>
    /// Represents a single log entry
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Exception? Exception { get; set; }

        public string GetFormattedMessage()
        {
            var levelString = Level switch
            {
                LogLevel.Trace => "TRACE",
                LogLevel.Debug => "DEBUG",
                LogLevel.Information => "INFO",
                LogLevel.Warning => "WARN",
                LogLevel.Error => "ERROR",
                LogLevel.Critical => "FATAL",
                _ => "NONE"
            };

            var baseMessage = $"[{Timestamp:HH:mm:ss}] [{levelString}] {Message}";
            
            if (Exception != null)
            {
                baseMessage += $"\n  Exception: {Exception.Message}";
                if (!string.IsNullOrEmpty(Exception.StackTrace))
                {
                    baseMessage += $"\n  Stack: {Exception.StackTrace}";
                }
            }

            return baseMessage;
        }

        public string GetShortMessage()
        {
            var levelChar = Level switch
            {
                LogLevel.Trace => "T",
                LogLevel.Debug => "D",
                LogLevel.Information => "I",
                LogLevel.Warning => "W",
                LogLevel.Error => "E",
                LogLevel.Critical => "F",
                _ => "?"
            };

            var shortCategory = Category.Length > 20 
                ? "..." + Category.Substring(Category.Length - 17) 
                : Category;

            var shortMessage = Message.Length > 80 
                ? Message.Substring(0, 77) + "..." 
                : Message;

            return $"[{Timestamp:HH:mm:ss}] [{levelChar}] {shortMessage}";
        }
    }
}
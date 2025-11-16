using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Middleware;

namespace ConduitLLM.Core.Extensions
{
    /// <summary>
    /// Extension methods for secure logging that automatically sanitize user input to prevent log injection attacks.
    /// </summary>
    public static class SecureLoggingExtensions
    {
        /// <summary>
        /// Logs a debug message with sanitized user input parameters.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="message">The log message template.</param>
        /// <param name="args">The arguments to be sanitized and logged.</param>
        public static void LogDebugSecure(this ILogger logger, string message, params object?[] args)
        {
            logger.LogDebug(message, SanitizeArgs(args));
        }

        /// <summary>
        /// Logs an information message with sanitized user input parameters.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="message">The log message template.</param>
        /// <param name="args">The arguments to be sanitized and logged.</param>
        public static void LogInformationSecure(this ILogger logger, string message, params object?[] args)
        {
            logger.LogInformation(message, SanitizeArgs(args));
        }

        /// <summary>
        /// Logs a warning message with sanitized user input parameters.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="message">The log message template.</param>
        /// <param name="args">The arguments to be sanitized and logged.</param>
        public static void LogWarningSecure(this ILogger logger, string message, params object?[] args)
        {
            logger.LogWarning(message, SanitizeArgs(args));
        }

        /// <summary>
        /// Logs a warning message with exception and sanitized user input parameters.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="exception">The exception to log.</param>
        /// <param name="message">The log message template.</param>
        /// <param name="args">The arguments to be sanitized and logged.</param>
        public static void LogWarningSecure(this ILogger logger, Exception exception, string message, params object?[] args)
        {
            logger.LogWarning(exception, message, SanitizeArgs(args));
        }

        /// <summary>
        /// Logs an error message with sanitized user input parameters.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="exception">The exception to log.</param>
        /// <param name="message">The log message template.</param>
        /// <param name="args">The arguments to be sanitized and logged.</param>
        public static void LogErrorSecure(this ILogger logger, Exception exception, string message, params object?[] args)
        {
            logger.LogError(exception, message, SanitizeArgs(args));
        }

        /// <summary>
        /// Logs an error message with sanitized user input parameters.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="message">The log message template.</param>
        /// <param name="args">The arguments to be sanitized and logged.</param>
        public static void LogErrorSecure(this ILogger logger, string message, params object?[] args)
        {
            logger.LogError(message, SanitizeArgs(args));
        }

        /// <summary>
        /// Logs a critical message with sanitized user input parameters.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="exception">The exception to log.</param>
        /// <param name="message">The log message template.</param>
        /// <param name="args">The arguments to be sanitized and logged.</param>
        public static void LogCriticalSecure(this ILogger logger, Exception exception, string message, params object?[] args)
        {
            logger.LogCritical(exception, message, SanitizeArgs(args));
        }

        /// <summary>
        /// Logs a critical message with sanitized user input parameters.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="message">The log message template.</param>
        /// <param name="args">The arguments to be sanitized and logged.</param>
        public static void LogCriticalSecure(this ILogger logger, string message, params object?[] args)
        {
            logger.LogCritical(message, SanitizeArgs(args));
        }

        /// <summary>
        /// Sanitizes an object for safe logging.
        /// </summary>
        /// <param name="value">The value to sanitize.</param>
        /// <returns>The sanitized value.</returns>
        public static object? SanitizeForLogging(object? value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is string stringValue)
            {
                return InputSanitizationMiddleware.SanitizeString(stringValue);
            }

            // For collections, sanitize each item
            if (value is IEnumerable<string> stringCollection)
            {
                return stringCollection.Select(s => InputSanitizationMiddleware.SanitizeString(s ?? string.Empty)).ToList();
            }

            // For objects with ToString() overrides, sanitize the string representation
            var stringRepresentation = value.ToString();
            if (stringRepresentation != null && stringRepresentation != value.GetType().FullName)
            {
                return InputSanitizationMiddleware.SanitizeString(stringRepresentation);
            }

            return value;
        }

        private static object?[] SanitizeArgs(object?[] args)
        {
            if (args == null || args.Length == 0)
            {
                return args ?? Array.Empty<object?>();
            }

            var sanitizedArgs = new object?[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                sanitizedArgs[i] = SanitizeForLogging(args[i]);
            }

            return sanitizedArgs;
        }
    }
}
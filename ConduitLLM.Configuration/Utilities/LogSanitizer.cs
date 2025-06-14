using System;
using System.Text.RegularExpressions;

namespace ConduitLLM.Configuration.Utilities
{
    /// <summary>
    /// Utility class for sanitizing user input before logging to prevent log injection attacks.
    /// </summary>
    public static class LogSanitizer
    {
        // Regex patterns for dangerous characters
        private static readonly Regex CrlfPattern = new(@"[\r\n]", RegexOptions.Compiled);
        private static readonly Regex ControlCharPattern = new(@"[\x00-\x1F\x7F]", RegexOptions.Compiled);
        
        // Maximum length for input values
        private const int MaxInputLength = 1000;

        /// <summary>
        /// Sanitizes a string value for safe logging by removing dangerous characters.
        /// </summary>
        /// <param name="input">The input string to sanitize.</param>
        /// <returns>The sanitized string safe for logging.</returns>
        public static string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            // Remove CRLF characters to prevent log injection
            var sanitized = CrlfPattern.Replace(input, " ");

            // Remove other control characters
            sanitized = ControlCharPattern.Replace(sanitized, string.Empty);

            // Truncate if too long
            if (sanitized.Length > MaxInputLength)
            {
                sanitized = sanitized.Substring(0, MaxInputLength);
            }

            return sanitized;
        }

        /// <summary>
        /// Sanitizes an object for safe logging.
        /// </summary>
        /// <param name="value">The value to sanitize.</param>
        /// <returns>The sanitized value.</returns>
        public static object SanitizeObject(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is string stringValue)
            {
                return Sanitize(stringValue);
            }

            // For other types, sanitize their string representation
            var stringRepresentation = value.ToString();
            if (stringRepresentation != null && stringRepresentation != value.GetType().FullName)
            {
                return Sanitize(stringRepresentation);
            }

            return value;
        }
    }
}
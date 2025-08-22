using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace ConduitLLM.Core.Extensions
{
    /// <summary>
    /// Provides methods to sanitize values for logging to prevent log injection attacks.
    /// This class uses patterns that static analysis tools like CodeQL can recognize.
    /// </summary>
    public static class LoggingSanitizer
    {
        private static readonly Regex CrlfPattern = new(@"[\r\n]", RegexOptions.Compiled);
        private static readonly Regex ControlCharPattern = new(@"[\x00-\x1F\x7F]", RegexOptions.Compiled);
        private static readonly Regex UnicodeSeparatorPattern = new(@"[\u2028\u2029]", RegexOptions.Compiled);
        private const int MaxLength = 1000;

        /// <summary>
        /// Sanitizes a value for safe logging. This method is designed to be recognized by static analysis tools.
        /// </summary>
        /// <param name="value">The value to sanitize.</param>
        /// <returns>The sanitized value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object? S(object? value)
        {
            if (value == null) return null;
            
            var str = value.ToString();
            if (str == null) return value;
            
            // Remove CRLF to prevent log injection
            str = CrlfPattern.Replace(str, " ");
            
            // Remove control characters
            str = ControlCharPattern.Replace(str, string.Empty);
            
            // Remove Unicode line/paragraph separators
            str = UnicodeSeparatorPattern.Replace(str, " ");
            
            // Truncate if too long
            if (str.Length > MaxLength)
            {
                str = str.Substring(0, MaxLength);
            }
            
            return str;
        }

        /// <summary>
        /// Sanitizes a string value for safe logging.
        /// </summary>
        /// <param name="value">The string to sanitize.</param>
        /// <returns>The sanitized string.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string? S(string? value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            
            // Remove CRLF to prevent log injection
            value = CrlfPattern.Replace(value, " ");
            
            // Remove control characters
            value = ControlCharPattern.Replace(value, string.Empty);
            
            // Remove Unicode line/paragraph separators
            value = UnicodeSeparatorPattern.Replace(value, " ");
            
            // Truncate if too long
            if (value.Length > MaxLength)
            {
                value = value.Substring(0, MaxLength);
            }
            
            return value;
        }

        /// <summary>
        /// Sanitizes an integer value (pass-through for type safety).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int S(int value) => value;

        /// <summary>
        /// Sanitizes a long value (pass-through for type safety).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long S(long value) => value;

        /// <summary>
        /// Sanitizes a decimal value (pass-through for type safety).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal S(decimal value) => value;

        /// <summary>
        /// Sanitizes a boolean value (pass-through for type safety).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool S(bool value) => value;

        /// <summary>
        /// Sanitizes a DateTime value (pass-through for type safety).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime S(DateTime value) => value;

        /// <summary>
        /// Sanitizes a Guid value (pass-through for type safety).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Guid S(Guid value) => value;
    }
}
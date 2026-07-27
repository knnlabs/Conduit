using System.Runtime.CompilerServices;
using ConfigLoggingSanitizer = ConduitLLM.Configuration.Utilities.LoggingSanitizer;

namespace ConduitLLM.Core.Extensions
{
    /// <summary>
    /// Provides methods to sanitize values for logging to prevent log injection attacks.
    /// This class delegates to the canonical implementation in ConduitLLM.Configuration.Utilities.
    /// </summary>
    /// <remarks>
    /// This is a facade for backward compatibility. The canonical implementation is in
    /// <see cref="ConduitLLM.Configuration.Utilities.LoggingSanitizer"/>.
    /// New code should use the Configuration namespace directly.
    /// </remarks>
    [Obsolete(
        "Use ConduitLLM.Configuration.Utilities.LoggingSanitizer directly.",
        false)]
    public static class LoggingSanitizer
    {
        /// <summary>
        /// Sanitizes a value for safe logging.
        /// </summary>
        /// <param name="value">The value to sanitize.</param>
        /// <returns>The sanitized value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object? S(object? value) => ConfigLoggingSanitizer.S(value);

        /// <summary>
        /// Sanitizes a string value for safe logging.
        /// </summary>
        /// <param name="value">The string to sanitize.</param>
        /// <returns>The sanitized string.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string? S(string? value) => ConfigLoggingSanitizer.S(value);

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

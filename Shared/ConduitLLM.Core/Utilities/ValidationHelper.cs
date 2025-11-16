using System.Text.RegularExpressions;

using ConduitLLM.Core.Exceptions;

namespace ConduitLLM.Core.Utilities
{
    /// <summary>
    /// Helper class for common validation operations to reduce code duplication.
    /// </summary>
    public static class ValidationHelper
    {
        /// <summary>
        /// Validates that a required string parameter is not null or empty.
        /// </summary>
        /// <param name="value">The string value to validate.</param>
        /// <param name="parameterName">The name of the parameter for error messages.</param>
        /// <exception cref="ValidationException">Thrown if the string is null or empty.</exception>
        public static void RequireNonEmpty(string? value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ValidationException($"{parameterName} cannot be null or empty");
            }
        }

        /// <summary>
        /// Validates that a collection is not null or empty.
        /// </summary>
        /// <typeparam name="T">The type of items in the collection.</typeparam>
        /// <param name="collection">The collection to validate.</param>
        /// <param name="parameterName">The name of the parameter for error messages.</param>
        /// <exception cref="ValidationException">Thrown if the collection is null or empty.</exception>
        public static void RequireNonEmpty<T>(IEnumerable<T>? collection, string parameterName)
        {
            if (collection == null || collection.Count() == 0)
            {
                throw new ValidationException($"{parameterName} collection cannot be null or empty");
            }
        }

        /// <summary>
        /// Validates that a value is within a numeric range.
        /// </summary>
        /// <typeparam name="T">The type of the value being validated.</typeparam>
        /// <param name="value">The value to validate.</param>
        /// <param name="min">The minimum allowed value (inclusive).</param>
        /// <param name="max">The maximum allowed value (inclusive).</param>
        /// <param name="parameterName">The name of the parameter for error messages.</param>
        /// <exception cref="ValidationException">Thrown if the value is outside the specified range.</exception>
        public static void RequireRange<T>(T value, T min, T max, string parameterName) where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
            {
                throw new ValidationException($"{parameterName} must be between {min} and {max}");
            }
        }

        /// <summary>
        /// Validates a string value against a regular expression pattern.
        /// </summary>
        /// <param name="value">The string value to validate.</param>
        /// <param name="pattern">The regular expression pattern to match against.</param>
        /// <param name="parameterName">The name of the parameter for error messages.</param>
        /// <param name="description">A description of the expected format for error messages.</param>
        /// <exception cref="ValidationException">Thrown if the string is null, empty, or doesn't match the pattern.</exception>
        public static void RequirePattern(string? value, string pattern, string parameterName, string description = "valid format")
        {
            RequireNonEmpty(value, parameterName);

            if (!Regex.IsMatch(value!, pattern))
            {
                throw new ValidationException($"{parameterName} must be in {description}");
            }
        }

        /// <summary>
        /// Validates an object is not null.
        /// </summary>
        /// <param name="value">The object to validate.</param>
        /// <param name="parameterName">The name of the parameter for error messages.</param>
        /// <exception cref="ValidationException">Thrown if the object is null.</exception>
        public static void RequireNotNull(object? value, string parameterName)
        {
            if (value == null)
            {
                throw new ValidationException($"{parameterName} cannot be null");
            }
        }

        /// <summary>
        /// Validates that a string does not exceed a maximum length.
        /// </summary>
        /// <param name="value">The string value to validate.</param>
        /// <param name="maxLength">The maximum allowed length.</param>
        /// <param name="parameterName">The name of the parameter for error messages.</param>
        /// <exception cref="ValidationException">Thrown if the string exceeds the maximum length.</exception>
        public static void RequireMaxLength(string? value, int maxLength, string parameterName)
        {
            if (value != null && value.Length > maxLength)
            {
                throw new ValidationException($"{parameterName} cannot exceed {maxLength} characters");
            }
        }

        /// <summary>
        /// Validates that a condition is true.
        /// </summary>
        /// <param name="condition">The condition to validate.</param>
        /// <param name="errorMessage">The error message to include in the exception if the condition is false.</param>
        /// <exception cref="ValidationException">Thrown if the condition is false.</exception>
        public static void RequireCondition(bool condition, string errorMessage)
        {
            if (!condition)
            {
                throw new ValidationException(errorMessage);
            }
        }

        /// <summary>
        /// Validates that a Guid is not empty.
        /// </summary>
        /// <param name="value">The Guid to validate.</param>
        /// <param name="parameterName">The name of the parameter for error messages.</param>
        /// <exception cref="ValidationException">Thrown if the Guid is empty.</exception>
        public static void RequireValidGuid(Guid value, string parameterName)
        {
            if (value == Guid.Empty)
            {
                throw new ValidationException($"{parameterName} cannot be an empty GUID");
            }
        }

        /// <summary>
        /// Validates a numeric value is greater than zero.
        /// </summary>
        /// <param name="value">The numeric value to validate.</param>
        /// <param name="parameterName">The name of the parameter for error messages.</param>
        /// <exception cref="ValidationException">Thrown if the value is not greater than zero.</exception>
        public static void RequirePositive(int value, string parameterName)
        {
            if (value <= 0)
            {
                throw new ValidationException($"{parameterName} must be a positive number");
            }
        }

        /// <summary>
        /// Validates a numeric value is greater than zero.
        /// </summary>
        /// <param name="value">The numeric value to validate.</param>
        /// <param name="parameterName">The name of the parameter for error messages.</param>
        /// <exception cref="ValidationException">Thrown if the value is not greater than zero.</exception>
        public static void RequirePositive(decimal value, string parameterName)
        {
            if (value <= 0)
            {
                throw new ValidationException($"{parameterName} must be a positive number");
            }
        }

        /// <summary>
        /// Validates a numeric value is greater than zero.
        /// </summary>
        /// <param name="value">The numeric value to validate.</param>
        /// <param name="parameterName">The name of the parameter for error messages.</param>
        /// <exception cref="ValidationException">Thrown if the value is not greater than zero.</exception>
        public static void RequirePositive(double value, string parameterName)
        {
            if (value <= 0)
            {
                throw new ValidationException($"{parameterName} must be a positive number");
            }
        }

        /// <summary>
        /// Validates a numeric value is non-negative (zero or greater).
        /// </summary>
        /// <param name="value">The numeric value to validate.</param>
        /// <param name="parameterName">The name of the parameter for error messages.</param>
        /// <exception cref="ValidationException">Thrown if the value is negative.</exception>
        public static void RequireNonNegative(int value, string parameterName)
        {
            if (value < 0)
            {
                throw new ValidationException($"{parameterName} cannot be negative");
            }
        }

        /// <summary>
        /// Validates a numeric value is non-negative (zero or greater).
        /// </summary>
        /// <param name="value">The numeric value to validate.</param>
        /// <param name="parameterName">The name of the parameter for error messages.</param>
        /// <exception cref="ValidationException">Thrown if the value is negative.</exception>
        public static void RequireNonNegative(decimal value, string parameterName)
        {
            if (value < 0)
            {
                throw new ValidationException($"{parameterName} cannot be negative");
            }
        }

        /// <summary>
        /// Validates a numeric value is non-negative (zero or greater).
        /// </summary>
        /// <param name="value">The numeric value to validate.</param>
        /// <param name="parameterName">The name of the parameter for error messages.</param>
        /// <exception cref="ValidationException">Thrown if the value is negative.</exception>
        public static void RequireNonNegative(double value, string parameterName)
        {
            if (value < 0)
            {
                throw new ValidationException($"{parameterName} cannot be negative");
            }
        }

        /// <summary>
        /// Validates a date is not in the future.
        /// </summary>
        /// <param name="value">The date to validate.</param>
        /// <param name="parameterName">The name of the parameter for error messages.</param>
        /// <exception cref="ValidationException">Thrown if the date is in the future.</exception>
        public static void RequireNotFutureDate(DateTime value, string parameterName)
        {
            if (value > DateTime.UtcNow)
            {
                throw new ValidationException($"{parameterName} cannot be a future date");
            }
        }

        /// <summary>
        /// Validates a date is not in the past.
        /// </summary>
        /// <param name="value">The date to validate.</param>
        /// <param name="parameterName">The name of the parameter for error messages.</param>
        /// <exception cref="ValidationException">Thrown if the date is in the past.</exception>
        public static void RequireNotPastDate(DateTime value, string parameterName)
        {
            if (value < DateTime.UtcNow)
            {
                throw new ValidationException($"{parameterName} cannot be a past date");
            }
        }

        /// <summary>
        /// Validates a date is within a specified range.
        /// </summary>
        /// <param name="value">The date to validate.</param>
        /// <param name="minDate">The minimum allowed date (inclusive).</param>
        /// <param name="maxDate">The maximum allowed date (inclusive).</param>
        /// <param name="parameterName">The name of the parameter for error messages.</param>
        /// <exception cref="ValidationException">Thrown if the date is outside the specified range.</exception>
        public static void RequireDateRange(DateTime value, DateTime minDate, DateTime maxDate, string parameterName)
        {
            if (value < minDate || value > maxDate)
            {
                throw new ValidationException($"{parameterName} must be between {minDate:d} and {maxDate:d}");
            }
        }

        /// <summary>
        /// Validates an enum value is defined.
        /// </summary>
        /// <typeparam name="T">The enum type.</typeparam>
        /// <param name="value">The enum value to validate.</param>
        /// <param name="parameterName">The name of the parameter for error messages.</param>
        /// <exception cref="ValidationException">Thrown if the enum value is not defined.</exception>
        public static void RequireValidEnum<T>(T value, string parameterName) where T : struct, Enum
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                throw new ValidationException($"Invalid value for {parameterName}");
            }
        }
    }
}

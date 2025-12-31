using System.Text.Json;

namespace ConduitLLM.Functions.Utilities;

/// <summary>
/// Utility class for safely converting JsonElement values to .NET types.
/// </summary>
/// <remarks>
/// When System.Text.Json deserializes to Dictionary&lt;string, object&gt;,
/// values remain as JsonElement instead of their primitive types.
/// These utilities handle conversion to the expected types.
/// </remarks>
public static class JsonElementConverter
{
    /// <summary>
    /// Converts a JsonElement or other value to its actual .NET value.
    /// Recursively converts arrays and objects.
    /// </summary>
    /// <param name="value">The value to convert (may be JsonElement or native type).</param>
    /// <returns>The converted value in the appropriate .NET type.</returns>
    public static object? ConvertJsonElement(object? value)
    {
        if (value is not JsonElement element)
        {
            return value;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(e => ConvertJsonElement(e)).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
            _ => element.ToString()
        };
    }

    /// <summary>
    /// Safely converts a value to string, handling JsonElement.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>String representation or null.</returns>
    public static string? ConvertToString(object? value)
    {
        if (value is JsonElement element)
        {
            return element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : element.ToString();
        }
        return value?.ToString();
    }

    /// <summary>
    /// Safely converts a value to int, handling JsonElement.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>Integer value or null if conversion fails.</returns>
    public static int? ConvertToInt32(object? value)
    {
        if (value == null) return null;

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var intValue))
                return intValue;
            if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out var parsedInt))
                return parsedInt;
            return null;
        }

        try
        {
            return Convert.ToInt32(value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Safely converts a value to long, handling JsonElement.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>Long value or null if conversion fails.</returns>
    public static long? ConvertToInt64(object? value)
    {
        if (value == null) return null;

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var longValue))
                return longValue;
            if (element.ValueKind == JsonValueKind.String && long.TryParse(element.GetString(), out var parsedLong))
                return parsedLong;
            return null;
        }

        try
        {
            return Convert.ToInt64(value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Safely converts a value to double, handling JsonElement.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>Double value or null if conversion fails.</returns>
    public static double? ConvertToDouble(object? value)
    {
        if (value == null) return null;

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number)
                return element.GetDouble();
            if (element.ValueKind == JsonValueKind.String && double.TryParse(element.GetString(), out var parsedDouble))
                return parsedDouble;
            return null;
        }

        try
        {
            return Convert.ToDouble(value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Safely converts a value to decimal, handling JsonElement.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>Decimal value or null if conversion fails.</returns>
    public static decimal? ConvertToDecimal(object? value)
    {
        if (value == null) return null;

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var decValue))
                return decValue;
            if (element.ValueKind == JsonValueKind.String && decimal.TryParse(element.GetString(), out var parsedDecimal))
                return parsedDecimal;
            return null;
        }

        try
        {
            return Convert.ToDecimal(value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Safely converts a value to bool, handling JsonElement.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>Boolean value or null if conversion fails.</returns>
    public static bool? ConvertToBoolean(object? value)
    {
        if (value == null) return null;

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(element.GetString(), out var parsed) => parsed,
                _ => null
            };
        }

        try
        {
            return Convert.ToBoolean(value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Safely converts a value to List&lt;string&gt;, handling JsonElement arrays.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>List of strings or null if conversion fails.</returns>
    public static List<string>? ConvertToStringList(object? value)
    {
        if (value == null) return null;

        if (value is List<string> stringList)
            return stringList;

        if (value is IEnumerable<string> stringEnumerable)
            return stringEnumerable.ToList();

        if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray()
                .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.ToString())
                .Where(s => s != null)
                .Cast<string>()
                .ToList();
        }

        return null;
    }

    /// <summary>
    /// Safely converts a value to List&lt;int&gt;, handling JsonElement arrays.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>List of integers or null if conversion fails.</returns>
    public static List<int>? ConvertToIntList(object? value)
    {
        if (value == null) return null;

        if (value is List<int> intList)
            return intList;

        if (value is IEnumerable<int> intEnumerable)
            return intEnumerable.ToList();

        if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
        {
            var result = new List<int>();
            foreach (var e in element.EnumerateArray())
            {
                if (e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var intValue))
                    result.Add(intValue);
            }
            return result.Count > 0 ? result : null;
        }

        return null;
    }

    /// <summary>
    /// Safely converts a value to DateTime, handling JsonElement.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>DateTime value or null if conversion fails.</returns>
    public static DateTime? ConvertToDateTime(object? value)
    {
        if (value == null) return null;

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String && element.TryGetDateTime(out var dateValue))
                return dateValue;
            return null;
        }

        if (value is DateTime dt)
            return dt;

        if (value is DateTimeOffset dto)
            return dto.DateTime;

        if (value is string dateString && DateTime.TryParse(dateString, out var parsed))
            return parsed;

        return null;
    }

    /// <summary>
    /// Checks if the value is null or represents a JSON null.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value is null or JSON null.</returns>
    public static bool IsNullOrJsonNull(object? value)
    {
        if (value == null) return true;

        if (value is JsonElement element)
        {
            return element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined;
        }

        return false;
    }
}

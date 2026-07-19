using System.Text.Json;

namespace ConduitLLM.Providers.Helpers
{
    /// <summary>
    /// Utility class for converting <see cref="JsonElement"/> values to their corresponding .NET types.
    /// </summary>
    internal static class JsonElementConverter
    {
        /// <summary>
        /// Converts a JsonElement to its actual .NET value for proper serialization.
        /// </summary>
        /// <param name="element">The JsonElement to convert.</param>
        /// <returns>The converted value as a proper .NET type.</returns>
        internal static object? ConvertJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out var intValue))
                        return intValue;
                    if (element.TryGetInt64(out var longValue))
                        return longValue;
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                case JsonValueKind.Array:
                    return element.EnumerateArray()
                        .Select(e => ConvertJsonElement(e))
                        .ToList();
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object?>();
                    foreach (var property in element.EnumerateObject())
                    {
                        dict[property.Name] = ConvertJsonElement(property.Value);
                    }
                    return dict;
                default:
                    return element.ToString();
            }
        }
    }
}

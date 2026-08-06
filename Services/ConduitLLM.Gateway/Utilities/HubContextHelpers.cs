namespace ConduitLLM.Gateway.Utilities
{
    /// <summary>
    /// Shared helpers for reading SignalR hub connection context data.
    /// </summary>
    internal static class HubContextHelpers
    {
        /// <summary>
        /// Gets the correlation ID stored on the connection, creating and storing one if absent.
        /// </summary>
        public static string GetOrCreateCorrelationId(IDictionary<object, object?> items)
        {
            if (items.TryGetValue("CorrelationId", out var value) && value is string correlationId)
            {
                return correlationId;
            }

            correlationId = Guid.NewGuid().ToString();
            items["CorrelationId"] = correlationId;
            return correlationId;
        }

        /// <summary>
        /// Converts a connection context item to an int, tolerating long and string representations.
        /// </summary>
        public static int? ConvertToInt(object? value)
        {
            return value switch
            {
                int intValue => intValue,
                long longValue => (int)longValue,
                string stringValue when int.TryParse(stringValue, out var parsedValue) => parsedValue,
                _ => null
            };
        }
    }
}

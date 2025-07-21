using System;
using System.Text.Json.Nodes;

namespace ConduitLLM.Tests.TestHelpers
{
    /// <summary>
    /// Helper methods for working with JSON in tests.
    /// </summary>
    public static class JsonTestHelpers
    {
        /// <summary>
        /// Creates a JsonObject from a JSON string with proper error handling.
        /// </summary>
        /// <param name="json">The JSON string to parse</param>
        /// <returns>A JsonObject, never null</returns>
        /// <exception cref="InvalidOperationException">Thrown when JSON parsing fails</exception>
        public static JsonObject CreateJsonObject(string json)
        {
            try
            {
                var node = JsonNode.Parse(json);
                if (node is JsonObject jsonObject)
                {
                    return jsonObject;
                }
                
                throw new InvalidOperationException($"JSON did not parse to a JsonObject. Actual type: {node?.GetType().Name ?? "null"}");
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException($"Failed to parse JSON: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates function parameters for the common weather function used in tests.
        /// </summary>
        public static JsonObject CreateWeatherFunctionParameters()
        {
            return CreateJsonObject(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""location"": { 
                        ""type"": ""string"", 
                        ""description"": ""City name"" 
                    },
                    ""unit"": { 
                        ""type"": ""string"", 
                        ""enum"": [""celsius"", ""fahrenheit""] 
                    }
                },
                ""required"": [""location""]
            }");
        }

        /// <summary>
        /// Creates simple function parameters with just a location property.
        /// </summary>
        public static JsonObject CreateSimpleLocationParameters()
        {
            return CreateJsonObject(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""location"": { ""type"": ""string"" }
                }
            }");
        }
    }
}
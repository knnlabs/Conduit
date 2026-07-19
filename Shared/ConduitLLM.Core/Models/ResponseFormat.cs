using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Represents the format configuration for model responses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ResponseFormat class is used to specify how a model should format its output.
    /// Different providers may support different response format types.
    /// </para>
    /// <para>
    /// Common types include:
    /// - "text": Standard text response (default for most models)
    /// - "json_object": JSON format where the model will respond with valid JSON
    /// - Other provider-specific formats may be available
    /// </para>
    /// </remarks>
    public class ResponseFormat
    {
        /// <summary>
        /// Gets or sets the response format type.
        /// </summary>
        /// <remarks>
        /// Common values include "text" and "json_object", but may vary by provider.
        /// </remarks>
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        /// <summary>
        /// The JSON Schema definition, used when <see cref="Type"/> is <c>json_schema</c>.
        /// </summary>
        [JsonPropertyName("json_schema")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonSchemaFormat? JsonSchema { get; set; }

        /// <summary>
        /// Creates a new instance of the ResponseFormat class with default values.
        /// </summary>
        public ResponseFormat() { }

        /// <summary>
        /// Creates a new instance of the ResponseFormat class with the specified type.
        /// </summary>
        /// <param name="type">The response format type.</param>
        public ResponseFormat(string type)
        {
            Type = type;
        }

        /// <summary>
        /// Creates a new ResponseFormat configured for JSON output.
        /// </summary>
        /// <returns>A ResponseFormat with type set to "json_object".</returns>
        public static ResponseFormat Json()
        {
            return new ResponseFormat("json_object");
        }

        /// <summary>
        /// Creates a new ResponseFormat configured for plain text output.
        /// </summary>
        /// <returns>A ResponseFormat with type set to "text".</returns>
        public static ResponseFormat Text()
        {
            return new ResponseFormat("text");
        }

        /// <summary>
        /// Creates a new ResponseFormat configured for schema-constrained JSON output.
        /// </summary>
        /// <param name="name">The schema name.</param>
        /// <param name="schema">The JSON Schema document the output must conform to.</param>
        /// <param name="strict">Whether the provider must strictly enforce the schema.</param>
        /// <returns>A ResponseFormat with type set to "json_schema".</returns>
        public static ResponseFormat WithJsonSchema(string name, JsonElement schema, bool? strict = null)
        {
            return new ResponseFormat("json_schema")
            {
                JsonSchema = new JsonSchemaFormat { Name = name, Schema = schema, Strict = strict }
            };
        }
    }
}

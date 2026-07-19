using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// A JSON Schema definition for structured outputs, used with
    /// <see cref="ResponseFormat"/> when the type is <c>json_schema</c>.
    /// </summary>
    public class JsonSchemaFormat
    {
        /// <summary>
        /// The name of the schema (required by OpenAI-compatible structured outputs).
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// When true, the provider enforces strict adherence to the schema.
        /// </summary>
        [JsonPropertyName("strict")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Strict { get; set; }

        /// <summary>
        /// The JSON Schema document the output must conform to.
        /// </summary>
        [JsonPropertyName("schema")]
        public JsonElement Schema { get; set; }
    }
}

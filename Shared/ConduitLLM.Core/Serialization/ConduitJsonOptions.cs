using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Serialization
{
    /// <summary>
    /// Shared, immutable <see cref="JsonSerializerOptions"/> instances for common serialization shapes.
    /// Using these statics (instead of constructing options per call or per instance) lets
    /// System.Text.Json reuse its cached serializer metadata.
    /// </summary>
    public static class ConduitJsonOptions
    {
        /// <summary>
        /// Wire-format options: camelCase property naming and null values omitted when writing
        /// (<see cref="JsonIgnoreCondition.WhenWritingNull"/>). No additional converters.
        /// </summary>
        public static readonly JsonSerializerOptions Wire = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Compact options: camelCase property naming with non-indented output
        /// (<c>WriteIndented = false</c>). Nulls are written. No additional converters.
        /// </summary>
        public static readonly JsonSerializerOptions Compact = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        /// <summary>
        /// Compact wire-format options: camelCase property naming, non-indented output
        /// (<c>WriteIndented = false</c>), and null values omitted when writing
        /// (<see cref="JsonIgnoreCondition.WhenWritingNull"/>). No additional converters.
        /// </summary>
        public static readonly JsonSerializerOptions CompactWire = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}

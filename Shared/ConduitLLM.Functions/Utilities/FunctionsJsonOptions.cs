using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConduitLLM.Functions.Utilities
{
    /// <summary>
    /// Shared, immutable <see cref="JsonSerializerOptions"/> instances for ConduitLLM.Functions.
    /// Mirrors <c>ConduitLLM.Core.Serialization.ConduitJsonOptions</c>; this project sits below
    /// Core in the dependency graph (Functions &lt;- Configuration &lt;- Core) and cannot
    /// reference it. Using these statics lets System.Text.Json reuse its cached serializer
    /// metadata instead of rebuilding it per instance.
    /// </summary>
    public static class FunctionsJsonOptions
    {
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

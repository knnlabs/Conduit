using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models.Rerank
{
    /// <summary>
    /// A rerank request: score/order a set of documents against a query (Cohere-compatible shape).
    /// </summary>
    public class RerankRequest
    {
        /// <summary>The rerank model (alias) to use.</summary>
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        /// <summary>The search query.</summary>
        [JsonPropertyName("query")]
        public required string Query { get; set; }

        /// <summary>The documents to rerank.</summary>
        [JsonPropertyName("documents")]
        public required List<string> Documents { get; set; }

        /// <summary>Optional maximum number of results to return.</summary>
        [JsonPropertyName("top_n")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TopN { get; set; }

        /// <summary>Whether to echo the document text back in each result.</summary>
        [JsonPropertyName("return_documents")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? ReturnDocuments { get; set; }

        /// <summary>Provider-specific passthrough fields.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }
}

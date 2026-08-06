using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models.Rerank
{
    /// <summary>
    /// A rerank response: documents scored and ordered by relevance to the query.
    /// </summary>
    public class RerankResponse
    {
        /// <summary>The model that produced the ranking.</summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        /// <summary>The ranked results, highest relevance first.</summary>
        [JsonPropertyName("results")]
        public required List<RerankResult> Results { get; set; }

        /// <summary>Usage/cost information (search units are the billable unit for rerank).</summary>
        [JsonPropertyName("usage")]
        public Usage? Usage { get; set; }
    }

    /// <summary>
    /// A single reranked document.
    /// </summary>
    public class RerankResult
    {
        /// <summary>The index of the document in the original request list.</summary>
        [JsonPropertyName("index")]
        public required int Index { get; set; }

        /// <summary>The relevance score (higher is more relevant).</summary>
        [JsonPropertyName("relevance_score")]
        public required double RelevanceScore { get; set; }

        /// <summary>The document text, when <c>return_documents</c> was requested.</summary>
        [JsonPropertyName("document")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Document { get; set; }
    }
}

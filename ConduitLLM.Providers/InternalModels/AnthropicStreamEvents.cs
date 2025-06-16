using System.Text.Json.Serialization;

namespace ConduitLLM.Providers.InternalModels
{
    /// <summary>
    /// Base class for all Anthropic streaming events to enable deserialization
    /// in StreamHelper.cs ProcessSseStreamAsync method.
    /// </summary>
    internal class AnthropicMessageStreamEvent
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("index")]
        public int? Index { get; set; }

        [JsonPropertyName("delta")]
        public AnthropicStreamDelta? Delta { get; set; }
    }
}

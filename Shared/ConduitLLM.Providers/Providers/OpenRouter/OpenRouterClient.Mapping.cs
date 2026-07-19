using System.Text.Json;

using CoreModels = ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.OpenRouter
{
    public partial class OpenRouterClient
    {
        /// <summary>
        /// Per-mapping provider options (provider/plugins/transforms/models/route), parsed once from
        /// <c>ModelProviderMapping.ProviderOptions</c>. Null when none configured or the JSON is invalid.
        /// </summary>
        private readonly Dictionary<string, JsonElement>? _mappingOptions;

        /// <summary>
        /// Merges the per-mapping provider options into the outgoing request. Precedence:
        /// standard mapped parameters &gt; caller-supplied ExtensionData (merged by the base) &gt; mapping
        /// options — so a per-request choice always beats a per-mapping default, and neither can
        /// clobber a standard parameter. Whole-key merge (no deep merge of nested objects in v1).
        /// </summary>
        protected override object MapToOpenAIRequest(CoreModels.ChatCompletionRequest request)
        {
            var mapped = base.MapToOpenAIRequest(request);
            if (_mappingOptions is null || mapped is not IDictionary<string, object?> dict)
            {
                return mapped;
            }

            foreach (var (key, value) in _mappingOptions)
            {
                if (!dict.ContainsKey(key))
                {
                    dict[key] = value;
                }
            }

            return mapped;
        }

        private Dictionary<string, JsonElement>? ParseProviderOptions(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                return parsed is { Count: > 0 } ? parsed : null;
            }
            catch (JsonException ex)
            {
                Logger.LogWarning(ex, "Ignoring malformed OpenRouter ProviderOptions JSON for a mapping.");
                return null;
            }
        }
    }
}

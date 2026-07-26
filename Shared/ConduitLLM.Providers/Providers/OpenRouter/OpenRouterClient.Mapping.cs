using System.Text.Json;
using System.Text.Json.Nodes;

using CoreModels = ConduitLLM.Core.Models;
using ConduitLLM.Providers.OpenAI;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.OpenRouter
{
    public partial class OpenRouterClient
    {
        protected override void ValidateRequest<TRequest>(TRequest request, string operationName)
        {
            base.ValidateRequest(request, operationName);
            if (request is CoreModels.ChatCompletionRequest chatRequest)
            {
                OpenRouterMultimodalValidator.Validate(chatRequest);
            }
        }

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
            if (mapped is not IDictionary<string, object?> dict)
            {
                return mapped;
            }

            ApplyPromptCachingIntent(dict, request);

            // Caller session IDs are forwarded verbatim. Inferred affinity is already an opaque
            // HMAC and therefore does not disclose prompt content to OpenRouter.
            if (!dict.ContainsKey("session_id"))
            {
                var sessionId = request.SessionId ?? request.RoutingAffinityKey;
                if (!string.IsNullOrWhiteSpace(sessionId)) dict["session_id"] = sessionId;
            }

            if (_mappingOptions is not null)
            {
                foreach (var (key, value) in _mappingOptions)
                {
                    if (!dict.ContainsKey(key))
                    {
                        dict[key] = value;
                    }
                }
            }

            return mapped;
        }

        private static void ApplyPromptCachingIntent(
            IDictionary<string, object?> mapped,
            CoreModels.ChatCompletionRequest request)
        {
            var intent = request.PromptCachingIntent;
            if (intent is null || mapped.ContainsKey("cache_control")) return;

            var directive = new Dictionary<string, object?> { ["type"] = "ephemeral" };
            if (!string.IsNullOrWhiteSpace(intent.Ttl)) directive["ttl"] = intent.Ttl;

            if (intent.Strategy == CoreModels.PromptCachingStrategy.Automatic)
            {
                if (mapped.TryGetValue("messages", out var automaticMessages) &&
                    automaticMessages is List<OpenAIMessage> existingMessages &&
                    existingMessages.Sum(message => CountCacheControls(message.Content)) > 0) return;
                mapped["cache_control"] = directive;
                return;
            }

            if (mapped.TryGetValue("messages", out var messagesObject) &&
                messagesObject is List<OpenAIMessage> messages)
            {
                var targets = ResolveTargets(request.Messages, intent.InjectionPoints);
                var added = messages.Sum(message => CountCacheControls(message.Content));
                foreach (var index in targets)
                {
                    if (added >= CoreModels.PromptCachingConstants.MaxExplicitBreakpoints) break;
                    if (TryAddCacheControl(messages[index].Content, directive, out var content))
                    {
                        messages[index] = messages[index] with { Content = content };
                        added++;
                    }
                }
            }
        }

        private static IReadOnlyList<int> ResolveTargets(
            IReadOnlyList<CoreModels.Message> messages,
            IReadOnlyList<CoreModels.CacheInjectionPoint> points)
        {
            var result = new List<int>();
            var seen = new HashSet<int>();
            foreach (var point in points)
            {
                var candidates = Enumerable.Range(0, messages.Count)
                    .Where(i => point.Role is null || messages[i].Role.Equals(point.Role, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (point.Index.HasValue)
                {
                    var index = point.Index.Value < 0 ? candidates.Count + point.Index.Value : point.Index.Value;
                    if (index >= 0 && index < candidates.Count && seen.Add(candidates[index])) result.Add(candidates[index]);
                }
                else
                {
                    foreach (var candidate in candidates)
                        if (seen.Add(candidate)) result.Add(candidate);
                }
            }
            return result;
        }

        private static bool TryAddCacheControl(
            object? content,
            IReadOnlyDictionary<string, object?> directive,
            out object? updated)
        {
            updated = content;
            JsonArray blocks;
            if (content is string text)
            {
                if (string.IsNullOrWhiteSpace(text)) return false;
                blocks = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text });
            }
            else
            {
                try
                {
                    blocks = JsonNode.Parse(JsonSerializer.Serialize(content)) as JsonArray ?? new JsonArray();
                }
                catch (Exception ex) when (ex is JsonException or NotSupportedException)
                {
                    return false;
                }
            }

            var last = blocks.LastOrDefault() as JsonObject;
            if (last is null || last.ContainsKey("cache_control")) return false;
            if (last["type"]?.GetValue<string>() == "text" && string.IsNullOrWhiteSpace(last["text"]?.GetValue<string>())) return false;

            last["cache_control"] = JsonSerializer.SerializeToNode(directive);
            updated = blocks;
            return true;
        }

        private static int CountCacheControls(object? content)
        {
            if (content is null || content is string) return 0;
            try
            {
                return (JsonNode.Parse(JsonSerializer.Serialize(content)) as JsonArray)?
                    .Count(node => node is JsonObject block && block.ContainsKey("cache_control")) ?? 0;
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                return 0;
            }
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

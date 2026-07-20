using System.Text.Json;
using System.Text.Json.Nodes;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Providers.OpenAI;

public partial class OpenAIClient
{
    protected override object MapToOpenAIRequest(ChatCompletionRequest request)
    {
        var mapped = base.MapToOpenAIRequest(request);
        if (mapped is not IDictionary<string, object?> dict) return mapped;

        var intent = request.PromptCachingIntent;
        if (intent is null) return mapped;

        // Caller controls always win, including controls passed through JsonExtensionData.
        if (!dict.ContainsKey("prompt_cache_key") && !string.IsNullOrWhiteSpace(intent.PromptCacheKey))
            dict["prompt_cache_key"] = intent.PromptCacheKey;

        // Older model families reject the v5.6 controls; keep their provider-managed behavior.
        if (!OpenAIPromptCachingAdapter.IsExplicitModel(ProviderModelId)) return mapped;

        if (!dict.ContainsKey("prompt_cache_options"))
        {
            var options = new Dictionary<string, object?>
            {
                ["mode"] = intent.Strategy == PromptCachingStrategy.Explicit ? "explicit" : "implicit"
            };
            if (!string.IsNullOrWhiteSpace(intent.Ttl)) options["ttl"] = intent.Ttl;
            dict["prompt_cache_options"] = options;
        }

        if (intent.Strategy == PromptCachingStrategy.Explicit &&
            dict.TryGetValue("messages", out var messagesObject) &&
            messagesObject is List<OpenAIMessage> messages)
        {
            var targets = ResolveTargets(request.Messages, intent.InjectionPoints);
            var added = messages.Sum(message => CountBreakpoints(message.Content));
            foreach (var index in targets)
            {
                if (added >= PromptCachingConstants.MaxExplicitBreakpoints) break;
                if (TryAddBreakpoint(messages[index].Content, out var content))
                {
                    messages[index] = messages[index] with { Content = content };
                    added++;
                }
            }
        }
        return mapped;
    }

    private static IReadOnlyList<int> ResolveTargets(IReadOnlyList<Message> messages, IReadOnlyList<CacheInjectionPoint> points)
    {
        var result = new List<int>();
        var seen = new HashSet<int>();
        foreach (var point in points)
        {
            var candidates = Enumerable.Range(0, messages.Count)
                .Where(i => point.Role is null || messages[i].Role.Equals(point.Role, StringComparison.OrdinalIgnoreCase)).ToList();
            if (point.Index is int requested)
            {
                var index = requested < 0 ? candidates.Count + requested : requested;
                if (index >= 0 && index < candidates.Count && seen.Add(candidates[index])) result.Add(candidates[index]);
            }
            else foreach (var candidate in candidates) if (seen.Add(candidate)) result.Add(candidate);
        }
        return result;
    }

    private static bool TryAddBreakpoint(object? content, out object? updated)
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
            try { blocks = JsonNode.Parse(JsonSerializer.Serialize(content)) as JsonArray ?? new JsonArray(); }
            catch (Exception ex) when (ex is JsonException or NotSupportedException) { return false; }
        }
        var last = blocks.LastOrDefault() as JsonObject;
        if (last is null || last.ContainsKey("prompt_cache_breakpoint")) return false;
        last["prompt_cache_breakpoint"] = new JsonObject { ["mode"] = "explicit" };
        updated = blocks;
        return true;
    }

    private static int CountBreakpoints(object? content)
    {
        if (content is null || content is string) return 0;
        try
        {
            return (JsonNode.Parse(JsonSerializer.Serialize(content)) as JsonArray)?
                .Count(node => node is JsonObject block && block.ContainsKey("prompt_cache_breakpoint")) ?? 0;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException) { return 0; }
    }
}

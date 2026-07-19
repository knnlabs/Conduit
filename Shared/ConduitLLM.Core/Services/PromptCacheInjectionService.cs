using System.Text.Json;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services;

/// <summary>
/// Service that injects cache_control directives into chat completion request messages
/// based on a <see cref="PromptCachingConfig"/>.
/// </summary>
public static class PromptCacheInjectionService
{
    /// <summary>
    /// Maximum number of cache_control breakpoints that Anthropic allows per request.
    /// </summary>
    private const int MaxCachedBlocks = 4;

    /// <summary>
    /// Injects cache_control directives into the request messages in-place.
    /// </summary>
    /// <param name="request">The chat completion request to modify.</param>
    /// <param name="config">The caching configuration.</param>
    public static void InjectCacheControl(ChatCompletionRequest request, PromptCachingConfig config)
    {
        if (!config.AutoInjectEnabled || config.InjectionPoints.Count == 0)
            return;

        var injectedCount = 0;

        foreach (var point in config.InjectionPoints)
        {
            if (injectedCount >= MaxCachedBlocks)
                break;

            var targetMessages = FindTargetMessages(request.Messages, point);

            foreach (var message in targetMessages)
            {
                if (injectedCount >= MaxCachedBlocks)
                    break;

                InjectCacheControlOnMessage(message);
                injectedCount++;
            }
        }
    }

    /// <summary>
    /// Finds messages matching the injection point criteria.
    /// </summary>
    private static List<Message> FindTargetMessages(List<Message> messages, CacheInjectionPoint point)
    {
        // Filter by role if specified
        var candidates = point.Role != null
            ? messages.Where(m => string.Equals(m.Role, point.Role, StringComparison.OrdinalIgnoreCase)).ToList()
            : messages.ToList();

        if (candidates.Count == 0)
            return candidates;

        // Apply index selection if specified
        if (point.Index.HasValue)
        {
            var idx = point.Index.Value;

            // Resolve negative indices
            if (idx < 0)
                idx = candidates.Count + idx;

            if (idx >= 0 && idx < candidates.Count)
                return new List<Message> { candidates[idx] };

            return new List<Message>();
        }

        return candidates;
    }

    /// <summary>
    /// Adds cache_control to the last content block of a message.
    /// If content is a plain string, converts it to a content array first.
    /// </summary>
    private static void InjectCacheControlOnMessage(Message message)
    {
        var cacheControl = new Dictionary<string, string> { ["type"] = "ephemeral" };

        if (message.Content == null)
            return;

        if (message.Content is string textContent)
        {
            // Convert string to content array with cache_control
            message.Content = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["type"] = "text",
                    ["text"] = textContent,
                    ["cache_control"] = cacheControl
                }
            };
            return;
        }

        if (message.Content is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            // Convert to mutable list, add cache_control to last element
            var elements = new List<Dictionary<string, object?>>();
            foreach (var element in jsonElement.EnumerateArray())
            {
                var dict = new Dictionary<string, object?>();
                foreach (var prop in element.EnumerateObject())
                {
                    dict[prop.Name] = ConvertJsonElement(prop.Value);
                }
                elements.Add(dict);
            }

            if (elements.Count > 0)
            {
                elements[^1]["cache_control"] = cacheControl;
            }

            message.Content = elements.Cast<object>().ToList();
            return;
        }

        // If content is already a List<object>, add cache_control to the last element
        if (message.Content is IList<object> contentList && contentList.Count > 0)
        {
            var lastItem = contentList[^1];
            if (lastItem is Dictionary<string, object?> dict)
            {
                dict["cache_control"] = cacheControl;
            }
        }
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out var i) => i,
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
            _ => element.ToString()
        };
    }
}

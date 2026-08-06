using System.Globalization;
using System.Text.Json;

using ConduitLLM.Admin.DTOs;

namespace ConduitLLM.Admin.Services;

/// <summary>Single source of truth for typed global-setting editing and validation.</summary>
public static class GlobalSettingDefinitionRegistry
{
    public const string ProtectedWebAdminKey = "WebAdmin_VirtualKey";

    public static IReadOnlyList<GlobalSettingDefinitionDto> All { get; } =
    [
        Integer("Agentic.MaxIterations", "Maximum agentic iterations",
            "Maximum tool-execution loop iterations per request.", "Agentic", "5", 1, 100),
        Integer("Agentic.MinIterations", "Minimum agentic iterations",
            "Minimum tool-execution loop iterations per request.", "Agentic", "1", 1, 100),
        Boolean("Agentic.DefaultEnabled", "Agentic mode by default",
            "Enables agentic orchestration when a request does not override it.", "Agentic", "true"),
        Boolean("Functions.DiscoveryCacheEnabled", "Function discovery cache",
            "Caches function tool definitions using each function configuration's TTL.", "Functions", "false",
            "/functions/configurations"),
        Json("Routing.Defaults", "Structured routing defaults",
            "Default provider-aware routing policy. Edit through the structured controls below.",
            "Routing", "{}", "/settings"),
        Boolean("Routing.Chat.Enabled", "Provider-aware chat routing",
            "Emergency switch for provider-aware chat routing.", "Routing", "true", "/settings"),
        Json("PromptCaching.Config", "Prompt caching policy",
            "Provider-aware prompt caching policy.", "Feature-owned", "{}", "/prompt-caching", true),
        Boolean("IpFilter:Enabled", "IP filtering",
            "Enables the IP filtering policy.", "Feature-owned", "false", "/ip-filtering", true),
        Boolean("IpFilter:DefaultAllow", "Default IP decision",
            "Allows requests that do not match an IP rule.", "Feature-owned", "true", "/ip-filtering", true),
        Boolean("IpFilter:BypassForAdminUi", "Admin UI IP bypass",
            "Allows the Admin UI to bypass IP filtering.", "Feature-owned", "true", "/ip-filtering", true),
        Json("IpFilter:ExcludedEndpoints", "IP filter exclusions",
            "Endpoint patterns excluded from IP filtering.", "Feature-owned", "[]", "/ip-filtering", true),
        Boolean("MediaCleanup.Enabled", "Media cleanup",
            "Enables scheduled media cleanup.", "Feature-owned", "false", "/media-assets/cleanup-status", true),
        Integer("MediaCleanup.SimpleRetentionDays", "Media retention days",
            "Default media retention period.", "Feature-owned", "30", 1, 3650,
            "/media-assets/cleanup-status", true),
    ];

    private static readonly IReadOnlyDictionary<string, GlobalSettingDefinitionDto> ByKey =
        All.ToDictionary(item => item.Key, StringComparer.Ordinal);

    public static GlobalSettingDefinitionDto? Find(string key) =>
        ByKey.TryGetValue(key, out var definition) ? definition : null;

    public static void ValidateValue(string key, string value)
    {
        var definition = Find(key);
        if (definition == null)
        {
            return;
        }

        switch (definition.Type)
        {
            case "boolean":
                if (!bool.TryParse(value, out _))
                {
                    throw new ArgumentException($"{key} must be true or false.", nameof(value));
                }
                break;
            case "integer":
                if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                {
                    throw new ArgumentException($"{key} must be an integer.", nameof(value));
                }
                ValidateRange(key, integer, definition);
                break;
            case "number":
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    throw new ArgumentException($"{key} must be a number.", nameof(value));
                }
                ValidateRange(key, number, definition);
                break;
            case "json":
                try
                {
                    using var _ = JsonDocument.Parse(value);
                }
                catch (JsonException ex)
                {
                    throw new ArgumentException($"{key} must contain valid JSON.", nameof(value), ex);
                }
                break;
        }

        if (definition.MaxLength.HasValue && value.Length > definition.MaxLength)
        {
            throw new ArgumentException(
                $"{key} cannot exceed {definition.MaxLength.Value} characters.",
                nameof(value));
        }
    }

    private static void ValidateRange(
        string key,
        decimal value,
        GlobalSettingDefinitionDto definition)
    {
        if (definition.Minimum.HasValue && value < definition.Minimum.Value
            || definition.Maximum.HasValue && value > definition.Maximum.Value)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"{key} must be between {definition.Minimum} and {definition.Maximum}.");
        }
    }

    private static GlobalSettingDefinitionDto Boolean(
        string key,
        string name,
        string description,
        string category,
        string defaultValue,
        string? featureRoute = null,
        bool featureOwned = false) =>
        new()
        {
            Key = key,
            DisplayName = name,
            Description = description,
            Type = "boolean",
            Category = category,
            DefaultValue = defaultValue,
            FeatureRoute = featureRoute,
            IsFeatureOwned = featureOwned
        };

    private static GlobalSettingDefinitionDto Integer(
        string key,
        string name,
        string description,
        string category,
        string defaultValue,
        decimal minimum,
        decimal maximum,
        string? featureRoute = null,
        bool featureOwned = false) =>
        new()
        {
            Key = key,
            DisplayName = name,
            Description = description,
            Type = "integer",
            Category = category,
            DefaultValue = defaultValue,
            Minimum = minimum,
            Maximum = maximum,
            FeatureRoute = featureRoute,
            IsFeatureOwned = featureOwned
        };

    private static GlobalSettingDefinitionDto Json(
        string key,
        string name,
        string description,
        string category,
        string defaultValue,
        string? featureRoute = null,
        bool featureOwned = false) =>
        new()
        {
            Key = key,
            DisplayName = name,
            Description = description,
            Type = "json",
            Category = category,
            DefaultValue = defaultValue,
            FeatureRoute = featureRoute,
            IsFeatureOwned = featureOwned
        };
}

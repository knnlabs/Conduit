using System.Text.RegularExpressions;

namespace ConduitLLM.Core.Utilities;

/// <summary>Removes inline media and signed-query secrets from diagnostic text.</summary>
public static partial class SensitiveDataRedactor
{
    [GeneratedRegex(@"data:([a-z0-9.+-]+/[a-z0-9.+-]+);base64,[a-z0-9+/=_-]+", RegexOptions.IgnoreCase)]
    private static partial Regex DataUrlPattern();

    [GeneratedRegex(@"https://[^\s""'<>]+", RegexOptions.IgnoreCase)]
    private static partial Regex HttpsUrlPattern();

    [GeneratedRegex(@"(?<=[:""])[a-z0-9+/=_-]{128,}(?=[""\s,}])", RegexOptions.IgnoreCase)]
    private static partial Regex LongBase64Pattern();

    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        var redacted = DataUrlPattern().Replace(value, "data:$1;base64,[REDACTED]");
        redacted = LongBase64Pattern().Replace(redacted, "[REDACTED]");
        return HttpsUrlPattern().Replace(redacted, match =>
        {
            if (!Uri.TryCreate(match.Value, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Query))
                return match.Value;
            return uri.GetLeftPart(UriPartial.Path) + "?[REDACTED]";
        });
    }
}

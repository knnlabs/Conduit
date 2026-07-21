namespace ConduitLLM.Gateway.Endpoints;

/// <summary>Virtual-key request context accessors for Gateway Minimal APIs.</summary>
public static class GatewayHttpContextExtensions
{
    public static int? GetVirtualKeyId(this HttpContext context)
    {
        if (context.Items.TryGetValue("VirtualKeyId", out var value) && value is int id)
        {
            return id;
        }

        return int.TryParse(context.User.FindFirst("VirtualKeyId")?.Value, out var parsed)
            ? parsed
            : null;
    }

    public static string? GetVirtualKey(this HttpContext context)
    {
        if (context.Items.TryGetValue("VirtualKey", out var value)
            && value is string key
            && !string.IsNullOrEmpty(key))
        {
            return key;
        }

        var claim = context.User.FindFirst("VirtualKey")?.Value;
        return string.IsNullOrEmpty(claim) ? null : claim;
    }
}

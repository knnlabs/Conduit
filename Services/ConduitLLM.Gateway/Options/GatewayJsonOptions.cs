using System.Text.Json;
using System.Text.Json.Serialization;

using ConduitLLM.Configuration.Serialization;
using ConduitLLM.Core.Converters;
using ConduitLLM.Core.Serialization;
using ConduitLLM.Gateway.Serialization;

namespace ConduitLLM.Gateway.Options;

/// <summary>
/// Owns the Gateway wire-format serializer configuration.
/// </summary>
public static class GatewayJsonOptions
{
    public static void Configure(JsonSerializerOptions options)
    {
        options.TypeInfoResolverChain.Insert(0, GatewayHttpJsonContext.Default);
        options.TypeInfoResolverChain.Insert(1, CoreHttpJsonContext.Default);
        options.TypeInfoResolverChain.Insert(2, ConfigurationHttpJsonContext.Default);
        if (options.TypeInfoResolverChain.All(static resolver =>
                resolver is not System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver))
        {
            options.TypeInfoResolverChain.Add(
                new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver());
        }

        options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.SnakeCaseLower,
                allowIntegerValues: false));
        options.Converters.Add(new UtcDateTimeConverter());
        options.Converters.Add(new NullableUtcDateTimeConverter());
    }

    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions();
        Configure(options);
        return options;
    }
}

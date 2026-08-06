using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Models.ModelAuthors;
using ConduitLLM.Admin.Models.Models;
using ConduitLLM.Admin.Models.ModelSeries;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.IpFilter;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Functions.DTOs;

using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace ConduitLLM.Admin.Endpoints;

/// <summary>
/// Registers the request DTOs that use RFC 7386 JSON Merge Patch semantics and records the
/// original patch document alongside the normally deserialized DTO.
/// </summary>
internal sealed class JsonMergePatchRequestConverterFactory : JsonConverterFactory
{
    private static readonly HashSet<Type> PatchTypes =
    [
        typeof(UpdateFunctionConfigurationRequest),
        typeof(UpdateFunctionCostDto),
        typeof(UpdateFunctionCredentialRequest),
        typeof(UpdateGlobalSettingDto),
        typeof(UpdateIpFilterDto),
        typeof(UpdateMediaRetentionPolicyRequest),
        typeof(UpdateModelAuthorDto),
        typeof(UpdateModelCostDto),
        typeof(UpdateModelDto),
        typeof(ModelIdentifierRequestDto),
        typeof(UpdateModelProviderMappingDto),
        typeof(UpdateModelSeriesDto),
        typeof(UpdateNotificationDto),
        typeof(UpdateProviderRequest),
        typeof(UpdateKeyRequest),
        typeof(UpdateProviderToolDto),
        typeof(UpdateVirtualKeyGroupRequestDto),
        typeof(UpdateVirtualKeyRequestDto)
    ];

    public override bool CanConvert(Type typeToConvert) => PatchTypes.Contains(typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(JsonMergePatchRequestConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType, options)!;
    }
}

/// <summary>
/// Runtime-only request wrapper that keeps merge-patch parsing separate from the normal JSON
/// options used by OpenAPI schema generation.
/// </summary>
internal sealed class JsonMergePatch<T>
    where T : class
{
    private JsonMergePatch(T value)
    {
        Value = value;
    }

    public T Value { get; }

    public static async ValueTask<JsonMergePatch<T>> BindAsync(
        HttpContext context,
        ParameterInfo parameter)
    {
        var contentType = context.Request.GetTypedHeaders().ContentType?.MediaType.Value;
        if (!string.Equals(
                contentType,
                JsonMergePatchRouteExtensions.MediaType,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new BadHttpRequestException(
                $"PATCH requests require Content-Type: {JsonMergePatchRouteExtensions.MediaType}.",
                StatusCodes.Status415UnsupportedMediaType);
        }

        var applicationOptions = context.RequestServices
            .GetRequiredService<IOptions<HttpJsonOptions>>()
            .Value
            .SerializerOptions;
        var mergePatchOptions = new JsonSerializerOptions(applicationOptions);
        mergePatchOptions.Converters.Add(new JsonMergePatchRequestConverterFactory());

        try
        {
            var value = await JsonSerializer.DeserializeAsync<T>(
                context.Request.Body,
                mergePatchOptions,
                context.RequestAborted);
            return new JsonMergePatch<T>(
                value ?? throw new BadHttpRequestException(
                    "A JSON Merge Patch request body is required."));
        }
        catch (JsonException exception)
        {
            throw new BadHttpRequestException(
                "The JSON Merge Patch request body is invalid.",
                StatusCodes.Status400BadRequest,
                exception);
        }
    }
}

internal sealed class JsonMergePatchRequestConverter<T> : JsonConverter<T>
    where T : class
{
    private readonly JsonSerializerOptions _innerOptions;
    private readonly Dictionary<string, string> _jsonToClrNames;

    public JsonMergePatchRequestConverter(JsonSerializerOptions options)
    {
        _innerOptions = new JsonSerializerOptions(options);
        for (var index = _innerOptions.Converters.Count - 1; index >= 0; index--)
        {
            if (_innerOptions.Converters[index] is JsonMergePatchRequestConverterFactory)
            {
                _innerOptions.Converters.RemoveAt(index);
            }
        }

        var comparer = options.PropertyNameCaseInsensitive
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        _jsonToClrNames = typeof(T)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(IsPatchableProperty)
            .ToDictionary(
                property => GetJsonPropertyName(property, options),
                property => property.Name,
                comparer);
    }

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A JSON Merge Patch request body must be a JSON object.");
        }

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!_jsonToClrNames.ContainsKey(property.Name))
            {
                throw new JsonException($"The merge-patch property '{property.Name}' is not writable.");
            }
        }

        var value = document.RootElement.Deserialize<T>(_innerOptions)
            ?? throw new JsonException("The JSON Merge Patch request body cannot be null.");
        JsonMergePatchState.Attach(
            value,
            document.RootElement.Clone(),
            _innerOptions,
            _jsonToClrNames);
        return value;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, _innerOptions);

    private static bool IsPatchableProperty(PropertyInfo property)
    {
        if (!property.CanWrite || property.GetIndexParameters().Length != 0)
        {
            return false;
        }

        var ignore = property.GetCustomAttribute<JsonIgnoreAttribute>();
        return ignore is null || ignore.Condition != JsonIgnoreCondition.Always;
    }

    private static string GetJsonPropertyName(PropertyInfo property, JsonSerializerOptions options) =>
        property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
        ?? options.PropertyNamingPolicy?.ConvertName(property.Name)
        ?? property.Name;
}

/// <summary>
/// Accessors for the original JSON Merge Patch document associated with a deserialized update DTO.
/// </summary>
internal static class JsonMergePatchState
{
    private static readonly ConditionalWeakTable<object, PatchState> States = new();

    public static void Attach(
        object request,
        JsonElement document,
        JsonSerializerOptions serializerOptions,
        IReadOnlyDictionary<string, string> jsonToClrNames)
    {
        States.Remove(request);
        States.Add(request, new PatchState(document, serializerOptions, jsonToClrNames));
    }

    public static bool IsDefined<TRequest>(this TRequest request, string clrPropertyName)
        where TRequest : class
    {
        if (States.TryGetValue(request, out var state))
        {
            return state.TryGetProperty(clrPropertyName, out _);
        }

        var property = typeof(TRequest).GetProperty(clrPropertyName);
        return property?.GetValue(request) is not null;
    }

    public static bool TryGetPatchedProperty<TRequest, TValue>(
        this TRequest request,
        string clrPropertyName,
        TValue currentValue,
        out TValue patchedValue)
        where TRequest : class
    {
        if (!States.TryGetValue(request, out var state))
        {
            var property = typeof(TRequest).GetProperty(clrPropertyName)
                ?? throw new ArgumentException(
                    $"Property '{clrPropertyName}' does not exist on {typeof(TRequest).Name}.",
                    nameof(clrPropertyName));
            var value = property.GetValue(request);
            if (value is null)
            {
                patchedValue = currentValue;
                return false;
            }

            patchedValue = (TValue)value;
            return true;
        }

        if (!state.TryGetProperty(clrPropertyName, out var patch))
        {
            patchedValue = currentValue;
            return false;
        }

        if (patch.ValueKind == JsonValueKind.Null &&
            typeof(TValue).IsValueType &&
            Nullable.GetUnderlyingType(typeof(TValue)) is null)
        {
            throw new BadHttpRequestException(
                $"Property '{state.GetJsonName(clrPropertyName)}' cannot be null.");
        }

        var targetNode = JsonSerializer.SerializeToNode(currentValue, state.SerializerOptions);
        var patchNode = JsonNode.Parse(patch.GetRawText());
        var merged = Apply(targetNode, patchNode);
        patchedValue = merged is null
            ? default!
            : merged.Deserialize<TValue>(state.SerializerOptions)!;
        return true;
    }

    private static JsonNode? Apply(JsonNode? target, JsonNode? patch)
    {
        if (patch is not JsonObject patchObject)
        {
            return patch?.DeepClone();
        }

        var targetObject = target as JsonObject ?? [];
        foreach (var property in patchObject)
        {
            if (property.Value is null)
            {
                targetObject.Remove(property.Key);
                continue;
            }

            targetObject[property.Key] = Apply(
                targetObject[property.Key]?.DeepClone(),
                property.Value);
        }

        return targetObject;
    }

    private sealed class PatchState
    {
        private readonly JsonElement _document;
        private readonly bool _propertyNameCaseInsensitive;
        private readonly Dictionary<string, string> _clrToJsonNames;

        public PatchState(
            JsonElement document,
            JsonSerializerOptions serializerOptions,
            IReadOnlyDictionary<string, string> jsonToClrNames)
        {
            _document = document;
            _propertyNameCaseInsensitive = serializerOptions.PropertyNameCaseInsensitive;
            _clrToJsonNames = jsonToClrNames.ToDictionary(
                pair => pair.Value,
                pair => pair.Key,
                StringComparer.Ordinal);
            SerializerOptions = serializerOptions;
        }

        public JsonSerializerOptions SerializerOptions { get; }

        public bool TryGetProperty(string clrPropertyName, out JsonElement value)
        {
            var jsonName = GetJsonName(clrPropertyName);
            foreach (var property in _document.EnumerateObject())
            {
                if (string.Equals(
                        property.Name,
                        jsonName,
                        _propertyNameCaseInsensitive
                            ? StringComparison.OrdinalIgnoreCase
                            : StringComparison.Ordinal))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public string GetJsonName(string clrPropertyName) =>
            _clrToJsonNames.TryGetValue(clrPropertyName, out var jsonName)
                ? jsonName
                : throw new ArgumentException(
                    $"Property '{clrPropertyName}' is not writable in this merge-patch document.",
                    nameof(clrPropertyName));
    }
}

/// <summary>Requires the RFC 7386 media type for an Admin PATCH operation.</summary>
internal sealed class JsonMergePatchContentTypeEndpointFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var contentType = context.HttpContext.Request.GetTypedHeaders().ContentType?.MediaType.Value;
        if (string.Equals(
                contentType,
                JsonMergePatchRouteExtensions.MediaType,
                StringComparison.OrdinalIgnoreCase))
        {
            return next(context);
        }

        return ValueTask.FromResult<object?>(Results.Problem(
            statusCode: StatusCodes.Status415UnsupportedMediaType,
            title: "Unsupported Media Type",
            detail: $"PATCH requests require Content-Type: {JsonMergePatchRouteExtensions.MediaType}.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "unsupported_media_type",
                ["traceId"] = context.HttpContext.TraceIdentifier
            }));
    }
}

internal static class JsonMergePatchRouteExtensions
{
    public const string MediaType = "application/merge-patch+json";

    public static RouteHandlerBuilder AcceptsJsonMergePatch<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : class =>
        builder
            .WithMetadata(new AcceptsMetadata([MediaType], typeof(TRequest), isOptional: false))
            .AddEndpointFilter<JsonMergePatchContentTypeEndpointFilter>();
}

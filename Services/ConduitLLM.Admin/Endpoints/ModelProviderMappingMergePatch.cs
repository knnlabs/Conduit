using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Functions.Utilities;

namespace ConduitLLM.Admin.Endpoints;

internal static class ModelProviderMappingMergePatch
{
    public static Dictionary<string, System.Text.Json.JsonElement>? GetEffectiveProviderOptions(
        UpdateModelProviderMappingDto request,
        ModelProviderMapping mapping)
    {
        JsonMergePatchState.TryGetPatchedProperty(
            request,
            nameof(request.ProviderOptions),
            StructuredJson.ParseObject(mapping.ProviderOptions),
            out Dictionary<string, System.Text.Json.JsonElement>? providerOptions);
        return providerOptions;
    }

    public static void Apply(UpdateModelProviderMappingDto request, ModelProviderMapping mapping)
    {
        if (JsonMergePatchState.TryGetPatchedProperty(
                request,
                nameof(request.ModelAlias),
                mapping.ModelAlias,
                out var modelAlias))
        {
            mapping.ModelAlias = RequireText(modelAlias, "modelAlias");
        }
        if (JsonMergePatchState.TryGetPatchedProperty(
                request,
                nameof(request.ProviderModelId),
                mapping.ProviderModelId,
                out var providerModelId))
        {
            mapping.ProviderModelId = RequireText(providerModelId, "providerModelId");
        }
        if (JsonMergePatchState.TryGetPatchedProperty(
                request,
                nameof(request.ProviderId),
                mapping.ProviderId,
                out var providerId))
        {
            mapping.ProviderId = providerId;
        }
        if (JsonMergePatchState.TryGetPatchedProperty(
                request,
                nameof(request.ModelProviderTypeAssociationId),
                mapping.ModelProviderTypeAssociationId,
                out var associationId))
        {
            mapping.ModelProviderTypeAssociationId = associationId;
        }
        if (JsonMergePatchState.TryGetPatchedProperty(
                request,
                nameof(request.IsEnabled),
                mapping.IsEnabled,
                out var isEnabled))
        {
            mapping.IsEnabled = isEnabled;
        }
        if (JsonMergePatchState.IsDefined(request, nameof(request.ProviderOptions)))
        {
            mapping.ProviderOptions = StructuredJson.SerializeObject(
                GetEffectiveProviderOptions(request, mapping));
        }
        if (JsonMergePatchState.TryGetPatchedProperty(
                request,
                nameof(request.Priority),
                mapping.RoutingPriority,
                out var priority))
        {
            mapping.RoutingPriority = priority;
        }
        if (JsonMergePatchState.TryGetPatchedProperty(
                request,
                nameof(request.Weight),
                mapping.RoutingWeight,
                out var weight))
        {
            mapping.RoutingWeight = weight;
        }
        mapping.UpdatedAt = DateTime.UtcNow;
    }

    private static string RequireText(string? value, string propertyName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{propertyName} cannot be null or empty.")
            : value;
}

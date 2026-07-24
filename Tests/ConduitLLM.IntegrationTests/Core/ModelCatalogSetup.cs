using Microsoft.Extensions.Logging;

namespace ConduitLLM.IntegrationTests.Core;

/// <summary>
/// Resolves the ModelProviderTypeAssociation (model catalog identifier) that the v1/admin API
/// requires before a model provider mapping or model cost can be created. Associations come
/// from the bundled model catalog seeded at Admin startup; this helper resolves them via the
/// bulk mapping preview endpoint rather than creating catalog entries.
/// </summary>
public static class ModelCatalogSetup
{
    public static async Task<int> ResolveAssociationAsync(
        ConduitApiClient apiClient,
        int providerId,
        string modelAlias,
        string providerModelId,
        ILogger logger)
    {
        var previewResponse = await apiClient.AdminPostAsync<BulkMappingPreviewResponse>(
            "/v1/admin/model-provider-mappings/bulk/preview",
            new BulkMappingPreviewRequest
            {
                Mappings = new List<BulkMappingItem>
                {
                    new()
                    {
                        ModelAlias = modelAlias,
                        ProviderId = providerId,
                        ProviderModelId = providerModelId
                    }
                }
            });

        if (!previewResponse.Success || previewResponse.Data == null)
        {
            throw new InvalidOperationException($"Bulk mapping preview failed: {previewResponse.Error}");
        }

        var item = previewResponse.Data.Items.FirstOrDefault();
        if (item?.ModelProviderTypeAssociationId == null)
        {
            throw new InvalidOperationException(
                $"No model catalog association found for '{providerModelId}' " +
                $"({item?.ErrorType}: {item?.ErrorMessage}). " +
                "Seed the catalog via POST /v1/admin/model-catalogs/import and retry.");
        }

        logger.LogInformation(
            "✓ Model catalog association resolved: AssociationId={AssociationId} ({Identifier})",
            item.ModelProviderTypeAssociationId, providerModelId);

        return item.ModelProviderTypeAssociationId.Value;
    }
}

using System.Text.Json;
using ConduitLLM.Admin.Serialization;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Tests.Helpers;

using Moq;

namespace ConduitLLM.Tests.Admin.Services;

public partial class AdminVirtualKeyServiceTests
{
    [Fact]
    public async Task PreviewDiscoveryAsync_ReturnsTheSharedDiscoveryProjection()
    {
        const int keyId = 17;
        _mockVirtualKeyRepository
            .Setup(repository => repository.GetByIdAsync(keyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VirtualKey { Id = keyId, IsEnabled = true });

        _database.Seed(context =>
        {
            var model = ModelTestHelper.CreateCompleteTestModel("preview-model");
            var provider = new Provider
            {
                ProviderName = "OpenAI",
                ProviderType = ProviderType.OpenAI,
                IsEnabled = true
            };
            context.Models.Add(model);
            context.Providers.Add(provider);
            context.SaveChanges();

            var association = new ModelProviderTypeAssociation
            {
                ModelId = model.Id,
                Identifier = "openai/preview-model",
                Provider = ProviderType.OpenAI,
                IsEnabled = true,
                IsPrimary = true,
                ModelCost = new ModelCost
                {
                    CostName = "preview price",
                    InputCostPerMillionTokens = 1m,
                    OutputCostPerMillionTokens = 2m,
                    IsActive = true,
                    EffectiveDate = DateTime.UtcNow.AddDays(-1)
                }
            };
            context.ModelProviderTypeAssociations.Add(association);
            context.SaveChanges();

            context.ModelProviderMappings.Add(new ModelProviderMapping
            {
                ModelAlias = "preview-model",
                ProviderId = provider.Id,
                ProviderModelId = association.Identifier,
                ModelProviderTypeAssociationId = association.Id,
                IsEnabled = true
            });
            context.SaveChanges();
        });

        var response = await _service.PreviewDiscoveryAsync(keyId);

        var modelPreview = Assert.Single(response!.Data);
        Assert.Equal("openai", modelPreview.Provider);
        Assert.NotEmpty(modelPreview.InputModalities);
        Assert.Equal(modelPreview.MaxInputTokens + modelPreview.MaxOutputTokens, modelPreview.MaxTokens);
        Assert.Equal(1m, modelPreview.Pricing!.InputCostPerMillionTokens);
        Assert.Equal(response.Data.Count, response.Count);
    }

    [Fact]
    public async Task PreviewDiscoveryAsync_AdminWireContractUsesCamelCase()
    {
        const int keyId = 18;
        _mockVirtualKeyRepository
            .Setup(repository => repository.GetByIdAsync(keyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VirtualKey { Id = keyId, IsEnabled = true });

        _database.Seed(context =>
        {
            var model = ModelTestHelper.CreateCompleteTestModel("admin-wire-model");
            var provider = new Provider
            {
                ProviderName = "OpenAI",
                ProviderType = ProviderType.OpenAI,
                IsEnabled = true
            };
            context.Models.Add(model);
            context.Providers.Add(provider);
            context.SaveChanges();

            var association = new ModelProviderTypeAssociation
            {
                ModelId = model.Id,
                Identifier = "openai/admin-wire-model",
                Provider = ProviderType.OpenAI,
                IsEnabled = true,
                IsPrimary = true
            };
            context.ModelProviderTypeAssociations.Add(association);
            context.SaveChanges();

            context.ModelProviderMappings.Add(new ModelProviderMapping
            {
                ModelAlias = "admin-wire-model",
                ProviderId = provider.Id,
                ProviderModelId = association.Identifier,
                ModelProviderTypeAssociationId = association.Id,
                IsEnabled = true
            });
            context.SaveChanges();
        });

        var response = await _service.PreviewDiscoveryAsync(keyId);

        var json = JsonSerializer.Serialize(
            response,
            AdminHttpJsonContext.Default.DiscoveryModelsResponse);
        using var document = JsonDocument.Parse(json);
        var modelJson = document.RootElement.GetProperty("data")[0];
        var capabilitiesJson = modelJson.GetProperty("capabilities");

        Assert.True(modelJson.TryGetProperty("displayName", out _));
        Assert.False(modelJson.TryGetProperty("display_name", out _));
        Assert.True(capabilitiesJson.TryGetProperty("chatStream", out _));
        Assert.False(capabilitiesJson.TryGetProperty("chat_stream", out _));
    }
}

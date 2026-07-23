using System.Text.Json;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Models;
using ConduitLLM.Gateway.Services;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Http.Services;

public class ModelMetadataServiceTests
{
    private readonly Mock<IModelProviderMappingRepository> _repository = new();
    private readonly ModelMetadataService _service;

    public ModelMetadataServiceTests()
    {
        _service = new ModelMetadataService(
            _repository.Object,
            Mock.Of<ILogger<ModelMetadataService>>());
    }

    [Fact]
    public async Task GetModelMetadataAsync_ReturnsDirectionalProviderCapabilities()
    {
        var model = new Model
        {
            Id = 42,
            Name = "multimodal",
            SupportsChat = true,
            SupportsVision = true,
            InputModalitiesJson = ModelModalities.Serialize(["text", "image"]),
            OutputModalitiesJson = ModelModalities.Serialize(["text"]),
            CapabilitySource = ModelCapabilitySource.Curated
        };
        var association = new ModelProviderTypeAssociation
        {
            Id = 7,
            Identifier = "vendor/multimodal",
            IsEnabled = true,
            Model = model,
            InputModalitiesJson = ModelModalities.Serialize(["text", "image", "video"]),
            OutputModalitiesJson = ModelModalities.Serialize(["text"]),
            CapabilitySource = ModelCapabilitySource.ProviderApi
        };
        var mapping = new ModelProviderMapping
        {
            ModelAlias = "video-reader",
            IsEnabled = true,
            RoutingPriority = 1,
            Provider = new Provider { IsEnabled = true, ProviderType = ProviderType.OpenRouter },
            ModelProviderTypeAssociation = association
        };
        _repository
            .Setup(repository => repository.GetAllByModelNameAsync("video-reader", default))
            .ReturnsAsync([mapping]);

        var result = await _service.GetModelMetadataAsync("video-reader");

        var json = JsonSerializer.Serialize(result);
        Assert.Contains("\"video_input\":true", json);
        Assert.Contains("\"video_understanding\":true", json);
        Assert.Contains("\"video_generation\":false", json);
        Assert.Contains("\"capability_source\":\"providerapi\"", json);
    }

    [Fact]
    public async Task GetModelMetadataAsync_IgnoresDisabledMappings()
    {
        _repository
            .Setup(repository => repository.GetAllByModelNameAsync("disabled", default))
            .ReturnsAsync([
                new ModelProviderMapping
                {
                    ModelAlias = "disabled",
                    IsEnabled = false,
                    Provider = new Provider { IsEnabled = true },
                    ModelProviderTypeAssociation = new ModelProviderTypeAssociation
                    {
                        IsEnabled = true,
                        Model = new Model()
                    }
                }
            ]);

        Assert.Null(await _service.GetModelMetadataAsync("disabled"));
    }

    [Fact]
    public async Task GetModelMetadataAsync_WhenModelDoesNotExist_ReturnsNull()
    {
        _repository
            .Setup(repository => repository.GetAllByModelNameAsync("missing", default))
            .ReturnsAsync([]);

        Assert.Null(await _service.GetModelMetadataAsync("missing"));
    }
}

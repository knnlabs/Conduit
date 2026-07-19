using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Configuration;
using ConduitLLM.Core;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Constants;
using ConduitLLM.Gateway.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Gateway.Controllers;

[Trait("Category", "Unit")]
[Trait("Component", "Gateway")]
public class EmbeddingsControllerTests
{
    [Fact]
    public async Task CreateEmbedding_WhenMappingHasModelCostId_StoresItForUsageTracking()
    {
        const string alias = "customer-embedding-alias";
        const int modelCostId = 42;

        var mappingService = new Mock<IModelProviderMappingService>();
        mappingService
            .Setup(service => service.GetMappingByModelAliasAsync(alias))
            .ReturnsAsync(new ModelProviderMapping
            {
                ModelAlias = alias,
                ProviderModelId = "provider-embedding-model-v2",
                ProviderId = 7,
                Provider = new Provider { ProviderType = ProviderType.OpenAI },
                ModelProviderTypeAssociationId = 11,
                ModelProviderTypeAssociation = new ModelProviderTypeAssociation
                {
                    Id = 11,
                    ModelCostId = modelCostId
                }
            });

        var client = new Mock<ILLMClient>();
        client
            .Setup(value => value.CreateEmbeddingAsync(
                It.IsAny<EmbeddingRequest>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse
            {
                Object = "list",
                Data = [],
                Model = "provider-embedding-model-v2",
                Usage = new Usage { PromptTokens = 3, TotalTokens = 3 }
            });

        var clientFactory = new Mock<ILLMClientFactory>();
        clientFactory
            .Setup(factory => factory.GetClientAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client.Object);

        var conduit = new Conduit(
            clientFactory.Object,
            Mock.Of<ILogger<Conduit>>());
        var controller = new EmbeddingsController(
            conduit,
            Mock.Of<ILogger<EmbeddingsController>>(),
            mappingService.Object,
            Mock.Of<IEventBus>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.CreateEmbedding(new EmbeddingRequest
        {
            Model = alias,
            Input = "billing regression test",
            EncodingFormat = "float"
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(modelCostId, controller.HttpContext.Items[HttpContextKeys.ModelCostId]);
    }
}

using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Tests.Http.Builders;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers.Discovery.GetModels
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class GetModelsIntegrationTests : DiscoveryControllerTestsBase
    {
        public GetModelsIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task GetModels_WithMultipleModelsFromSameProvider_ReturnsAll()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var provider = new ProviderBuilder().WithProviderId(1).Build();
            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithProvider(provider)
                    .Build(),
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-3.5-turbo")
                    .WithProvider(provider)
                    .Build(),
                new ModelProviderMappingBuilder()
                    .WithModelAlias("text-davinci-003")
                    .WithProvider(provider)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await Controller.GetModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(3, response.count);
        }

        [Fact]
        public async Task GetModels_WithEmptyDatabase_ReturnsEmptyList()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");
            SetupModelProviderMappings(new List<ModelProviderMapping>());

            // Act
            var result = await Controller.GetModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(0, response.count);
            Assert.Empty((IEnumerable<object>)response.data);
        }

        [Fact]
        public async Task GetModels_WithLargeResultSet_HandlesCorrectly()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>();
            for (int i = 1; i <= 150; i++)
            {
                mappings.Add(new ModelProviderMappingBuilder()
                    .WithModelAlias($"model-{i}")
                    .Build());
            }

            SetupModelProviderMappings(mappings);

            // Act
            var result = await Controller.GetModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(150, response.count);
            Assert.Equal(150, ((IEnumerable<object>)response.data).Count());
        }
    }
}
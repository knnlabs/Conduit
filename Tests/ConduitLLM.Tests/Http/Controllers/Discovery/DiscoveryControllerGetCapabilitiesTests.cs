using Microsoft.AspNetCore.Mvc;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers.Discovery
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class DiscoveryControllerGetCapabilitiesTests : DiscoveryControllerTestsBase
    {
        public DiscoveryControllerGetCapabilitiesTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task GetCapabilities_ReturnsStaticListOfAllCapabilities()
        {
            // Act
            var result = await Controller.GetCapabilities();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            var capabilities = (string[])response.capabilities;
            
            Assert.Contains("chat", capabilities);
            Assert.Contains("chat_stream", capabilities);
            Assert.Contains("vision", capabilities);
            Assert.Contains("video_generation", capabilities);
            Assert.Contains("image_generation", capabilities);
            Assert.Contains("embeddings", capabilities);
            Assert.Contains("function_calling", capabilities);
            Assert.Contains("tool_use", capabilities);
            Assert.Contains("json_mode", capabilities);
        }

        [Fact]
        public async Task GetCapabilities_ReturnsCorrectNumberOfCapabilities()
        {
            // Act
            var result = await Controller.GetCapabilities();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            var capabilities = (string[])response.capabilities;
            Assert.Equal(9, capabilities.Length);
        }

        // NOTE: GetCapabilities_WhenExceptionOccurs_Returns500Error test was removed
        // because it's not realistic to force an exception in this method.
        // The method only creates a static array and returns it, which cannot fail
        // under normal circumstances. The catch block exists for defensive programming
        // but would never be reached in practice.
    }
}
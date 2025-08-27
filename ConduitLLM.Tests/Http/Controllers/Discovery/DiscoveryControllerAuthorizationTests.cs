using Microsoft.AspNetCore.Authorization;
using ConduitLLM.Http.Controllers;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers.Discovery
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class DiscoveryControllerAuthorizationTests : DiscoveryControllerTestsBase
    {
        public DiscoveryControllerAuthorizationTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Controller_ShouldRequireAuthorization()
        {
            // Arrange & Act
            var controllerType = typeof(DiscoveryController);
            var authorizeAttribute = Attribute.GetCustomAttribute(controllerType, typeof(AuthorizeAttribute));

            // Assert
            Assert.NotNull(authorizeAttribute);
        }
    }
}
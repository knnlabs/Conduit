using ConduitLLM.Http.Controllers;

namespace ConduitLLM.Tests.Http.Controllers
{
    public partial class VideosControllerTests
    {
        #region Authorization Tests

        [Fact]
        public void Controller_ShouldRequireAuthorization()
        {
            // Arrange & Act
            var controllerType = typeof(VideosController);
            var authorizeAttribute = Attribute.GetCustomAttribute(controllerType, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute));

            // Assert
            Assert.NotNull(authorizeAttribute);
        }

        [Fact]
        public void Controller_ShouldHaveRateLimiting()
        {
            // Arrange & Act
            var controllerType = typeof(VideosController);
            var rateLimitAttribute = Attribute.GetCustomAttribute(controllerType, typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute));

            // Assert
            Assert.NotNull(rateLimitAttribute);
            var attr = (Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute)rateLimitAttribute;
            Assert.Equal("VirtualKeyPolicy", attr.PolicyName);
        }

        #endregion
    }
}
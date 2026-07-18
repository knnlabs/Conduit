using ConduitLLM.Gateway.Controllers;

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
        public void Controller_ShouldNotCarryFrameworkRateLimitingAttribute()
        {
            // Rate limiting is enforced by VirtualKeyRateLimitMiddleware against the
            // Redis-backed IVirtualKeyRateLimitService, not the framework rate limiter.
            // This test guards against a future regression that re-adds the attribute,
            // which would silently route through the deleted no-op policy.
            var controllerType = typeof(VideosController);
            var rateLimitAttribute = Attribute.GetCustomAttribute(controllerType, typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute));

            Assert.Null(rateLimitAttribute);
        }

        #endregion
    }
}
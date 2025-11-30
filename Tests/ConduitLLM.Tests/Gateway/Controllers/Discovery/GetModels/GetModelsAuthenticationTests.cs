using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using Xunit.Abstractions;
using Moq;

namespace ConduitLLM.Tests.Http.Controllers.Discovery.GetModels
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class GetModelsAuthenticationTests : DiscoveryControllerTestsBase
    {
        public GetModelsAuthenticationTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task GetModels_WithoutVirtualKeyClaim_ShouldReturnUnauthorized()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
            Controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var result = await Controller.GetModels();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var errorDto = Assert.IsType<ErrorResponseDto>(unauthorizedResult.Value);
            Assert.Equal("Virtual key not found", errorDto.error.ToString());
        }

        [Fact]
        public async Task GetModels_WithInvalidVirtualKey_ShouldReturnUnauthorized()
        {
            // Arrange
            var claims = new List<Claim> { new Claim("VirtualKey", "invalid-key") };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var httpContext = new DefaultHttpContext { User = principal };
            Controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            MockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync("invalid-key", It.IsAny<string>()))
                .ReturnsAsync((VirtualKey)null);

            // Act
            var result = await Controller.GetModels();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var errorDto = Assert.IsType<ErrorResponseDto>(unauthorizedResult.Value);
            Assert.Equal("Invalid virtual key", errorDto.error.ToString());
        }

        [Fact]
        public async Task GetModels_WithDisabledVirtualKey_ShouldReturnUnauthorized()
        {
            // Arrange
            var claims = new List<Claim> { new Claim("VirtualKey", "disabled-key") };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var httpContext = new DefaultHttpContext { User = principal };
            Controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            MockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync("disabled-key", It.IsAny<string>()))
                .ReturnsAsync((VirtualKey)null);

            // Act
            var result = await Controller.GetModels();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var errorDto = Assert.IsType<ErrorResponseDto>(unauthorizedResult.Value);
            Assert.Equal("Invalid virtual key", errorDto.error.ToString());
        }
    }
}
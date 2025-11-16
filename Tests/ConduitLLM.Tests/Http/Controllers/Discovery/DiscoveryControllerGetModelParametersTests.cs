using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Tests.Http.Builders;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers.Discovery
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class DiscoveryControllerGetModelParametersTests : DiscoveryControllerTestsBase
    {
        public DiscoveryControllerGetModelParametersTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task GetModelParameters_WithoutVirtualKeyClaim_ShouldReturnUnauthorized()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
            Controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var result = await Controller.GetModelParameters("gpt-4");

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var errorDto = Assert.IsType<ErrorResponseDto>(unauthorizedResult.Value);
            Assert.Equal("Virtual key not found", errorDto.error.ToString());
        }

        [Fact]
        public async Task GetModelParameters_WithValidModelAlias_ReturnsParameters()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var parametersJson = @"{
                ""temperature"": {
                    ""type"": ""slider"",
                    ""min"": 0,
                    ""max"": 2,
                    ""default"": 1
                }
            }";

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithModelId(1)
                    .WithSeriesParameters(parametersJson)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await Controller.GetModelParameters("gpt-4");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(1, response.model_id);
            Assert.Equal("gpt-4", response.model_alias);
            Assert.NotNull(response.parameters);
        }

        [Fact]
        public async Task GetModelParameters_WithNumericModelId_ReturnsParameters()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var parametersJson = @"{""test"": ""value""}";

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithModelId(123)
                    .WithSeriesParameters(parametersJson)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await Controller.GetModelParameters("123");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(123, response.model_id);
            Assert.Equal("gpt-4", response.model_alias);
        }

        [Fact]
        public async Task GetModelParameters_WithNonExistentModel_ReturnsNotFound()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");
            SetupModelProviderMappings(new List<ModelProviderMapping>());

            // Act
            var result = await Controller.GetModelParameters("non-existent");

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorDto = Assert.IsType<ErrorResponseDto>(notFoundResult.Value);
            Assert.Equal("Model 'non-existent' not found or has no parameter information", errorDto.error.ToString());
        }

        [Fact]
        public async Task GetModelParameters_WithInvalidParametersJson_ReturnsEmptyObject()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithModelId(1)
                    .WithSeriesParameters("invalid json {}") // Invalid JSON
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await Controller.GetModelParameters("gpt-4");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.NotNull(response.parameters); // Should return empty object, not null
        }

        [Fact]
        public async Task GetModelParameters_WhenExceptionOccurs_Returns500Error()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            MockDbContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await Controller.GetModelParameters("gpt-4");

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            var errorDto = Assert.IsType<ErrorResponseDto>(objectResult.Value);
            Assert.Equal("Failed to retrieve model parameters", errorDto.error.ToString());
        }
    }
}
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ConduitLLM.Core.Models;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers.Discovery.GetModels
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class GetModelsErrorHandlingTests : DiscoveryControllerTestsBase
    {
        public GetModelsErrorHandlingTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task GetModels_WhenDatabaseExceptionOccurs_Returns500Error()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            MockDbContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database connection failed"));

            // Act
            var result = await Controller.GetModels();

            // Assert - GatewayControllerBase returns OpenAIErrorResponse via ExceptionToResponseMapper
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(500, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Equal("An unexpected error occurred", errorResponse.Error.Message);
            Assert.Equal("server_error", errorResponse.Error.Type);
        }
    }
}
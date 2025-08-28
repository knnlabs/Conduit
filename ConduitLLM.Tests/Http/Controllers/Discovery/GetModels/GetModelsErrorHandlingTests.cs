using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ConduitLLM.Configuration.DTOs;
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

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            var errorDto = Assert.IsType<ErrorResponseDto>(objectResult.Value);
            Assert.Equal("Failed to retrieve model discovery information", errorDto.error.ToString());
        }

        [Fact]
        public async Task GetModels_WhenExceptionOccurs_LogsError()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");
            var exception = new Exception("Test exception");

            MockDbContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

            // Act
            await Controller.GetModels();

            // Assert
            MockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Error retrieving model discovery information")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}
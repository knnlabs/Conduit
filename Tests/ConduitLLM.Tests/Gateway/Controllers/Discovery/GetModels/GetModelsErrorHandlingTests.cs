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

            // Act + Assert — error mapping is owned by OpenAIErrorMiddleware; the action propagates.
            var act = async () => await Controller.GetModels();
            await act.Should().ThrowAsync<Exception>().WithMessage("Database connection failed");
        }
    }
}
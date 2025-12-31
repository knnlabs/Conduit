using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Middleware;
using ConduitLLM.Tests.Http.Middleware.Builders;
using ConduitLLM.Tests.Http.Middleware.Assertions;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Http.Middleware
{
    /// <summary>
    /// Tests for path filtering in UsageTrackingMiddleware.
    /// Ensures that only actual generation requests are tracked, not polling/status endpoints.
    /// </summary>
    public partial class UsageTrackingMiddlewareTests
    {
        [Theory]
        [InlineData("/v1/videos/generations/async", true)]
        [InlineData("/v1/videos/generations", true)]
        [InlineData("/v1/images/generations", true)]
        [InlineData("/v1/chat/completions", true)]
        [InlineData("/v1/completions", true)]
        [InlineData("/v1/embeddings", true)]
        [InlineData("/v1/audio/transcriptions", true)]
        [InlineData("/v1/audio/speech", true)]
        public async Task ShouldTrackUsage_ForGenerationEndpoints_ReturnsTrue(string path, bool shouldTrack)
        {
            // Arrange
            var context = new HttpContextBuilder()
                .WithPath(path)
                .WithVirtualKey(1)
                .WithStatusCode(200)
                .Build();

            Fixture.SetupDefaultCost(0.001m);

            // Act
            await Invoker
                .WithResponse(ResponseBuilders.OpenAI()
                    .WithModel("test-model")
                    .WithUsage(10, 20)
                    .Build())
                .InvokeAsync(context);

            // Assert - Should track usage (call cost calculation)
            if (shouldTrack)
            {
                UsageTrackingAssertions.VerifyCostCalculatedOnce(Fixture.CostService);
            }
        }

        [Theory]
        [InlineData("/v1/videos/generations/tasks/task_abc123")]
        [InlineData("/v1/videos/generations/tasks/task_xyz789")]
        [InlineData("/v1/images/generations/tasks/task_def456")]
        [InlineData("/v1/images/status/task_ghi789")]
        [InlineData("/v1/tasks/some-task-id")]
        [InlineData("/v1/videos/status")]
        public async Task ShouldTrackUsage_ForPollingEndpoints_ReturnsFalse(string path)
        {
            // Arrange
            var context = new HttpContextBuilder()
                .WithPath(path)
                .WithVirtualKey(1)
                .WithStatusCode(200)
                .Build();

            Fixture.SetupDefaultCost(0.001m);

            var response = new
            {
                taskId = "task_abc123",
                status = "completed",
                videoUrl = "https://example.com/video.mp4"
            };

            // Act
            await Invoker
                .WithResponse(response)
                .InvokeAsync(context);

            // Assert - Should NOT track usage for polling endpoints
            UsageTrackingAssertions.VerifyNoCostCalculation(Fixture.CostService);
            UsageTrackingAssertions.VerifyNoRequestLogged(Fixture.RequestLogService);
        }

        [Fact]
        public async Task ShouldTrackUsage_WithoutVirtualKeyId_ReturnsFalse()
        {
            // Arrange - No virtual key set
            var context = new HttpContextBuilder()
                .ForChatCompletions()
                .WithStatusCode(200)
                // Note: NOT setting WithVirtualKey()
                .Build();

            // Act
            await Invoker
                .WithResponse(ResponseBuilders.OpenAI()
                    .WithModel("gpt-4")
                    .WithUsage(10, 20)
                    .Build())
                .InvokeAsync(context);

            // Assert - Should NOT track usage without VirtualKeyId
            UsageTrackingAssertions.VerifyNoCostCalculation(Fixture.CostService);
        }

        [Theory]
        [InlineData(400)]
        [InlineData(401)]
        [InlineData(403)]
        [InlineData(404)]
        [InlineData(429)]
        [InlineData(500)]
        [InlineData(503)]
        public async Task ShouldTrackUsage_WithErrorStatusCode_ReturnsFalse(int statusCode)
        {
            // Arrange
            var context = new HttpContextBuilder()
                .ForChatCompletions()
                .WithVirtualKey(1)
                .AsError(statusCode)
                .Build();

            var response = new
            {
                error = new { message = "An error occurred", type = "invalid_request_error" }
            };

            // Act
            await Invoker
                .WithResponse(response)
                .InvokeAsync(context);

            // Assert - Should NOT track usage for error responses
            UsageTrackingAssertions.VerifyNoCostCalculation(Fixture.CostService);
        }

        [Fact]
        public async Task ShouldTrackUsage_ForNonApiPath_ReturnsFalse()
        {
            // Arrange - Path doesn't start with /v1
            var context = new HttpContextBuilder()
                .ForNonApiPath()
                .WithVirtualKey(1)
                .WithStatusCode(200)
                .Build();

            var response = new { status = "healthy" };

            // Act
            await Invoker
                .WithResponse(response)
                .InvokeAsync(context);

            // Assert - Should NOT track usage for non-API paths
            UsageTrackingAssertions.VerifyNoCostCalculation(Fixture.CostService);
        }
    }
}

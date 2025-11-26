using ConduitLLM.Http.Middleware;
using Microsoft.Extensions.Logging;
using Moq;

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
        // Note: /v1/functions/execute is handled differently by ProcessFunctionResponseAsync
        // and doesn't use standard usage-based cost calculation
        public async Task ShouldTrackUsage_ForGenerationEndpoints_ReturnsTrue(string path, bool shouldTrack)
        {
            // Arrange
            var context = CreateHttpContext(path);
            context.Items["VirtualKeyId"] = 1;
            context.Response.StatusCode = 200;

            var response = new
            {
                model = "test-model",
                usage = new { prompt_tokens = 10, completion_tokens = 20, total_tokens = 30 }
            };

            SetupMockResponse(context, response);
            _mockCostService.Setup(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<ConduitLLM.Core.Models.Usage>(), default))
                .ReturnsAsync(0.001m);
            _mockBatchSpendService.SetupGet(x => x.IsHealthy).Returns(true);

            // Create a new middleware instance with the updated _next delegate
            var middleware = new UsageTrackingMiddleware(_next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context, _mockCostService.Object, _mockBatchSpendService.Object,
                _mockRequestLogService.Object, _mockVirtualKeyService.Object, _mockBillingAuditService.Object, _mockToolCostService.Object);

            // Assert - Should track usage (call cost calculation)
            if (shouldTrack)
            {
                _mockCostService.Verify(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<ConduitLLM.Core.Models.Usage>(), default), Times.AtLeastOnce);
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
            var context = CreateHttpContext(path);
            context.Items["VirtualKeyId"] = 1;
            context.Response.StatusCode = 200;

            var response = new
            {
                taskId = "task_abc123",
                status = "completed",
                videoUrl = "https://example.com/video.mp4"
            };

            SetupMockResponse(context, response);
            _mockCostService.Setup(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<ConduitLLM.Core.Models.Usage>(), default))
                .ReturnsAsync(0.001m);
            _mockBatchSpendService.SetupGet(x => x.IsHealthy).Returns(true);

            // Create a new middleware instance with the updated _next delegate
            var middleware = new UsageTrackingMiddleware(_next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context, _mockCostService.Object, _mockBatchSpendService.Object,
                _mockRequestLogService.Object, _mockVirtualKeyService.Object, _mockBillingAuditService.Object, _mockToolCostService.Object);

            // Assert - Should NOT track usage for polling endpoints
            _mockCostService.Verify(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<ConduitLLM.Core.Models.Usage>(), default), Times.Never);
            _mockRequestLogService.Verify(x => x.LogRequestAsync(It.IsAny<ConduitLLM.Configuration.DTOs.LogRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task ShouldTrackUsage_WithoutVirtualKeyId_ReturnsFalse()
        {
            // Arrange
            var context = CreateHttpContext("/v1/chat/completions");
            // Note: NOT setting context.Items["VirtualKeyId"]
            context.Response.StatusCode = 200;

            var response = new
            {
                model = "gpt-4",
                usage = new { prompt_tokens = 10, completion_tokens = 20, total_tokens = 30 }
            };

            SetupMockResponse(context, response);

            // Create a new middleware instance with the updated _next delegate
            var middleware = new UsageTrackingMiddleware(_next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context, _mockCostService.Object, _mockBatchSpendService.Object,
                _mockRequestLogService.Object, _mockVirtualKeyService.Object, _mockBillingAuditService.Object, _mockToolCostService.Object);

            // Assert - Should NOT track usage without VirtualKeyId
            _mockCostService.Verify(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<ConduitLLM.Core.Models.Usage>(), default), Times.Never);
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
            var context = CreateHttpContext("/v1/chat/completions");
            context.Items["VirtualKeyId"] = 1;
            context.Response.StatusCode = statusCode;

            var response = new
            {
                error = new { message = "An error occurred", type = "invalid_request_error" }
            };

            SetupMockResponse(context, response);

            // Create a new middleware instance with the updated _next delegate
            var middleware = new UsageTrackingMiddleware(_next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context, _mockCostService.Object, _mockBatchSpendService.Object,
                _mockRequestLogService.Object, _mockVirtualKeyService.Object, _mockBillingAuditService.Object, _mockToolCostService.Object);

            // Assert - Should NOT track usage for error responses
            _mockCostService.Verify(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<ConduitLLM.Core.Models.Usage>(), default), Times.Never);
        }

        [Fact]
        public async Task ShouldTrackUsage_ForNonApiPath_ReturnsFalse()
        {
            // Arrange - Path doesn't start with /v1
            var context = CreateHttpContext("/health");
            context.Items["VirtualKeyId"] = 1;
            context.Response.StatusCode = 200;

            var response = new { status = "healthy" };

            SetupMockResponse(context, response);

            // Create a new middleware instance with the updated _next delegate
            var middleware = new UsageTrackingMiddleware(_next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context, _mockCostService.Object, _mockBatchSpendService.Object,
                _mockRequestLogService.Object, _mockVirtualKeyService.Object, _mockBillingAuditService.Object, _mockToolCostService.Object);

            // Assert - Should NOT track usage for non-API paths
            _mockCostService.Verify(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<ConduitLLM.Core.Models.Usage>(), default), Times.Never);
        }
    }
}

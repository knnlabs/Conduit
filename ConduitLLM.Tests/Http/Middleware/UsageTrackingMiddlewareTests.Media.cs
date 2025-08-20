using System.Threading.Tasks;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Http.Middleware;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Http.Middleware
{
    public partial class UsageTrackingMiddlewareTests
    {
        [Fact]
        public async Task OpenAI_ImageGeneration_Response_Tracks_Usage()
        {
            // Arrange
            var context = CreateHttpContext("/v1/images/generations");
            var virtualKeyId = 321;
            var virtualKey = "test-key-321";
            
            context.Items["VirtualKeyId"] = virtualKeyId;
            context.Items["VirtualKey"] = virtualKey;
            context.Items["ProviderType"] = "OpenAI";

            var imageResponse = new
            {
                created = 1677652288,
                model = "dall-e-3",
                data = new[]
                {
                    new
                    {
                        url = "https://example.com/image1.png",
                        revised_prompt = "A futuristic city with flying cars"
                    }
                },
                usage = new
                {
                    images = 1
                }
            };

            SetupMockResponse(context, imageResponse);
            
            _mockCostService.Setup(x => x.CalculateCostAsync("dall-e-3", It.IsAny<Usage>(), default))
                .ReturnsAsync(0.04m);
            
            _mockBatchSpendService.SetupGet(x => x.IsHealthy).Returns(true);

            // Create a new middleware instance that uses our updated _next delegate
            var middleware = new UsageTrackingMiddleware(_next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context, _mockCostService.Object, _mockBatchSpendService.Object, 
                _mockRequestLogService.Object, _mockVirtualKeyService.Object);

            // Assert
            _mockCostService.Verify(x => x.CalculateCostAsync("dall-e-3", 
                It.Is<Usage>(u => u.ImageCount == 1), 
                default), Times.Once);
            
            _mockBatchSpendService.Verify(x => x.QueueSpendUpdate(virtualKeyId, 0.04m), Times.Once);
            
            _mockRequestLogService.Verify(x => x.LogRequestAsync(It.Is<LogRequestDto>(dto =>
                dto.RequestType == "image"
            )), Times.Once);
        }
    }
}